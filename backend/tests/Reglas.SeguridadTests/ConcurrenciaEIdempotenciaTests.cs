using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E6.8 (docs/PROGRESO.md) — <c>sp_AsignarPersona</c>: bloqueo
/// determinista (04 §7.3) e idempotencia (§12.4) contra la base real.
/// Mismo patrón de fixture que <see cref="SugerenciaDePuestoTests"/>.
/// </summary>
public class ConcurrenciaEIdempotenciaTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != System.Data.ConnectionState.Open) await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InitializeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    // ═══ Helpers de datos ═══

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, string categoria = "operario", string nombre = "Persona de prueba")
    {
        var p = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = nombre, Categoria = categoria, Situacion = "presente_sin_asignar" };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoRotativoAsync(SmartAssignDbContext ctx, byte lineaId, string? codigo = null)
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = codigo ?? $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba", Tipo = "rotativo" };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx, string rol = "coordinador")
    {
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = rol, OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<(byte turno, int jornadaLineaId)> CrearJornadaAbiertaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return (turno.Id, jornada.Id);
    }

    private static async Task<short> CrearCapacidadAsync(SmartAssignDbContext ctx, string nombre)
    {
        var c = new CapacidadFisica { Codigo = $"C{Guid.NewGuid():N}"[..10], Nombre = nombre };
        ctx.CapacidadesFisicas.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    // ═══ Invocación del SP ═══

    private record Resultado(string? Codigo, string? Mensaje, long? AsignacionId);

    private async Task<Resultado> AsignarAsync(
        int personalId, int puestoId, int usuarioId, int jornadaLineaId,
        Guid idempotencyKey, bool cederPerfil = false, string origen = "manual_supervisor")
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var contexto = conexion.CreateCommand();
        contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await contexto.ExecuteNonQueryAsync();

        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_AsignarPersona";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@origen", origen);
        cmd.Parameters.AddWithValue("@idempotency_key", idempotencyKey);
        cmd.Parameters.AddWithValue("@ceder_perfil", cederPerfil);
        var pCodigo = new SqlParameter("@codigo_rechazo", System.Data.SqlDbType.VarChar, 40) { Direction = System.Data.ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", System.Data.SqlDbType.NVarChar, 400) { Direction = System.Data.ParameterDirection.Output };
        var pAsignacion = new SqlParameter("@asignacion_id", System.Data.SqlDbType.BigInt) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        cmd.Parameters.Add(pAsignacion);

        await cmd.ExecuteNonQueryAsync();
        return new Resultado(pCodigo.Value as string, pMensaje.Value as string, pAsignacion.Value as long?);
    }

    // ═══ Camino feliz ═══

    [Fact]
    public async Task Asigna_la_persona_al_puesto_y_actualiza_su_situacion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);

        var resultado = await AsignarAsync(persona, puesto, usuario, jornadaId, Guid.NewGuid());

        resultado.Codigo.Should().BeNull();
        resultado.AsignacionId.Should().NotBeNull();

        await using var verificacion = CrearContexto();
        var situacion = await verificacion.Personas.Where(p => p.Id == persona).Select(p => p.Situacion).SingleAsync();
        situacion.Should().Be("asignado");
        var asignacion = await verificacion.Asignaciones.SingleAsync(a => a.Id == resultado.AsignacionId);
        asignacion.PuestoId.Should().Be(puesto);
        asignacion.Origen.Should().Be("manual_supervisor");
        asignacion.Fin.Should().BeNull();
    }

    [Fact]
    public async Task El_exito_queda_auditado_con_resultado_OK()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);

        await AsignarAsync(persona, puesto, usuario, jornadaId, Guid.NewGuid());

        await using var verificacion = CrearContexto();
        var auditoria = await verificacion.Auditorias
            .Where(a => a.UsuarioId == usuario && a.Accion == "ASIGNAR" && a.PersonalId == persona)
            .SingleAsync();
        auditoria.Resultado.Should().Be("OK");
    }

    // ═══ Idempotencia (§12.4) ═══

    [Fact]
    public async Task Reintentar_con_la_misma_clave_devuelve_el_mismo_resultado_sin_crear_una_segunda_asignacion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);
        var clave = Guid.NewGuid();

        var primero = await AsignarAsync(persona, puesto, usuario, jornadaId, clave);
        var segundo = await AsignarAsync(persona, puesto, usuario, jornadaId, clave); // mismo idempotency_key, "doble toque"

        segundo.AsignacionId.Should().Be(primero.AsignacionId);

        await using var verificacion = CrearContexto();
        var totalAsignaciones = await verificacion.Asignaciones.CountAsync(a => a.PuestoId == puesto);
        totalAsignaciones.Should().Be(1, "el reintento no debe volver a insertar");
    }

    [Fact]
    public async Task Un_rechazo_tambien_queda_cacheado_por_idempotencia()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, categoria: "averiero"); // incompatible con rotativo
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);
        var clave = Guid.NewGuid();

        var primero = await AsignarAsync(persona, puesto, usuario, jornadaId, clave);
        var segundo = await AsignarAsync(persona, puesto, usuario, jornadaId, clave);

        primero.Codigo.Should().Be("CATEGORIA_INCOMPATIBLE");
        segundo.Codigo.Should().Be("CATEGORIA_INCOMPATIBLE");
        segundo.Mensaje.Should().Be(primero.Mensaje, "el reintento repite exactamente lo ya ocurrido, no vuelve a evaluar nada");
    }

    // ═══ Atomicidad y rechazo nominal (B1) ═══

    [Fact]
    public async Task Un_rechazo_no_cambia_la_situacion_de_la_persona_ni_crea_asignacion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, categoria: "averiero");
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);

        var resultado = await AsignarAsync(persona, puesto, usuario, jornadaId, Guid.NewGuid());

        resultado.Codigo.Should().Be("CATEGORIA_INCOMPATIBLE");
        resultado.AsignacionId.Should().BeNull();

        await using var verificacion = CrearContexto();
        var situacion = await verificacion.Personas.Where(p => p.Id == persona).Select(p => p.Situacion).SingleAsync();
        situacion.Should().Be("presente_sin_asignar", "§7.5: nunca queda el puesto ocupado y la persona a medias");
        (await verificacion.Asignaciones.AnyAsync(a => a.PuestoId == puesto)).Should().BeFalse();
    }

    [Fact]
    public async Task Restriccion_medica_bloquea_y_tambien_queda_auditada_como_rechazo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);
        var capacidad = await CrearCapacidadAsync(ctx, "No levantar carga");
        ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puesto, CapacidadId = capacidad });
        ctx.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = persona, CapacidadId = capacidad, FechaInicio = new DateOnly(2020, 1, 1),
            FechaFin = null, FechaDictamen = new DateOnly(2020, 1, 1), Fuente = "Enfermería", RegistradoPor = usuario,
        });
        await ctx.SaveChangesAsync();

        var resultado = await AsignarAsync(persona, puesto, usuario, jornadaId, Guid.NewGuid());

        resultado.Codigo.Should().Be("RESTRICCION_MEDICA");

        await using var verificacion = CrearContexto();
        var auditoria = await verificacion.Auditorias
            .Where(a => a.UsuarioId == usuario && a.Accion == "ASIGNAR" && a.PersonalId == persona)
            .SingleAsync();
        auditoria.Resultado.Should().Be("RECHAZO");
        auditoria.CodigoRechazo.Should().Be("RESTRICCION_MEDICA");
    }

    [Fact]
    public async Task El_rechazo_por_puesto_ocupado_es_nominal_con_nombre_linea_y_puesto()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var ganador = await CrearPersonaAsync(ctx, nombre: "María López Hernández");
        var perdedor = await CrearPersonaAsync(ctx, nombre: "Otro Operario");
        var puesto = await CrearPuestoRotativoAsync(ctx, 1, codigo: "L1-R07");
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);

        await AsignarAsync(ganador, puesto, usuario, jornadaId, Guid.NewGuid()); // ocupa el puesto primero
        var resultado = await AsignarAsync(perdedor, puesto, usuario, jornadaId, Guid.NewGuid());

        resultado.Codigo.Should().Be("PUESTO_OCUPADO");
        resultado.Mensaje.Should().Contain("María López Hernández")
            .And.Contain("L1")
            .And.Contain("por otro supervisor", "00 §B1: rechazo nominal, nunca genérico");
    }

    // ═══ Concurrencia real (04 §7.3, B1) — el corazón de esta UT ═══

    [Fact]
    public async Task Dos_supervisores_compitiendo_por_el_mismo_puesto_solo_uno_gana()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var personaA = await CrearPersonaAsync(ctx, nombre: "Persona A");
        var personaB = await CrearPersonaAsync(ctx, nombre: "Persona B");
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);

        // Dos conexiones reales, en paralelo, por el MISMO puesto — el
        // UPDLOCK+HOLDLOCK (puesto antes que persona) debe serializarlas:
        // una espera a que la otra confirme, nunca corren libres.
        var tareaA = AsignarAsync(personaA, puesto, usuario, jornadaId, Guid.NewGuid());
        var tareaB = AsignarAsync(personaB, puesto, usuario, jornadaId, Guid.NewGuid());
        var resultados = await Task.WhenAll(tareaA, tareaB);

        var ganadores = resultados.Where(r => r.Codigo is null).ToList();
        var perdedores = resultados.Where(r => r.Codigo == "PUESTO_OCUPADO").ToList();

        ganadores.Should().HaveCount(1, "gana la primera transacción que confirma, nunca las dos (B1)");
        perdedores.Should().HaveCount(1);
        perdedores.Single().Mensaje.Should().Contain("por otro supervisor");

        await using var verificacion = CrearContexto();
        var activas = await verificacion.Asignaciones.CountAsync(a => a.PuestoId == puesto && a.Fin == null);
        activas.Should().Be(1, "el índice único UX_Asig_puesto_activo es la última red, pero el bloqueo ya debería haber evitado la carrera");
    }

    [Fact]
    public async Task Diez_intentos_simultaneos_por_el_mismo_puesto_solo_uno_gana()
    {
        // Prueba de carga ligera del mismo principio — no solo "dos
        // conexiones tienen suerte de intercalarse bien", sino que el
        // mecanismo sostiene la garantía bajo más contención.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        var (_, jornadaId) = await CrearJornadaAbiertaAsync(ctx, 1);
        var personas = new List<int>();
        for (var i = 0; i < 10; i++) personas.Add(await CrearPersonaAsync(ctx, nombre: $"Persona {i}"));

        var tareas = personas.Select(p => AsignarAsync(p, puesto, usuario, jornadaId, Guid.NewGuid())).ToArray();
        var resultados = await Task.WhenAll(tareas);

        resultados.Count(r => r.Codigo is null).Should().Be(1);
        resultados.Count(r => r.Codigo == "PUESTO_OCUPADO").Should().Be(9);

        await using var verificacion = CrearContexto();
        (await verificacion.Asignaciones.CountAsync(a => a.PuestoId == puesto && a.Fin == null)).Should().Be(1);
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E10.3 (docs/PROGRESO.md): <c>sp_ExtraccionInversa</c> — 00 §B5, el
/// piso de seguridad, combinado con el orden derivado (E10.1) y el
/// disparador ya existente (E10.2). Mismo patrón de base descartable
/// que el resto de la suite.
/// </summary>
public class ExtraccionInversaConPisoTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await conexion.OpenAsync();
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

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx)
    {
        var u = new Usuario
        {
            Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba",
            Rol = "coordinador", OrigenIdentidad = "local", Activo = true,
        };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> JornadaAbiertaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var existente = await ctx.JornadasLinea.Where(j => j.LineaId == lineaId && j.CerradoEn == null).Select(j => j.Id).SingleOrDefaultAsync();
        if (existente != 0) return existente;

        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    private static async Task<int> CrearPuestoAsync(SmartAssignDbContext ctx, byte lineaId, string tipo = "rotativo")
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba", Tipo = tipo,
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    /// <summary>Ocupa @cuantos puestos rotativos de la línea con operarios reales — la "dotación" que cuenta contra el piso (B5).</summary>
    private static async Task<List<int>> OcuparRotativosAsync(SmartAssignDbContext ctx, byte lineaId, int cuantos, int usuarioId, string ficha = "F")
    {
        var jornada = await JornadaAbiertaAsync(ctx, lineaId);
        var ocupantes = new List<int>();
        for (var i = 0; i < cuantos; i++)
        {
            var puesto = await CrearPuestoAsync(ctx, lineaId);
            var persona = new Personal { Ficha = $"{ficha}{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operario" };
            ctx.Personas.Add(persona);
            await ctx.SaveChangesAsync();
            ctx.Asignaciones.Add(new Asignacion
            {
                JornadaLineaId = jornada, PuestoId = puesto, PersonalId = persona.Id,
                Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
            });
            await ctx.SaveChangesAsync();
            ocupantes.Add(persona.Id);
        }
        return ocupantes;
    }

    private static async Task<int> OcuparFijoAsync(SmartAssignDbContext ctx, byte lineaId, int usuarioId)
    {
        var puesto = await CrearPuestoAsync(ctx, lineaId, tipo: "fijo");
        var jornada = await JornadaAbiertaAsync(ctx, lineaId);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Titular fijo de prueba", Categoria = "operador_a" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada, PuestoId = puesto, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
        return persona.Id;
    }

    private async Task ActivarLineasAsync(params byte[] lineas)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "UPDATE Linea SET activa_hoy = 1 WHERE Id IN (" + string.Join(",", lineas) + ");";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetPisoAsync(byte lineaId, short? minimo)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "UPDATE Linea SET minimo_operarios = @min WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@min", (object?)minimo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", lineaId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ═══ Invocación del SP ═══

    private record Resultado(int? CandidatoId, byte? LineaOrigen, long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<Resultado> ExtraerAsync(int puestoSolicitante, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ExtraccionInversa";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@puesto_id_solicitante", puestoSolicitante);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCandidato = new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pLinea = new SqlParameter("@linea_origen", SqlDbType.TinyInt) { Direction = ParameterDirection.Output };
        var pMovimiento = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCandidato);
        cmd.Parameters.Add(pLinea);
        cmd.Parameters.Add(pMovimiento);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new Resultado(
            pCandidato.Value as int?, pLinea.Value as byte?, pMovimiento.Value as long?,
            pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Con_margen_sobre_el_piso_extrae_correctamente_cierra_la_asignacion_anterior()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await ActivarLineasAsync(4, 10, 9, 3, 5, 7, 6, 2, 1);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);
        await SetPisoAsync(10, minimo: 1);
        var ocupantes = await OcuparRotativosAsync(ctx, lineaId: 10, cuantos: 2, usuario); // 2 > piso de 1: hay margen

        var resultado = await ExtraerAsync(puestoSolicitante, usuario);

        resultado.Codigo.Should().BeNull();
        resultado.LineaOrigen.Should().Be((byte)10, "L10 es la primera en el orden derivado para L4 solicitante");
        resultado.CandidatoId.Should().BeOneOf(ocupantes);
        resultado.MovimientoId.Should().NotBeNull();

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.Motivo.Should().Be("extraccion_inversa");
        movimiento.LineaDestino.Should().Be((byte)4);
        movimiento.PuestoDestinoId.Should().Be(puestoSolicitante);
        movimiento.Estado.Should().Be("en_transito");

        var extraido = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == resultado.CandidatoId);
        extraido.Situacion.Should().Be("en_transito");

        var asignacionCerrada = await ctx.Asignaciones.AsNoTracking()
            .Where(a => a.PersonalId == resultado.CandidatoId).SingleAsync();
        asignacionCerrada.Fin.Should().NotBeNull("la extracción cierra la asignación vigente del donante");
    }

    [Fact]
    public async Task Una_linea_exactamente_en_el_piso_queda_inmune_y_se_salta()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await ActivarLineasAsync(4, 10, 9, 3, 5, 7, 6, 2, 1);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);

        await SetPisoAsync(10, minimo: 2);
        await OcuparRotativosAsync(ctx, lineaId: 10, cuantos: 2, usuario); // exactamente en el piso — inmune

        await SetPisoAsync(9, minimo: 1);
        var ocupantesL9 = await OcuparRotativosAsync(ctx, lineaId: 9, cuantos: 2, usuario); // con margen

        var resultado = await ExtraerAsync(puestoSolicitante, usuario);

        resultado.LineaOrigen.Should().Be((byte)9, "L10 está en su piso — inmune — pasa a L9");
        resultado.CandidatoId.Should().BeOneOf(ocupantesL9);
    }

    [Fact]
    public async Task Sin_ningun_margen_en_todo_el_recorrido_alerta_capacidad_critica_de_planta()
    {
        // §9.6, literal: "Capacidad crítica de planta agotada. Requiere intervención humana."
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await ActivarLineasAsync(4, 10, 9, 3, 5, 7, 6, 2, 1);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);

        foreach (byte linea in new byte[] { 10, 9, 3, 5, 7, 6, 2, 1 })
        {
            await SetPisoAsync(linea, minimo: 1);
            await OcuparRotativosAsync(ctx, linea, cuantos: 1, usuario); // exactamente en el piso — todas inmunes
        }

        var resultado = await ExtraerAsync(puestoSolicitante, usuario);

        resultado.CandidatoId.Should().BeNull();
        resultado.Codigo.Should().Be("CAPACIDAD_CRITICA_DE_PLANTA_AGOTADA");
        resultado.Mensaje.Should().Be("Capacidad crítica de planta agotada. Requiere intervención humana.");
    }

    [Fact]
    public async Task Los_ocupantes_de_puestos_fijos_nunca_cuentan_ni_se_extraen()
    {
        // 00 §B5, literal: "los puestos fijos no cuentan: no se extraen nunca".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await ActivarLineasAsync(4, 10, 9, 3, 5, 7, 6, 2, 1);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);

        // L10 solo tiene un fijo ocupado — no cuenta como dotación de rotativos, y no es extraíble.
        await OcuparFijoAsync(ctx, lineaId: 10, usuario);
        await SetPisoAsync(9, minimo: 0);
        var ocupantesL9 = await OcuparRotativosAsync(ctx, lineaId: 9, cuantos: 1, usuario);

        var resultado = await ExtraerAsync(puestoSolicitante, usuario);

        resultado.LineaOrigen.Should().Be((byte)9, "L10 no tiene ningún rotativo ocupado — no hay de dónde extraer ahí");
        resultado.CandidatoId.Should().BeOneOf(ocupantesL9);
    }

    [Fact]
    public async Task Sin_ningun_piso_configurado_la_regla_no_aplica_todavia_nunca_es_inmune()
    {
        // R2: sin Linea.minimo_operarios ni Parametro['minimo_operarios_default'], nunca inventar un umbral.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await ActivarLineasAsync(4, 10, 9, 3, 5, 7, 6, 2, 1);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);
        var ocupantesL10 = await OcuparRotativosAsync(ctx, lineaId: 10, cuantos: 1, usuario); // ni un piso configurado en ningún lado

        var resultado = await ExtraerAsync(puestoSolicitante, usuario);

        resultado.LineaOrigen.Should().Be((byte)10);
        resultado.CandidatoId.Should().BeOneOf(ocupantesL10);
    }

    [Fact]
    public async Task Si_la_L8_todavia_tiene_candidatos_no_se_ejecuta_la_extraccion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);
        var candidatoEnBolson = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Candidato del Bolsón", Categoria = "operario",
            Situacion = "en_bolson", LineaFisicaActual = 8,
        };
        ctx.Personas.Add(candidatoEnBolson);
        await ctx.SaveChangesAsync();

        var resultado = await ExtraerAsync(puestoSolicitante, usuario);

        resultado.Codigo.Should().Be("L8_TIENE_CANDIDATOS_DISPONIBLES");
        resultado.CandidatoId.Should().BeNull();
    }
}

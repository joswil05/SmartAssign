using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.1 (docs/PROGRESO.md): arranca E9 (Motor de relevos).
/// <c>sp_DetectarFatiga</c>/<c>sp_MarcarRelevoSolicitado</c> — §9.4 paso
/// 1, literal: "en ambos casos, el puesto no se libera todavía". Mismo
/// patrón de base descartable que el resto de la suite.
/// </summary>
public class SolicitudDeRelevoTests : IAsyncLifetime
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

    private static async Task<int> CrearPuestoAsync(
        SmartAssignDbContext ctx, byte lineaId, string tipo = "rotativo", short? horasEnPuesto = 1, short? umbralCriticoHoras = null)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba", Tipo = tipo, HorasEnPuesto = horasEnPuesto, UmbralCriticoHoras = umbralCriticoHoras,
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

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

    /// <summary>Ocupa el puesto desde hace @minutosAtras — controla el nivel de fatiga real.</summary>
    private static async Task OcuparPuestoAsync(SmartAssignDbContext ctx, int puestoId, byte lineaId, int usuarioId, int minutosAtras)
    {
        var jornadaId = await JornadaAbiertaAsync(ctx, lineaId);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operario" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaId, PuestoId = puestoId, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddMinutes(-minutosAtras), AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación de los SP ═══

    private async Task<int> DetectarFatigaAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DetectarFatiga";
        cmd.CommandType = CommandType.StoredProcedure;
        var pAbiertas = new SqlParameter("@abiertas", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pAbiertas);
        await cmd.ExecuteNonQueryAsync();
        return (int)pAbiertas.Value;
    }

    private record ResultadoMarcar(long? SolicitudId, string? Codigo, string? Mensaje);

    private async Task<ResultadoMarcar> MarcarManualAsync(int puestoId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_MarcarRelevoSolicitado";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pId = new SqlParameter("@solicitud_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoMarcar(pId.Value as long?, pCodigo.Value as string, pMensaje.Value as string);
    }

    // ═══ sp_DetectarFatiga ═══

    [Fact]
    public async Task Detecta_un_rotativo_fatigado_y_abre_su_solicitud_sin_liberar_el_puesto()
    {
        // §9.4 p1, literal: "el puesto no se libera todavía".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, horasEnPuesto: 1); // umbral sugerido: 60 min
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 70);

        var abiertas = await DetectarFatigaAsync();

        abiertas.Should().Be(1);
        var solicitud = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.PuestoId == puesto);
        solicitud.Origen.Should().Be("umbral_automatico");
        solicitud.Nivel.Should().Be("sugerido");
        solicitud.ResueltaEn.Should().BeNull();
        solicitud.ExcesoRelativo.Should().NotBeNull();

        var ocupacionSigueActiva = await ctx.Asignaciones.AsNoTracking().AnyAsync(a => a.PuestoId == puesto && a.Fin == null);
        ocupacionSigueActiva.Should().BeTrue("el operario sigue produciendo hasta que llegue su reemplazo");
    }

    [Fact]
    public async Task Un_rotativo_sin_fatiga_no_genera_solicitud()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 10); // muy por debajo del umbral

        var abiertas = await DetectarFatigaAsync();

        abiertas.Should().Be(0);
        (await ctx.SolicitudesRelevo.AsNoTracking().AnyAsync(s => s.PuestoId == puesto)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_fijo_fatigado_en_teoria_nunca_genera_solicitud()
    {
        // §9.1: "la fatiga solo aplica a puestos rotativos".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, tipo: "fijo", horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 500);

        var abiertas = await DetectarFatigaAsync();

        abiertas.Should().Be(0);
    }

    [Fact]
    public async Task Correr_la_deteccion_dos_veces_no_duplica_la_solicitud()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 70);
        (await DetectarFatigaAsync()).Should().Be(1);

        var segunda = await DetectarFatigaAsync();

        segunda.Should().Be(0, "UX_SR_abierta ya tiene una fila viva para ese puesto");
        (await ctx.SolicitudesRelevo.AsNoTracking().CountAsync(s => s.PuestoId == puesto)).Should().Be(1);
    }

    [Fact]
    public async Task Detecta_varios_puestos_fatigados_a_la_vez()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoMuyFatigado = await CrearPuestoAsync(ctx, lineaId: 4, horasEnPuesto: 1);
        var puestoSugerido = await CrearPuestoAsync(ctx, lineaId: 1, horasEnPuesto: 1);
        var puestoNormal = await CrearPuestoAsync(ctx, lineaId: 2, horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puestoMuyFatigado, lineaId: 4, usuario, minutosAtras: 300);
        await OcuparPuestoAsync(ctx, puestoSugerido, lineaId: 1, usuario, minutosAtras: 65);
        await OcuparPuestoAsync(ctx, puestoNormal, lineaId: 2, usuario, minutosAtras: 5);

        var abiertas = await DetectarFatigaAsync();

        abiertas.Should().Be(2);
    }

    // ═══ sp_MarcarRelevoSolicitado ═══

    [Fact]
    public async Task Marcar_manualmente_antes_del_umbral_igual_abre_la_solicitud_con_piso_sugerido()
    {
        // §9.4 p1, literal: "marcar manualmente [...] antes de llegar a ese umbral".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 5); // muy lejos del umbral real

        var resultado = await MarcarManualAsync(puesto, usuario);

        resultado.Codigo.Should().BeNull();
        var solicitud = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == resultado.SolicitudId);
        solicitud.Origen.Should().Be("manual_supervisor");
        solicitud.Nivel.Should().Be("sugerido", "CK_SR_nivel no admite 'normal' — el marcado manual nunca vale menos que 'sugerido'");
    }

    [Fact]
    public async Task Marcar_manualmente_cuando_ya_esta_critico_conserva_el_nivel_real()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, horasEnPuesto: 1, umbralCriticoHoras: 3); // umbral crítico: 180 min
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 300); // muy por encima, crítico

        var resultado = await MarcarManualAsync(puesto, usuario);

        var solicitud = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == resultado.SolicitudId);
        solicitud.Nivel.Should().Be("critico", "el nivel real no se degrada solo porque el disparo fue manual");
    }

    [Fact]
    public async Task No_se_puede_marcar_relevo_en_un_puesto_fijo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, tipo: "fijo");
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 5);

        var resultado = await MarcarManualAsync(puesto, usuario);

        resultado.Codigo.Should().Be("PUESTO_NO_ROTATIVO");
    }

    [Fact]
    public async Task No_se_puede_marcar_relevo_en_un_puesto_sin_ocupante()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);

        var resultado = await MarcarManualAsync(puesto, usuario);

        resultado.Codigo.Should().Be("PUESTO_SIN_OCUPANTE");
    }

    [Fact]
    public async Task No_se_puede_marcar_relevo_dos_veces_sobre_el_mismo_puesto()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 5);
        await MarcarManualAsync(puesto, usuario);

        var segundo = await MarcarManualAsync(puesto, usuario);

        segundo.Codigo.Should().Be("RELEVO_YA_SOLICITADO");
    }

    [Fact]
    public async Task Un_puesto_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var resultado = await MarcarManualAsync(puestoId: 999_999_999, usuario);

        resultado.Codigo.Should().Be("PUESTO_INEXISTENTE");
    }

    [Fact]
    public async Task Cinco_marcados_manuales_concurrentes_sobre_el_mismo_puesto_solo_uno_gana()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 5);

        var tareas = Enumerable.Range(0, 5).Select(_ => MarcarManualAsync(puesto, usuario));
        var resultados = await Task.WhenAll(tareas);

        resultados.Count(r => r.Codigo is null).Should().Be(1, "UPDLOCK+HOLDLOCK sobre Puesto serializa la contienda");
        resultados.Count(r => r.Codigo == "RELEVO_YA_SOLICITADO").Should().Be(4);
    }
}

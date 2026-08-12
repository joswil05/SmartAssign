using System.Data;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E12.3 (docs/PROGRESO.md): bandeja de salida transaccional (05 §4.1).
/// Prueba la garantía en el nivel donde de verdad vive — SQL Server, no
/// C#: <c>sp_EncolarEvento</c> nunca abre su propia transacción, así que
/// una transacción que revierte se lleva su fila de <c>EventoSaliente</c>
/// con ella. `sp_RegistrarParo`/`sp_ReanudarProduccion` (E11.1/E11.2)
/// son los productores reales de esta UT — mismo patrón exacto que
/// `SESSION_CONTEXT` que el resto de la suite, porque ambos leen
/// `JornadaLinea` (RLS).
/// </summary>
public class BandejaDeSalidaTransaccionalTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private async Task<SqlConnection> AbrirComoCoordinadorAsync()
    {
        var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
        return conexion;
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
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = "supervisor", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> JornadaAbiertaAsync(SmartAssignDbContext ctx, byte lineaId = 4)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    // ═══ Invocación de sp_RegistrarParo / sp_ReanudarProduccion ═══

    private record ResultadoRegistrar(int? ParoId, string? Codigo);

    private async Task<ResultadoRegistrar> RegistrarParoAsync(int jornadaLineaId, int usuarioId, string descripcion = "Paro de prueba, con detalle real.")
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RegistrarParo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@categoria_id", (short)1);
        cmd.Parameters.AddWithValue("@causa_id", (short)1);
        cmd.Parameters.AddWithValue("@descripcion", descripcion);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pParo = new SqlParameter("@paro_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pParo);
        cmd.Parameters.Add(new SqlParameter("@rotativos_liberados", SqlDbType.Int) { Direction = ParameterDirection.Output });
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoRegistrar(pParo.Value as int?, pCodigo.Value as string);
    }

    private async Task<string?> ReanudarProduccionAsync(int paroId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ReanudarProduccion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@paro_id", paroId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return pCodigo.Value as string;
    }

    [Fact]
    public async Task sp_EncolarEvento_inserta_la_fila_tal_cual()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_EncolarEvento";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@tipo_evento", "EventoDePrueba");
        cmd.Parameters.AddWithValue("@grupos", "linea:4,planta");
        cmd.Parameters.AddWithValue("@payload_json", "{\"X\":1}");
        await cmd.ExecuteNonQueryAsync();

        await using var ctx = CrearContexto();
        var evento = await ctx.EventosSalientes.AsNoTracking().SingleAsync();
        evento.TipoEvento.Should().Be("EventoDePrueba");
        evento.Grupos.Should().Be("linea:4,planta");
        evento.PayloadJson.Should().Be("{\"X\":1}");
        evento.ProcesadoEn.Should().BeNull();
    }

    [Fact]
    public async Task Si_la_transaccion_que_encola_revierte_la_fila_nunca_existio()
    {
        // 05 §4.1, literal: "garantiza que el evento se emite si y solo si
        // la transacción confirmó". sp_EncolarEvento no abre su propia
        // transacción — se une a la del llamador (mismo patrón que
        // sp_RegistrarAuditoria) — así que un ROLLBACK externo se la lleva.
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var tx = await conexion.BeginTransactionAsync())
        {
            await using (var cmd = conexion.CreateCommand())
            {
                cmd.Transaction = (SqlTransaction)tx;
                cmd.CommandText = "sp_EncolarEvento";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@tipo_evento", "EventoQueSeVaARevertir");
                cmd.Parameters.AddWithValue("@grupos", "planta");
                cmd.Parameters.AddWithValue("@payload_json", "{}");
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.RollbackAsync();
        }

        await using var ctx = CrearContexto();
        (await ctx.EventosSalientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Registrar_un_paro_encola_ParoIniciadoEvento_con_el_payload_correcto_en_la_misma_operacion()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);

        var resultado = await RegistrarParoAsync(jornada, usuario, "Rodillo trabado, mantenimiento avisado.");

        resultado.Codigo.Should().BeNull();

        var evento = await ctx.EventosSalientes.AsNoTracking().SingleAsync();
        evento.TipoEvento.Should().Be("ParoIniciadoEvento");
        evento.Grupos.Should().Be("linea:4,planta");
        evento.ProcesadoEn.Should().BeNull();

        using var payload = JsonDocument.Parse(evento.PayloadJson);
        payload.RootElement.GetProperty("ParoId").GetInt32().Should().Be(resultado.ParoId);
        payload.RootElement.GetProperty("LineaId").GetInt32().Should().Be(4);
        payload.RootElement.GetProperty("Categoria").GetString().Should().Be("Mecánico");
        payload.RootElement.GetProperty("Descripcion").GetString().Should().Be("Rodillo trabado, mantenimiento avisado.");
    }

    [Fact]
    public async Task Un_paro_rechazado_por_descripcion_vacia_no_encola_nada()
    {
        // El rechazo ocurre ANTES de BEGIN TRAN en sp_RegistrarParo — ni el
        // Paro ni el evento llegan a existir. Mismo criterio que la
        // garantía transaccional: nada a medias.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);

        var resultado = await RegistrarParoAsync(jornada, usuario, descripcion: "   ");

        resultado.Codigo.Should().Be("DESCRIPCION_OBLIGATORIA");
        (await ctx.EventosSalientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reanudar_produccion_encola_ParoReanudadoEvento_en_la_misma_operacion()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 6);
        var registro = await RegistrarParoAsync(jornada, usuario);

        var codigo = await ReanudarProduccionAsync(registro.ParoId!.Value, usuario);

        codigo.Should().BeNull();

        var eventoReanudado = await ctx.EventosSalientes.AsNoTracking()
            .SingleAsync(e => e.TipoEvento == "ParoReanudadoEvento");
        eventoReanudado.Grupos.Should().Be("linea:6,planta");

        using var payload = JsonDocument.Parse(eventoReanudado.PayloadJson);
        payload.RootElement.GetProperty("ParoId").GetInt32().Should().Be(registro.ParoId);
        payload.RootElement.GetProperty("LineaId").GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task Reanudar_un_paro_inexistente_no_encola_nada()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var codigo = await ReanudarProduccionAsync(999_999, usuario);

        codigo.Should().Be("PARO_INEXISTENTE");
        (await ctx.EventosSalientes.CountAsync()).Should().Be(0);
    }
}

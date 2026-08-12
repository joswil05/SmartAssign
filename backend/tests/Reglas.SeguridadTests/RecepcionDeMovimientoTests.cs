using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E8.3 (docs/PROGRESO.md): <c>sp_RecibirPersona</c> — paso 3 de
/// Parte X: "El supervisor destino confirma que llegó físicamente, y
/// solo entonces se le asigna el puesto" — esta UT prueba solo el cierre
/// del tránsito con <c>hora_llegada</c> real; la asignación posterior es
/// el flujo ya existente (E4.6/E6.7/E6.8), no algo que este SP haga.
/// Mismo patrón de base descartable que <see cref="DespachoDeMovimientoTests"/>.
/// </summary>
public class RecepcionDeMovimientoTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

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

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, byte lineaFisicaActual, string situacion = "presente_sin_asignar")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = "operario", LineaFisicaActual = lineaFisicaActual, Situacion = situacion,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
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

    // ═══ Invocación de los SP ═══

    private record ResultadoDespacho(long? MovimientoId, string? Codigo);
    private record ResultadoRecepcion(string? Codigo, string? Mensaje);

    private async Task<ResultadoDespacho> DespacharAsync(int personalId, byte lineaDestino, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DespacharPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_destino", lineaDestino);
        cmd.Parameters.AddWithValue("@motivo", "relevo");
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pId = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoDespacho(pId.Value as long?, pCodigo.Value as string);
    }

    private async Task<ResultadoRecepcion> RecibirAsync(long movimientoId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RecibirPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@movimiento_id", movimientoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoRecepcion(pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Recibir_a_alguien_en_transito_hacia_una_linea_normal_lo_deja_presente_sin_asignar()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario); // L1: línea normal, no Bolsón
        var antes = DateTime.UtcNow;

        var recepcion = await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        recepcion.Codigo.Should().BeNull();

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == despacho.MovimientoId);
        movimiento.Estado.Should().Be("recibido");
        movimiento.RecibidoPor.Should().Be(usuario);
        movimiento.HoraLlegada.Should().NotBeNull();
        movimiento.HoraLlegada!.Value.Should().BeOnOrAfter(antes.AddSeconds(-2));
        movimiento.HoraLlegada.Should().BeOnOrAfter(movimiento.HoraSalida, "§12.7: llegada nunca antes que la salida");

        var recibida = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona);
        recibida.Situacion.Should().Be("presente_sin_asignar", "L1 no es Bolsón — Parte VI: llegó, sala de espera, disponible");
        recibida.LineaFisicaActual.Should().Be((byte)1, "recién ahora llegó físicamente al destino, no en el despacho");
    }

    [Fact]
    public async Task Recibir_a_alguien_en_transito_hacia_el_Bolson_lo_deja_en_bolson()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 8, usuario); // L8: la única con es_bolson=1

        await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        var recibida = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona);
        recibida.Situacion.Should().Be("en_bolson", "L8 es Bolsón — Parte VI: ensamble manual, disponible");
        recibida.LineaFisicaActual.Should().Be((byte)8);
    }

    [Fact]
    public async Task La_duracion_del_transito_queda_calculada_y_persistida()
    {
        // duracion_seg (04 §5.2): columna calculada, la razón de ser de §12.7.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 8, usuario);

        await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == despacho.MovimientoId);
        movimiento.DuracionSeg.Should().NotBeNull().And.BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Un_movimiento_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var recepcion = await RecibirAsync(movimientoId: 999_999_999, usuario);

        recepcion.Codigo.Should().Be("MOVIMIENTO_INEXISTENTE");
    }

    [Fact]
    public async Task No_se_puede_recibir_dos_veces_el_mismo_movimiento()
    {
        // Doble toque de [CONFIRMAR] (fricción de red, C8) sobre la misma fila.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 8, usuario);
        await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        var segunda = await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        segunda.Codigo.Should().Be("MOVIMIENTO_NO_EN_TRANSITO");
    }

    [Fact]
    public async Task El_rol_de_aplicacion_no_puede_escribir_movimiento_directo_saltandose_el_sp()
    {
        // 04 §7.5 — mismo mecanismo de impersonación que sp_DespacharPersona (E8.1).
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 8, usuario);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = $"""
            EXECUTE AS USER = 'rol_app';
            UPDATE Movimiento SET estado = 'recibido', hora_llegada = SYSUTCDATETIME() WHERE Id = {despacho.MovimientoId};
            REVERT;
            """;

        var act = async () => await cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("UPDATE permission was denied");
    }

    [Fact]
    public async Task Dos_supervisores_confirmando_la_misma_recepcion_a_la_vez_solo_uno_gana()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 8, usuario);

        var tareas = Enumerable.Range(0, 5).Select(_ => RecibirAsync(despacho.MovimientoId!.Value, usuario));
        var resultados = await Task.WhenAll(tareas);

        resultados.Count(r => r.Codigo is null).Should().Be(1, "UPDLOCK+HOLDLOCK serializa el acceso a la misma fila");
        resultados.Count(r => r.Codigo == "MOVIMIENTO_NO_EN_TRANSITO").Should().Be(4);
    }
}

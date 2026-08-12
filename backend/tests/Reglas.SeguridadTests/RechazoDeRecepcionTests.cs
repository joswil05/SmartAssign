using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E8.4 (docs/PROGRESO.md): <c>sp_RechazarRecepcion</c> — la otra
/// salida del paso 3 de Parte X, literal 00 §C10. Mismo patrón de base
/// descartable que <see cref="DespachoDeMovimientoTests"/>/
/// <see cref="RecepcionDeMovimientoTests"/>.
/// </summary>
public class RechazoDeRecepcionTests : IAsyncLifetime
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
    private record ResultadoRechazo(string? Codigo, string? Mensaje);

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

    private async Task<ResultadoRechazo> RechazarAsync(long movimientoId, int usuarioId, short? motivoRechazoId, string? notaRechazo = null)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RechazarRecepcion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@movimiento_id", movimientoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@motivo_rechazo_id", (object?)motivoRechazoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@nota_rechazo", (object?)notaRechazo ?? DBNull.Value);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoRechazo(pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Rechazar_la_recepcion_abre_un_nuevo_transito_hacia_el_Bolson_no_en_bolson_directo()
    {
        // 00 §C10 p1, literal: "en tránsito hacia L8, no directamente En Bolsón".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario); // L1: rechaza desde ahí

        var rechazo = await RechazarAsync(despacho.MovimientoId!.Value, usuario, motivoRechazoId: 2 /* Persona incorrecta */);

        rechazo.Codigo.Should().BeNull();

        var persistida = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona);
        persistida.Situacion.Should().Be("en_transito", "el segundo trayecto también es un tránsito real, no llega directo al Bolsón");
        persistida.LineaFisicaActual.Should().Be((byte)4, "nunca llegó de verdad a L1 — no se toca hasta una recepción real");

        var nuevoTransito = await ctx.Movimientos.AsNoTracking()
            .Where(m => m.PersonalId == persona && m.Estado == "en_transito").SingleAsync();
        nuevoTransito.LineaOrigen.Should().Be((byte)1, "está físicamente parada en la línea que la rechazó");
        nuevoTransito.LineaDestino.Should().Be((byte)8, "L8 es la única línea con es_bolson=1");
        nuevoTransito.Motivo.Should().Be("rechazo_recepcion");
        nuevoTransito.DespachadoPor.Should().Be(usuario);
    }

    [Fact]
    public async Task El_movimiento_original_queda_rechazado_con_motivo_y_nota()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario);

        await RechazarAsync(despacho.MovimientoId!.Value, usuario, motivoRechazoId: 2, notaRechazo: "No era la persona esperada");

        var original = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == despacho.MovimientoId);
        original.Estado.Should().Be("rechazado");
        original.MotivoRechazoId.Should().Be((short)2);
        original.NotaRechazo.Should().Be("No era la persona esperada");
        original.HoraLlegada.Should().BeNull("nunca fue recibida — fue rechazada");
    }

    [Fact]
    public async Task El_rechazo_sin_motivo_se_deniega()
    {
        // 00 §C10 p4: "el rechazo exige motivo" — nunca un canal silencioso.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario);

        var rechazo = await RechazarAsync(despacho.MovimientoId!.Value, usuario, motivoRechazoId: null);

        rechazo.Codigo.Should().Be("MOTIVO_RECHAZO_OBLIGATORIO");

        var original = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == despacho.MovimientoId);
        original.Estado.Should().Be("en_transito", "un rechazo sin motivo no debe alterar nada");
    }

    [Fact]
    public async Task Un_motivo_de_rechazo_inexistente_se_deniega()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario);

        var rechazo = await RechazarAsync(despacho.MovimientoId!.Value, usuario, motivoRechazoId: 999);

        rechazo.Codigo.Should().Be("MOTIVO_RECHAZO_INVALIDO");
    }

    [Fact]
    public async Task Un_movimiento_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var rechazo = await RechazarAsync(movimientoId: 999_999_999, usuario, motivoRechazoId: 1);

        rechazo.Codigo.Should().Be("MOVIMIENTO_INEXISTENTE");
    }

    [Fact]
    public async Task No_se_puede_rechazar_un_movimiento_ya_recibido()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario);

        await using (var conexion = new SqlConnection(CadenaConexion))
        {
            await conexion.OpenAsync();
            await using var cmd = conexion.CreateCommand();
            cmd.CommandText = "sp_RecibirPersona";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@movimiento_id", despacho.MovimientoId!.Value);
            cmd.Parameters.AddWithValue("@usuario_id", usuario);
            cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
            await cmd.ExecuteNonQueryAsync();
        }

        var rechazo = await RechazarAsync(despacho.MovimientoId!.Value, usuario, motivoRechazoId: 1);

        rechazo.Codigo.Should().Be("MOVIMIENTO_NO_EN_TRANSITO");
    }

    [Fact]
    public async Task El_rechazo_queda_auditado()
    {
        // 00 §C10 p4: "Queda auditado" — la única pieza explícita de auditoría de Parte X hasta ahora.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario);

        await RechazarAsync(despacho.MovimientoId!.Value, usuario, motivoRechazoId: 2);

        var auditoria = await ctx.Auditorias.AsNoTracking()
            .SingleAsync(a => a.Entidad == "Movimiento" && a.EntidadId == despacho.MovimientoId);
        auditoria.Accion.Should().Be("RECHAZAR_RECEPCION");
        auditoria.Resultado.Should().Be("OK");
        auditoria.PersonalId.Should().Be(persona);
        auditoria.UsuarioId.Should().Be(usuario);
    }

    [Fact]
    public async Task El_rol_de_aplicacion_no_puede_escribir_movimiento_directo_saltandose_el_sp()
    {
        // 04 §7.5 — mismo mecanismo de impersonación que el resto de la familia.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 1, usuario);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = $"""
            EXECUTE AS USER = 'rol_app';
            UPDATE Movimiento SET estado = 'rechazado', motivo_rechazo_id = 1 WHERE Id = {despacho.MovimientoId};
            REVERT;
            """;

        var act = async () => await cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("UPDATE permission was denied");
    }
}

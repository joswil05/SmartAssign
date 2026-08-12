using System.Data;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E12.4 (docs/PROGRESO.md): "FCM campana vacía" (D5, 05 §2.5).
/// Prueba en el nivel donde vive la garantía transaccional — SQL Server,
/// no C#: <c>sp_EncolarNotificacion</c> nunca abre su propia transacción,
/// así que una transacción que revierte se lleva su fila de
/// <c>Notificacion</c> con ella. <c>sp_DespacharPersona</c> (E8.1) es el
/// productor real de esta UT.
/// </summary>
public class CampanaVaciaFcmTests : IAsyncLifetime
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

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx, string rol = "supervisor")
    {
        var u = new Usuario
        {
            Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba",
            Rol = rol, OrigenIdentidad = "local", Activo = true,
        };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> CrearPersonaAsync(
        SmartAssignDbContext ctx, byte? lineaFisicaActual, string situacion, string nombreCompleto = "María López")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = nombreCompleto,
            Categoria = "operario", LineaFisicaActual = lineaFisicaActual, Situacion = situacion,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task AsignarSupervisorAsync(SmartAssignDbContext ctx, byte lineaId, int usuarioId)
    {
        var linea = await ctx.Lineas.SingleAsync(l => l.Id == lineaId);
        linea.SupervisorActualId = usuarioId;
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación de sp_DespacharPersona ═══

    private record ResultadoDespacho(long? MovimientoId, string? Codigo);

    private async Task<ResultadoDespacho> DespacharAsync(int personalId, byte lineaDestino, string motivo, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DespacharPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_destino", lineaDestino);
        cmd.Parameters.AddWithValue("@motivo", motivo);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pId = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoDespacho(pId.Value as long?, pCodigo.Value as string);
    }

    [Fact]
    public async Task sp_EncolarNotificacion_inserta_la_fila_tal_cual()
    {
        await using var ctx = CrearContexto();
        var destinatario = await CrearUsuarioAsync(ctx);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_EncolarNotificacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@usuario_id", destinatario);
        cmd.Parameters.AddWithValue("@tipo", "PruebaDeNotificacion");
        cmd.Parameters.AddWithValue("@titulo", "Título de prueba");
        cmd.Parameters.AddWithValue("@cuerpo", "Cuerpo de prueba.");
        cmd.Parameters.AddWithValue("@payload_json", "{\"X\":1}");
        var pId = new SqlParameter("@notificacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        await cmd.ExecuteNonQueryAsync();

        var notificacion = await ctx.Notificaciones.AsNoTracking().SingleAsync(n => n.Id == (long)pId.Value);
        notificacion.UsuarioId.Should().Be(destinatario);
        notificacion.Tipo.Should().Be("PruebaDeNotificacion");
        notificacion.Criticidad.Should().Be("normal", "el default (04 §10) aplica cuando no se pasa @criticidad");
        notificacion.Titulo.Should().Be("Título de prueba");
        notificacion.Cuerpo.Should().Be("Cuerpo de prueba.");
        notificacion.PayloadJson.Should().Be("{\"X\":1}");
        notificacion.EntregadaEn.Should().BeNull("el envío de verdad es responsabilidad de NotificacionDispatcher, no de este SP");
    }

    [Fact]
    public async Task Si_la_transaccion_que_encola_revierte_la_fila_nunca_existio()
    {
        // 05 §4.1, mismo criterio que sp_EncolarEvento (E12.3):
        // sp_EncolarNotificacion no abre su propia transacción — se une a
        // la del llamador — así que un ROLLBACK externo se la lleva.
        await using var ctx = CrearContexto();
        var destinatario = await CrearUsuarioAsync(ctx);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var tx = await conexion.BeginTransactionAsync())
        {
            await using (var cmd = conexion.CreateCommand())
            {
                cmd.Transaction = (SqlTransaction)tx;
                cmd.CommandText = "sp_EncolarNotificacion";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@usuario_id", destinatario);
                cmd.Parameters.AddWithValue("@tipo", "SeVaARevertir");
                cmd.Parameters.AddWithValue("@titulo", "X");
                cmd.Parameters.AddWithValue("@cuerpo", "Y");
                var pId = new SqlParameter("@notificacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(pId);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.RollbackAsync();
        }

        (await ctx.Notificaciones.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CK_Notif_criticidad_rechaza_un_valor_fuera_del_catalogo()
    {
        await using var ctx = CrearContexto();
        var destinatario = await CrearUsuarioAsync(ctx);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_EncolarNotificacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@usuario_id", destinatario);
        cmd.Parameters.AddWithValue("@tipo", "X");
        cmd.Parameters.AddWithValue("@titulo", "X");
        cmd.Parameters.AddWithValue("@cuerpo", "Y");
        cmd.Parameters.AddWithValue("@criticidad", "urgente");
        cmd.Parameters.Add(new SqlParameter("@notificacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output });

        var act = async () => await cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("CK_Notif_criticidad");
    }

    [Fact]
    public async Task Despachar_hacia_una_linea_con_supervisor_encola_una_notificacion_de_transito_entrante_para_ese_supervisor()
    {
        await using var ctx = CrearContexto();
        var coordinador = await CrearUsuarioAsync(ctx, "coordinador");
        var supervisorDestino = await CrearUsuarioAsync(ctx, "supervisor");
        await AsignarSupervisorAsync(ctx, lineaId: 4, supervisorDestino);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 2, situacion: "presente_sin_asignar", nombreCompleto: "María López");

        var resultado = await DespacharAsync(persona, lineaDestino: 4, motivo: "relevo", coordinador);

        resultado.Codigo.Should().BeNull();

        var notificacion = await ctx.Notificaciones.AsNoTracking().SingleAsync();
        notificacion.UsuarioId.Should().Be(supervisorDestino, "00 §D1: el destino sí ve quién viene a relevar");
        notificacion.Tipo.Should().Be("TransitoEntrante");
        notificacion.Criticidad.Should().Be("normal");
        notificacion.Cuerpo.Should().Contain("María López").And.Contain("L2");
        notificacion.EntregadaEn.Should().BeNull();

        using var payload = JsonDocument.Parse(notificacion.PayloadJson!);
        payload.RootElement.GetProperty("MovimientoId").GetInt64().Should().Be(resultado.MovimientoId!.Value);
        payload.RootElement.GetProperty("PersonalId").GetInt32().Should().Be(persona);
        payload.RootElement.GetProperty("NombreCompleto").GetString().Should().Be("María López");
        payload.RootElement.GetProperty("LineaOrigen").GetInt32().Should().Be(2);
        payload.RootElement.GetProperty("LineaDestino").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task Despachar_hacia_una_linea_sin_supervisor_asignado_no_notifica_a_nadie()
    {
        // Honestidad del dato (§12.4): sin destinatario real, no se
        // inventa uno — mismo criterio que un umbral sin configurar.
        await using var ctx = CrearContexto();
        var coordinador = await CrearUsuarioAsync(ctx, "coordinador");
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 2, situacion: "presente_sin_asignar");

        var resultado = await DespacharAsync(persona, lineaDestino: 6, motivo: "relevo", coordinador);

        resultado.Codigo.Should().BeNull();
        (await ctx.Notificaciones.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_despacho_rechazado_por_falta_de_ubicacion_fisica_no_encola_ninguna_notificacion()
    {
        await using var ctx = CrearContexto();
        var coordinador = await CrearUsuarioAsync(ctx, "coordinador");
        var supervisorDestino = await CrearUsuarioAsync(ctx, "supervisor");
        await AsignarSupervisorAsync(ctx, lineaId: 4, supervisorDestino);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: null, situacion: "presente_sin_asignar");

        var resultado = await DespacharAsync(persona, lineaDestino: 4, motivo: "relevo", coordinador);

        resultado.Codigo.Should().Be("SIN_LINEA_FISICA");
        (await ctx.Notificaciones.CountAsync()).Should().Be(0);
    }
}

using System.Data;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E12.6 (docs/PROGRESO.md): "Acuse, escalado y 'supervisor no
/// localizable'" (D5, 04 §10) — cierra E12 (6/6) → F10. Prueba
/// <c>sp_EscalarNotificacionesVencidas</c> en el nivel donde vive: SQL
/// Server, con notificaciones encoladas directamente (mismo criterio que
/// <c>BandejaDeSalidaTransaccionalTests</c>, E12.3, probando
/// <c>sp_EncolarEvento</c> con un evento sintético) — ningún productor
/// real de esta sesión emite todavía <c>criticidad='critica'</c>.
/// </summary>
public class AcuseEscaladoYSupervisorNoLocalizableTests : IAsyncLifetime
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

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx)
    {
        var u = new Usuario
        {
            Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba",
            Rol = "supervisor", OrigenIdentidad = "local", Activo = true,
        };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task SembrarTimeoutAsync(SmartAssignDbContext ctx, int minutos)
    {
        ctx.Parametros.Add(new Parametro
        {
            Clave = "notificacion_acuse_timeout_min",
            Valor = minutos.ToString(CultureInfo.InvariantCulture),
            Tipo = "int",
            Descripcion = "prueba",
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Encola una Notificacion directamente y permite fijar creada_en en el pasado (simula "vieja sin acuse").</summary>
    private async Task<long> EncolarNotificacionAsync(
        int usuarioId, string criticidad, DateTime? creadaEnPasado = null, bool acusada = false)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_EncolarNotificacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@tipo", "PruebaDeEscalado");
        cmd.Parameters.AddWithValue("@criticidad", criticidad);
        cmd.Parameters.AddWithValue("@titulo", "Título de prueba");
        cmd.Parameters.AddWithValue("@cuerpo", "Cuerpo de prueba.");
        var pId = new SqlParameter("@notificacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        await cmd.ExecuteNonQueryAsync();
        var id = (long)pId.Value;

        if (creadaEnPasado is { } fecha)
        {
            await using var cmdFecha = conexion.CreateCommand();
            cmdFecha.CommandText = "UPDATE Notificacion SET creada_en = @fecha WHERE Id = @id";
            cmdFecha.Parameters.AddWithValue("@fecha", fecha);
            cmdFecha.Parameters.AddWithValue("@id", id);
            await cmdFecha.ExecuteNonQueryAsync();
        }

        if (acusada)
        {
            await using var cmdAcuse = conexion.CreateCommand();
            cmdAcuse.CommandText = "UPDATE Notificacion SET acusada_en = SYSUTCDATETIME() WHERE Id = @id";
            cmdAcuse.Parameters.AddWithValue("@id", id);
            await cmdAcuse.ExecuteNonQueryAsync();
        }

        return id;
    }

    private async Task<int> EscalarVencidasAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_EscalarNotificacionesVencidas";
        cmd.CommandType = CommandType.StoredProcedure;
        var pEscaladas = new SqlParameter("@escaladas", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pEscaladas);
        await cmd.ExecuteNonQueryAsync();
        return (int)pEscaladas.Value;
    }

    [Fact]
    public async Task Sin_el_parametro_de_timeout_configurado_no_escala_nada()
    {
        // Honestidad del dato (§12.4): "el tiempo configurado" de D5 no
        // existe sin sembrar — no se inventa un número razonable.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        await EncolarNotificacionAsync(usuario, "critica", creadaEnPasado: DateTime.UtcNow.AddDays(-1));

        var escaladas = await EscalarVencidasAsync();

        escaladas.Should().Be(0);
        (await ctx.Notificaciones.AsNoTracking().SingleAsync()).EscaladaEn.Should().BeNull();
    }

    [Fact]
    public async Task Una_notificacion_critica_vieja_sin_acuse_escala_y_avisa_al_coordinador()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        await SembrarTimeoutAsync(ctx, minutos: 10);
        var id = await EncolarNotificacionAsync(usuario, "critica", creadaEnPasado: DateTime.UtcNow.AddMinutes(-20));

        var escaladas = await EscalarVencidasAsync();

        escaladas.Should().Be(1);

        var notificacion = await ctx.Notificaciones.AsNoTracking().SingleAsync(n => n.Id == id);
        notificacion.EscaladaEn.Should().NotBeNull();

        // D5, literal: "escala al Coordinador y aparece en su panel como
        // 'supervisor no localizable'" — vía la misma bandeja de salida
        // transaccional de E12.3.
        var evento = await ctx.EventosSalientes.AsNoTracking().SingleAsync();
        evento.TipoEvento.Should().Be("AlertaCoordinadorEvento");
        evento.Grupos.Should().Be("planta");

        using var payload = JsonDocument.Parse(evento.PayloadJson);
        payload.RootElement.GetProperty("NotificacionId").GetInt64().Should().Be(id);
        payload.RootElement.GetProperty("Mensaje").GetString().Should().Contain("no localizable");
    }

    [Fact]
    public async Task Una_notificacion_critica_reciente_todavia_no_escala()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        await SembrarTimeoutAsync(ctx, minutos: 10);
        await EncolarNotificacionAsync(usuario, "critica", creadaEnPasado: DateTime.UtcNow.AddMinutes(-2));

        var escaladas = await EscalarVencidasAsync();

        escaladas.Should().Be(0);
        (await ctx.Notificaciones.AsNoTracking().SingleAsync()).EscaladaEn.Should().BeNull();
    }

    [Fact]
    public async Task Una_notificacion_normal_nunca_escala_aunque_este_vieja()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        await SembrarTimeoutAsync(ctx, minutos: 10);
        await EncolarNotificacionAsync(usuario, "normal", creadaEnPasado: DateTime.UtcNow.AddDays(-1));

        var escaladas = await EscalarVencidasAsync();

        escaladas.Should().Be(0);
        (await ctx.EventosSalientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_notificacion_ya_acusada_no_escala_aunque_este_vieja()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        await SembrarTimeoutAsync(ctx, minutos: 10);
        await EncolarNotificacionAsync(usuario, "critica", creadaEnPasado: DateTime.UtcNow.AddDays(-1), acusada: true);

        var escaladas = await EscalarVencidasAsync();

        escaladas.Should().Be(0);
    }

    [Fact]
    public async Task Una_notificacion_ya_escalada_no_se_vuelve_a_escalar()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        await SembrarTimeoutAsync(ctx, minutos: 10);
        await EncolarNotificacionAsync(usuario, "critica", creadaEnPasado: DateTime.UtcNow.AddMinutes(-20));
        await EscalarVencidasAsync();

        var segundaPasada = await EscalarVencidasAsync();

        segundaPasada.Should().Be(0, "ya se escaló una vez — no debe avisar al Coordinador dos veces por lo mismo");
        (await ctx.EventosSalientes.CountAsync()).Should().Be(1);
    }
}

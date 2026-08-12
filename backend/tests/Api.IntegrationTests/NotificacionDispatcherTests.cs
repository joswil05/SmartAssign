using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Api.Notificaciones;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E12.4 (docs/PROGRESO.md): la prueba de punta a punta de "FCM como
/// campana vacía" (D5, 05 §2.5) — <c>sp_DespacharPersona</c> (SQL real)
/// → <c>Notificacion</c> → <c>NotificacionDispatcher</c> (ya corriendo
/// solo, es un <c>IHostedService</c> que <see cref="SmartAssignApiFactory"/>
/// arranca con el resto del host) → <see cref="ServicioNotificacionesPushDeCaptura"/>
/// (reemplaza a Firebase, sin credenciales en CI) → <c>GET /api/notificaciones/{id}</c>
/// con JWT real. Nada de esto se invoca a mano: es la prueba de que el
/// mecanismo completo compone, no solo sus piezas por separado.
/// </summary>
public class NotificacionDispatcherTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private async Task<(int usuarioId, string username, string password)> CrearUsuarioAsync(string rol)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var credenciales = new ServicioCredenciales();
        var password = $"Clave#{Guid.NewGuid():N}"[..16];
        var (hash, salt) = credenciales.HashConSal(password);
        var username = $"u_{Guid.NewGuid():N}"[..20];

        var usuario = new Usuario
        {
            Username = username, NombreCompleto = username, Rol = rol, OrigenIdentidad = "local",
            PasswordHash = hash, PasswordSalt = salt, Activo = true,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return (usuario.Id, username, password);
    }

    private async Task AsignarSupervisorAsync(byte lineaId, int usuarioId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var linea = await db.Lineas.SingleAsync(l => l.Id == lineaId);
        linea.SupervisorActualId = usuarioId;
        await db.SaveChangesAsync();
    }

    private async Task<int> CrearPersonaAsync(byte? lineaFisicaActual, string situacion, string nombreCompleto)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = nombreCompleto,
            Categoria = "operario", LineaFisicaActual = lineaFisicaActual, Situacion = situacion,
        };
        db.Personas.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username, password, deviceId = $"device-{Guid.NewGuid():N}" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private HttpClient ClienteAutenticado(string token)
    {
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    /// <summary>Llama a sp_DespacharPersona tal cual lo llamaría la Api — SQL real, no un atajo de EF.</summary>
    private async Task<long> DespacharAsync(int personalId, byte lineaDestino, int usuarioId)
    {
        await using var conexion = new SqlConnection(factory.CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DespacharPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_destino", lineaDestino);
        cmd.Parameters.AddWithValue("@motivo", "relevo");
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pId = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return (long)pId.Value;
    }

    private static async Task<Notificacion?> EsperarNotificacionEntregadaAsync(SmartAssignDbContext db, int usuarioId)
    {
        // El dispatcher sondea cada 1 s (NotificacionDispatcher) — hasta
        // ~25 s de margen real para LocalDB bajo la carga de la suite
        // completa corriendo en paralelo, mismo criterio que
        // EventoSalienteDispatcherTests (E12.3).
        for (var i = 0; i < 25; i++)
        {
            var notificacion = await db.Notificaciones.AsNoTracking()
                .SingleOrDefaultAsync(n => n.UsuarioId == usuarioId && n.EntregadaEn != null);
            if (notificacion is not null) return notificacion;
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        return null;
    }

    [Fact]
    public async Task Un_transito_real_encola_notifica_y_se_descarga_el_contenido_real_de_punta_a_punta()
    {
        var (supervisorId, supervisorUser, supervisorPass) = await CrearUsuarioAsync("supervisor");
        var (coordinadorId, _, _) = await CrearUsuarioAsync("coordinador");
        await AsignarSupervisorAsync(lineaId: 4, supervisorId);
        var persona = await CrearPersonaAsync(lineaFisicaActual: 2, situacion: "presente_sin_asignar", nombreCompleto: "María López");

        var tokenSupervisor = await LoginAsync(supervisorUser, supervisorPass);
        using var clienteSupervisor = ClienteAutenticado(tokenSupervisor);

        // Paso 0 — el teléfono se registra para recibir la campana vacía
        // (05 §2.5, POST /dispositivos/push-token).
        var registro = await clienteSupervisor.PostAsJsonAsync("/api/dispositivos/push-token",
            new { deviceId = $"device-{Guid.NewGuid():N}", pushToken = $"token-{Guid.NewGuid():N}" });
        registro.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Paso 1 — ocurre el evento de negocio real.
        var movimientoId = await DespacharAsync(persona, lineaDestino: 4, coordinadorId);

        // Paso 2 — NotificacionDispatcher la entrega sola (vía el doble de
        // captura, sin credenciales de Firebase en CI).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var notificacion = await EsperarNotificacionEntregadaAsync(db, supervisorId);
        notificacion.Should().NotBeNull("sp_DespacharPersona → Notificacion → NotificacionDispatcher debió entregarla sola");

        // Paso 3 — el "ping" que de verdad salió no lleva nada de negocio.
        var captura = (ServicioNotificacionesPushDeCaptura)factory.Services.GetRequiredService<IServicioNotificacionesPush>();
        captura.Enviados.Should().Contain(e => e.Ping.E == notificacion!.Id.ToString());
        var pingEnviado = captura.Enviados.Single(e => e.Ping.E == notificacion!.Id.ToString());
        JsonSerializer.Serialize(pingEnviado.Ping).Should().Be($$"""{"E":"{{notificacion!.Id}}"}""",
            "D5: el ping SOLO trae el id opaco — nunca nombre, ficha, línea ni puesto");

        // Paso 4 — la app despierta y descarga el CONTENIDO REAL por HTTPS con JWT.
        var descarga = await clienteSupervisor.GetAsync($"/api/notificaciones/{notificacion!.Id}");
        descarga.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await descarga.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("tipo").GetString().Should().Be("TransitoEntrante");
        cuerpo.GetProperty("cuerpo").GetString().Should().Contain("María López");
        cuerpo.GetProperty("payload").GetProperty("MovimientoId").GetInt64().Should().Be(movimientoId);
    }

    [Fact]
    public async Task Un_usuario_no_puede_descargar_el_contenido_de_una_notificacion_ajena()
    {
        var (supervisorId, _, _) = await CrearUsuarioAsync("supervisor");
        var (otroId, otroUser, otroPass) = await CrearUsuarioAsync("supervisor");
        var (coordinadorId, _, _) = await CrearUsuarioAsync("coordinador");
        await AsignarSupervisorAsync(lineaId: 6, supervisorId);
        var persona = await CrearPersonaAsync(lineaFisicaActual: 1, situacion: "presente_sin_asignar", nombreCompleto: "Ana Ruiz");

        await DespacharAsync(persona, lineaDestino: 6, coordinadorId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var notificacion = await db.Notificaciones.AsNoTracking().SingleAsync(n => n.UsuarioId == supervisorId);

        var tokenOtro = await LoginAsync(otroUser, otroPass);
        using var clienteOtro = ClienteAutenticado(tokenOtro);

        var respuesta = await clienteOtro.GetAsync($"/api/notificaciones/{notificacion.Id}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

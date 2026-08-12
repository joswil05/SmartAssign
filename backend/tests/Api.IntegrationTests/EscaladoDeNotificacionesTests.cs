using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E12.6 (docs/PROGRESO.md): la prueba de punta a punta del escalado
/// — <c>sp_EncolarNotificacion</c> (crítica, sin acuse) →
/// <c>EscaladoDeNotificacionesDispatcher</c> (ya corriendo solo) →
/// <c>sp_EscalarNotificacionesVencidas</c> → <c>sp_EncolarEvento</c>
/// (misma bandeja de E12.3) → <c>EventoSalienteDispatcher</c> →
/// <c>PlantaHub</c> → un <see cref="HubConnection"/> real del
/// Coordinador. Clase con su PROPIA <see cref="SmartAssignApiFactory"/>
/// (base aislada) porque siembra <c>notificacion_acuse_timeout_min = 0</c>
/// — cualquier notificación crítica sin acuse de esta base escala casi
/// de inmediato, así que no puede compartir base con otras pruebas que
/// no esperan ese comportamiento.
/// </summary>
public class EscaladoDeNotificacionesTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private const string EVENTO = "AlertaCoordinadorEvento";

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

    private async Task SembrarTimeoutCeroAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        db.Parametros.Add(new Parametro
        {
            Clave = "notificacion_acuse_timeout_min", Valor = "0",
            Tipo = "int", Descripcion = "prueba",
        });
        await db.SaveChangesAsync();
    }

    private async Task<long> EncolarNotificacionCriticaAsync(int usuarioId)
    {
        await using var conexion = new SqlConnection(factory.CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_EncolarNotificacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@tipo", "PruebaDeEscaladoE2E");
        cmd.Parameters.AddWithValue("@criticidad", "critica");
        cmd.Parameters.AddWithValue("@titulo", "Relevista en camino");
        cmd.Parameters.AddWithValue("@cuerpo", "Cuerpo de prueba.");
        var pId = new SqlParameter("@notificacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        await cmd.ExecuteNonQueryAsync();
        return (long)pId.Value;
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

    private HubConnection ConectarHub(string token) =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hub/planta", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private static ChannelReader<JsonElement> Escuchar(HubConnection conexion, string evento)
    {
        var canal = Channel.CreateUnbounded<JsonElement>();
        conexion.On<JsonElement>(evento, mensaje => canal.Writer.TryWrite(mensaje));
        return canal.Reader;
    }

    private static async Task<JsonElement?> LeerConTimeoutAsync(ChannelReader<JsonElement> lector, TimeSpan espera)
    {
        using var cts = new CancellationTokenSource(espera);
        try
        {
            return await lector.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    [Fact]
    public async Task Una_notificacion_critica_sin_acuse_escala_solo_y_llega_al_Coordinador_como_supervisor_no_localizable()
    {
        await SembrarTimeoutCeroAsync();
        var (destinatarioId, _, _) = await CrearUsuarioAsync("supervisor");
        var (_, coordUser, coordPass) = await CrearUsuarioAsync("coordinador");
        var tokenCoordinador = await LoginAsync(coordUser, coordPass);

        await using var conexion = ConectarHub(tokenCoordinador);
        var lector = Escuchar(conexion, EVENTO);
        await conexion.StartAsync();

        var notificacionId = await EncolarNotificacionCriticaAsync(destinatarioId);

        // Dos dispatchers en cadena (EscaladoDeNotificacionesDispatcher →
        // EventoSalienteDispatcher), cada uno sondea cada 1 s — margen real
        // más generoso para la suite completa corriendo en paralelo.
        JsonElement? recibido = null;
        for (var i = 0; i < 30 && recibido is null; i++)
            recibido = await LeerConTimeoutAsync(lector, TimeSpan.FromSeconds(1));

        recibido.Should().NotBeNull(
            "sp_EncolarNotificacion → EscaladoDeNotificacionesDispatcher → sp_EscalarNotificacionesVencidas → sp_EncolarEvento → EventoSalienteDispatcher → PlantaHub debió entregarlo solo");
        recibido!.Value.GetProperty("NotificacionId").GetInt64().Should().Be(notificacionId);
        recibido.Value.GetProperty("Mensaje").GetString().Should().Contain("no localizable");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        (await db.Notificaciones.AsNoTracking().SingleAsync(n => n.Id == notificacionId)).EscaladaEn.Should().NotBeNull();
    }
}

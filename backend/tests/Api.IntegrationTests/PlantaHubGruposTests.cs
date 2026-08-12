using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Api.Hubs;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E12.1 (docs/PROGRESO.md): Hub SignalR <c>/hub/planta</c> con grupos
/// asignados por el servidor (05 §2.4, §2.2). Contra la Api real
/// (<see cref="SmartAssignApiFactory"/>) con un <see cref="HubConnection"/>
/// real de <c>Microsoft.AspNetCore.SignalR.Client</c> — nunca un doble
/// del framework: lo que se prueba es justo la membresía de grupo que
/// SignalR resuelve en el transporte real, no una simulación de ella.
/// </summary>
public class PlantaHubGruposTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private const string EVENTO_DE_PRUEBA = "EventoDePrueba";
    private static readonly TimeSpan ESPERA_AUSENCIA = TimeSpan.FromSeconds(1);

    private async Task<(int usuarioId, string username, string password)> CrearUsuarioAsync(
        string rol, string prefijoUsername, byte? lineaSupervisada = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var credenciales = new ServicioCredenciales();
        var password = $"Clave#{Guid.NewGuid():N}"[..16];
        var (hash, salt) = credenciales.HashConSal(password);
        var username = $"{prefijoUsername}_{Guid.NewGuid():N}"[..30];

        var usuario = new Usuario
        {
            Username = username, NombreCompleto = username, Rol = rol, OrigenIdentidad = "local",
            PasswordHash = hash, PasswordSalt = salt, Activo = true,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        if (lineaSupervisada is { } lineaId)
        {
            var linea = await db.Lineas.SingleAsync(l => l.Id == lineaId);
            linea.SupervisorActualId = usuario.Id;
            await db.SaveChangesAsync();
        }

        return (usuario.Id, username, password);
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

    /// <summary>
    /// El token viaja por querystring (`access_token`) — es la única vía
    /// que WebSocket/SSE permiten, no el header Authorization (Program.cs,
    /// `OnMessageReceived`). `LongPolling` porque el `TestServer` de
    /// `WebApplicationFactory` no soporta WebSockets reales.
    /// </summary>
    private HubConnection ConectarHub(string? token) =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hub/planta", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private static ChannelReader<string> Escuchar(HubConnection conexion)
    {
        var canal = Channel.CreateUnbounded<string>();
        conexion.On<string>(EVENTO_DE_PRUEBA, mensaje => canal.Writer.TryWrite(mensaje));
        return canal.Reader;
    }

    /// <summary>NULL si no llegó nada dentro de <paramref name="espera"/> — nunca lanza por timeout.</summary>
    private static async Task<string?> LeerConTimeoutAsync(ChannelReader<string> lector, TimeSpan espera)
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

    private async Task PublicarEnGrupoAsync(string grupo, string mensaje)
    {
        using var scope = factory.Services.CreateScope();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<PlantaHub>>();
        await hub.Clients.Group(grupo).SendAsync(EVENTO_DE_PRUEBA, mensaje);
    }

    /// <summary>
    /// `HubConnection.StartAsync()` en el cliente puede volver antes de que
    /// `PlantaHub.OnConnectedAsync()` termine de verdad en el servidor —
    /// esa parte hace varias idas a la base antes de unir al grupo de
    /// línea/bolsón, y no hay ningún método invocable por el cliente (a
    /// propósito, ver docstring del Hub) contra el que sincronizar. Reintentar
    /// la publicación es la forma de esperar sin tocar la superficie pública
    /// del Hub — nunca por una carrera real de membresía.
    /// </summary>
    private async Task<string?> PublicarYEsperarAsync(string grupo, string mensaje, ChannelReader<string> lector, int intentos = 8)
    {
        for (var i = 0; i < intentos; i++)
        {
            await PublicarEnGrupoAsync(grupo, mensaje);
            var recibido = await LeerConTimeoutAsync(lector, TimeSpan.FromMilliseconds(400));
            if (recibido is not null) return recibido;
        }
        return null;
    }

    [Fact]
    public async Task Un_supervisor_recibe_lo_publicado_en_el_grupo_de_su_propia_linea()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup4", lineaSupervisada: 4);
        var token = await LoginAsync(username, password);

        await using var conexion = ConectarHub(token);
        var lector = Escuchar(conexion);
        await conexion.StartAsync();

        (await PublicarYEsperarAsync("linea:4", "hola L4", lector)).Should().Be("hola L4");
    }

    [Fact]
    public async Task Un_supervisor_no_recibe_lo_publicado_en_la_linea_de_otro_supervisor()
    {
        // 05 §2.4: "el aislamiento se aplica al suscribirse" — mismo §2.2 que ya rige HTTP.
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup4b", lineaSupervisada: 4);
        var token = await LoginAsync(username, password);

        await using var conexion = ConectarHub(token);
        var lector = Escuchar(conexion);
        await conexion.StartAsync();

        await PublicarEnGrupoAsync("linea:1", "hola L1");

        (await LeerConTimeoutAsync(lector, ESPERA_AUSENCIA)).Should().BeNull();
    }

    [Fact]
    public async Task Un_supervisor_no_recibe_lo_publicado_en_planta()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup2", lineaSupervisada: 2);
        var token = await LoginAsync(username, password);

        await using var conexion = ConectarHub(token);
        var lector = Escuchar(conexion);
        await conexion.StartAsync();

        await PublicarEnGrupoAsync("planta", "solo Coordinador");

        (await LeerConTimeoutAsync(lector, ESPERA_AUSENCIA)).Should().BeNull();
    }

    [Fact]
    public async Task Un_coordinador_recibe_lo_publicado_en_cualquier_linea_y_en_planta()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord");
        var token = await LoginAsync(username, password);

        await using var conexion = ConectarHub(token);
        var lector = Escuchar(conexion);
        await conexion.StartAsync();

        (await PublicarYEsperarAsync("linea:6", "hola L6", lector)).Should().Be("hola L6");
        (await PublicarYEsperarAsync("planta", "hola planta", lector)).Should().Be("hola planta");
    }

    [Fact]
    public async Task El_supervisor_de_L8_entra_al_grupo_bolson_no_a_linea_8()
    {
        // Linea.EsBolson distingue al Bolsón — nunca su Id (Domain/Entities/Linea.cs).
        // El dato sembrado real (DatosEstructurales) marca EsBolson en el Id 8, pero
        // el Hub nunca asume ese número: solo consulta la columna.
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "supL8", lineaSupervisada: 8);
        var token = await LoginAsync(username, password);

        await using var conexion = ConectarHub(token);
        var lector = Escuchar(conexion);
        await conexion.StartAsync();

        (await PublicarYEsperarAsync("bolson", "hola bolsón", lector)).Should().Be("hola bolsón");

        await PublicarEnGrupoAsync("linea:8", "esto no debería llegarle a nadie por este camino");
        (await LeerConTimeoutAsync(lector, ESPERA_AUSENCIA)).Should().BeNull();
    }

    [Fact]
    public async Task Todos_los_supervisores_reciben_lo_publicado_en_avisos_sin_importar_su_linea()
    {
        var (_, userA, passA) = await CrearUsuarioAsync("supervisor", "supAvisosA", lineaSupervisada: 1);
        var (_, userB, passB) = await CrearUsuarioAsync("supervisor", "supAvisosB", lineaSupervisada: 4);
        var tokenA = await LoginAsync(userA, passA);
        var tokenB = await LoginAsync(userB, passB);

        await using var conexionA = ConectarHub(tokenA);
        await using var conexionB = ConectarHub(tokenB);
        var lectorA = Escuchar(conexionA);
        var lectorB = Escuchar(conexionB);
        await conexionA.StartAsync();
        await conexionB.StartAsync();

        // Reintenta hasta que AMBAS conexiones hayan recibido algo — misma
        // razón que PublicarYEsperarAsync (OnConnectedAsync puede seguir en
        // curso cuando StartAsync ya volvió en el cliente).
        string? mensajeA = null, mensajeB = null;
        for (var i = 0; i < 8 && (mensajeA is null || mensajeB is null); i++)
        {
            await PublicarEnGrupoAsync("avisos", "L4 · Puesto 3 — relevo sugerido · 62 min");
            mensajeA ??= await LeerConTimeoutAsync(lectorA, TimeSpan.FromMilliseconds(400));
            mensajeB ??= await LeerConTimeoutAsync(lectorB, TimeSpan.FromMilliseconds(400));
        }

        mensajeA.Should().NotBeNull();
        mensajeB.Should().NotBeNull();
    }

    [Fact]
    public async Task Sin_token_la_conexion_se_rechaza()
    {
        await using var conexion = ConectarHub(token: null);

        var accion = async () => await conexion.StartAsync();

        await accion.Should().ThrowAsync<Exception>("[Authorize] exige sesión — sin token no hay handshake posible");
    }
}

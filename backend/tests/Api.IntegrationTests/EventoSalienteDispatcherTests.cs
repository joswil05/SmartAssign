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
/// UT-E12.3 (docs/PROGRESO.md): la prueba de punta a punta de la bandeja
/// de salida — <c>sp_RegistrarParo</c> (SQL real) → <c>EventoSaliente</c>
/// → <c>EventoSalienteDispatcher</c> (ya corriendo solo, es un
/// `IHostedService` que <see cref="SmartAssignApiFactory"/> arranca con
/// el resto del host) → <c>PlantaHub</c> → un <see cref="HubConnection"/>
/// real. Nada de esto se invoca a mano: es la prueba de que el mecanismo
/// completo compone, no solo sus piezas por separado.
/// </summary>
public class EventoSalienteDispatcherTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private const string EVENTO = "ParoIniciadoEvento";

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

    private async Task<int> JornadaAbiertaAsync(byte lineaId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        db.Turnos.Add(turno);
        await db.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        db.JornadasLinea.Add(jornada);
        await db.SaveChangesAsync();
        return jornada.Id;
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

    /// <summary>Llama a sp_RegistrarParo tal cual lo llamaría la Api — SQL real, no un atajo de EF.</summary>
    private async Task<int> RegistrarParoAsync(int jornadaLineaId, int usuarioId)
    {
        await using var conexion = new SqlConnection(factory.CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RegistrarParo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@categoria_id", (short)1);
        cmd.Parameters.AddWithValue("@causa_id", (short)1);
        cmd.Parameters.AddWithValue("@descripcion", "Fuga de aceite en el rodillo principal.");
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pParo = new SqlParameter("@paro_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pParo);
        cmd.Parameters.Add(new SqlParameter("@rotativos_liberados", SqlDbType.Int) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return (int)pParo.Value;
    }

    [Fact]
    public async Task Registrar_un_paro_real_llega_al_Coordinador_por_el_hub_de_punta_a_punta()
    {
        var (usuarioId, username, password) = await CrearUsuarioAsync("coordinador");
        var jornada = await JornadaAbiertaAsync(lineaId: 4);
        var token = await LoginAsync(username, password);

        await using var conexion = ConectarHub(token);
        var lector = Escuchar(conexion, EVENTO);
        await conexion.StartAsync();

        var paroId = await RegistrarParoAsync(jornada, usuarioId);

        // El dispatcher sondea cada 1 s (EventoSalienteDispatcher) — hasta
        // ~10 s de margen real, sin acoplar la prueba a su cadencia exacta.
        JsonElement? recibido = null;
        // 25 intentos de 1 s: margen real para LocalDB bajo la carga de la
        // suite completa corriendo en paralelo, no solo en aislamiento.
        for (var i = 0; i < 25 && recibido is null; i++)
            recibido = await LeerConTimeoutAsync(lector, TimeSpan.FromSeconds(1));

        recibido.Should().NotBeNull("sp_RegistrarParo → EventoSaliente → dispatcher → PlantaHub debió entregarlo solo");
        recibido!.Value.GetProperty("ParoId").GetInt32().Should().Be(paroId);
        recibido.Value.GetProperty("LineaId").GetInt32().Should().Be(4);
        recibido.Value.GetProperty("Categoria").GetString().Should().Be("Mecánico");
    }

    [Fact]
    public async Task El_dispatcher_marca_procesado_en_al_entregar()
    {
        var (usuarioId, username, password) = await CrearUsuarioAsync("coordinador");
        var jornada = await JornadaAbiertaAsync(lineaId: 1);
        var token = await LoginAsync(username, password);

        await using var conexion = ConectarHub(token);
        var lector = Escuchar(conexion, EVENTO);
        await conexion.StartAsync();

        var paroId = await RegistrarParoAsync(jornada, usuarioId);

        JsonElement? recibido = null;
        // 25 intentos de 1 s: margen real para LocalDB bajo la carga de la
        // suite completa corriendo en paralelo, no solo en aislamiento.
        for (var i = 0; i < 25 && recibido is null; i++)
            recibido = await LeerConTimeoutAsync(lector, TimeSpan.FromSeconds(1));
        recibido.Should().NotBeNull();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var evento = await db.EventosSalientes.AsNoTracking()
            .SingleAsync(e => e.TipoEvento == EVENTO && e.PayloadJson.Contains($"\"ParoId\":{paroId}"));
        evento.ProcesadoEn.Should().NotBeNull();
    }
}

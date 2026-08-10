using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E6.3 (docs/PROGRESO.md): los dos endpoints que la app necesita antes
/// de tener malla — 05_TRD.md §2.3 <c>GET /auth/me</c> (rol, nombre y
/// línea vigente resuelta en vivo, nunca desde el token) y
/// <c>GET /servidor/info</c> (verificación anónima al escanear el QR de
/// alta, 02 §1.0).
/// </summary>
public class AuthEndpointsTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
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
            Username = username,
            NombreCompleto = $"{prefijoUsername} de prueba",
            Rol = rol,
            OrigenIdentidad = "local",
            PasswordHash = hash,
            PasswordSalt = salt,
            Activo = true,
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

    private static async Task<string> LoginAsync(HttpClient cliente, string username, string password, string deviceId)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username, password, deviceId });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static void ConAutorizacion(HttpClient cliente, string token) =>
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Servidor_info_responde_sin_ningun_token()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/servidor/info");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("servidor").GetString().Should().Be("SmartAssign");
    }

    [Fact]
    public async Task Me_sin_token_se_rechaza()
    {
        using var cliente = factory.CreateClient();

        (await cliente.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_de_un_supervisor_con_linea_devuelve_su_linea_vigente()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_me", lineaSupervisada: 4);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-me-1"));

        var respuesta = await cliente.GetAsync("/api/auth/me");
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("rol").GetString().Should().Be("supervisor");
        var linea = cuerpo.GetProperty("linea");
        linea.ValueKind.Should().NotBe(JsonValueKind.Null);
        linea.GetProperty("id").GetByte().Should().Be(4);
        linea.GetProperty("esBolson").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Me_de_un_supervisor_de_L8_marca_esBolson_en_true()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_me_l8", lineaSupervisada: 8);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-me-l8"));

        var cuerpo = await (await cliente.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("linea").GetProperty("esBolson").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Me_de_un_supervisor_sin_linea_devuelve_linea_nula()
    {
        // 02 §1.1, nodo <¿Tiene línea asignada?>: la rama NO existe de verdad.
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_me_sin_linea");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-me-sin-linea"));

        var cuerpo = await (await cliente.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("linea").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Me_de_un_coordinador_devuelve_linea_nula_aunque_pueda_ver_todas()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord_me");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-me-coord"));

        var cuerpo = await (await cliente.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("rol").GetString().Should().Be("coordinador");
        cuerpo.GetProperty("linea").ValueKind.Should().Be(JsonValueKind.Null);
    }
}

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
/// UT-E2.6 (docs/PROGRESO.md): la suite de aislamiento completa, contra
/// la Api real levantada de punta a punta — HTTP, JWT, autorización y RLS
/// juntos, no cada capa por separado. Es la prueba automatizada del
/// mismo escenario que PC-1 hará a mano: "dos supervisores en dos
/// teléfonos, ninguno ve la línea del otro".
/// </summary>
public class AislamientoEntreSupervisoresTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
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
            NombreCompleto = username,
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
    public async Task Dos_supervisores_en_dos_telefonos_ninguno_ve_la_linea_del_otro()
    {
        var (_, userL1, passL1) = await CrearUsuarioAsync("supervisor", "sup1", lineaSupervisada: 1);
        var (_, userL4, passL4) = await CrearUsuarioAsync("supervisor", "sup4", lineaSupervisada: 4);

        using var telefonoA = factory.CreateClient();
        using var telefonoB = factory.CreateClient();

        ConAutorizacion(telefonoA, await LoginAsync(telefonoA, userL1, passL1, "device-a"));
        ConAutorizacion(telefonoB, await LoginAsync(telefonoB, userL4, passL4, "device-b"));

        (await telefonoA.GetAsync("/api/lineas/1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await telefonoA.GetAsync("/api/lineas/4")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await telefonoB.GetAsync("/api/lineas/4")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await telefonoB.GetAsync("/api/lineas/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_supervisor_de_L2_es_rechazado_en_el_endpoint_de_L4()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup2", lineaSupervisada: 2);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-sup2"));

        (await cliente.GetAsync("/api/lineas/2")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync("/api/lineas/4")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_coordinador_ve_cualquier_linea_sin_filtro()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-coord"));

        (await cliente.GetAsync("/api/lineas")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync("/api/lineas/1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync("/api/lineas/9")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Un_supervisor_no_puede_listar_todas_las_lineas()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup6", lineaSupervisada: 6);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-sup6"));

        (await cliente.GetAsync("/api/lineas")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sin_token_toda_peticion_de_linea_se_rechaza()
    {
        using var cliente = factory.CreateClient();

        (await cliente.GetAsync("/api/lineas/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await cliente.GetAsync("/api/lineas")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_supervisor_sin_linea_asignada_no_ve_ninguna_linea()
    {
        // §2.3: "Si no tiene línea asignada, no puede operar".
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_sin_linea");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-sin-linea"));

        (await cliente.GetAsync("/api/lineas/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

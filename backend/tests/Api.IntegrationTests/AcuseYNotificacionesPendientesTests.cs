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
/// UT-E12.6 (docs/PROGRESO.md): <c>POST /api/notificaciones/{id}/acuse</c>
/// y <c>GET /api/notificaciones/pendientes</c> (05 §2, D5). Usa
/// <c>criticidad='normal'</c> a propósito en todas las pruebas — el
/// escalado (E2E con <c>criticidad='critica'</c>) tiene su propia clase
/// dedicada, <c>EscaladoDeNotificacionesTests</c>, con su propia base
/// aislada, para no competir con el dispatcher real de escalado que
/// corre solo en el host de <see cref="SmartAssignApiFactory"/>.
/// </summary>
public class AcuseYNotificacionesPendientesTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private async Task<(int usuarioId, string username, string password)> CrearUsuarioAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var credenciales = new ServicioCredenciales();
        var password = $"Clave#{Guid.NewGuid():N}"[..16];
        var (hash, salt) = credenciales.HashConSal(password);
        var username = $"u_{Guid.NewGuid():N}"[..20];

        var usuario = new Usuario
        {
            Username = username, NombreCompleto = username, Rol = "supervisor", OrigenIdentidad = "local",
            PasswordHash = hash, PasswordSalt = salt, Activo = true,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return (usuario.Id, username, password);
    }

    private async Task<long> EncolarNotificacionAsync(int usuarioId, string titulo = "Título de prueba")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var notificacion = new Notificacion
        {
            UsuarioId = usuarioId, Tipo = "PruebaDeAcuse", Criticidad = "normal",
            Titulo = titulo, Cuerpo = "Cuerpo de prueba.",
        };
        db.Notificaciones.Add(notificacion);
        await db.SaveChangesAsync();
        return notificacion.Id;
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

    [Fact]
    public async Task Acusar_una_notificacion_propia_marca_acusada_en_y_devuelve_204()
    {
        var (usuarioId, username, password) = await CrearUsuarioAsync();
        var id = await EncolarNotificacionAsync(usuarioId);
        var token = await LoginAsync(username, password);
        using var cliente = ClienteAutenticado(token);

        var respuesta = await cliente.PostAsync($"/api/notificaciones/{id}/acuse", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        (await db.Notificaciones.AsNoTracking().SingleAsync(n => n.Id == id)).AcusadaEn.Should().NotBeNull();
    }

    [Fact]
    public async Task Acusar_dos_veces_es_idempotente_no_corre_la_marca_de_tiempo()
    {
        var (usuarioId, username, password) = await CrearUsuarioAsync();
        var id = await EncolarNotificacionAsync(usuarioId);
        var token = await LoginAsync(username, password);
        using var cliente = ClienteAutenticado(token);

        await cliente.PostAsync($"/api/notificaciones/{id}/acuse", null);
        using var scope1 = factory.Services.CreateScope();
        var primerAcuse = (await scope1.ServiceProvider.GetRequiredService<SmartAssignDbContext>()
            .Notificaciones.AsNoTracking().SingleAsync(n => n.Id == id)).AcusadaEn;

        await Task.Delay(50);
        var segunda = await cliente.PostAsync($"/api/notificaciones/{id}/acuse", null);

        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope2 = factory.Services.CreateScope();
        var segundoAcuse = (await scope2.ServiceProvider.GetRequiredService<SmartAssignDbContext>()
            .Notificaciones.AsNoTracking().SingleAsync(n => n.Id == id)).AcusadaEn;
        segundoAcuse.Should().Be(primerAcuse);
    }

    [Fact]
    public async Task Un_usuario_no_puede_acusar_la_notificacion_de_otro()
    {
        var (usuarioId, _, _) = await CrearUsuarioAsync();
        var (_, otroUsername, otraPassword) = await CrearUsuarioAsync();
        var id = await EncolarNotificacionAsync(usuarioId);
        var token = await LoginAsync(otroUsername, otraPassword);
        using var cliente = ClienteAutenticado(token);

        var respuesta = await cliente.PostAsync($"/api/notificaciones/{id}/acuse", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        (await db.Notificaciones.AsNoTracking().SingleAsync(n => n.Id == id)).AcusadaEn.Should().BeNull();
    }

    [Fact]
    public async Task Pendientes_devuelve_solo_las_no_acusadas_del_usuario_ordenadas_por_fecha()
    {
        var (usuarioId, username, password) = await CrearUsuarioAsync();
        var primera = await EncolarNotificacionAsync(usuarioId, "Primera");
        var segunda = await EncolarNotificacionAsync(usuarioId, "Segunda");
        var token = await LoginAsync(username, password);
        using var cliente = ClienteAutenticado(token);
        await cliente.PostAsync($"/api/notificaciones/{primera}/acuse", null);

        var respuesta = await cliente.GetAsync("/api/notificaciones/pendientes");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var items = cuerpo.EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("id").GetInt64().Should().Be(segunda);
        items[0].GetProperty("titulo").GetString().Should().Be("Segunda");
    }

    [Fact]
    public async Task Pendientes_nunca_devuelve_notificaciones_de_otro_usuario()
    {
        var (usuarioId, _, _) = await CrearUsuarioAsync();
        var (_, otroUsername, otraPassword) = await CrearUsuarioAsync();
        await EncolarNotificacionAsync(usuarioId);
        var token = await LoginAsync(otroUsername, otraPassword);
        using var cliente = ClienteAutenticado(token);

        var respuesta = await cliente.GetAsync("/api/notificaciones/pendientes");

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.EnumerateArray().Should().BeEmpty();
    }
}

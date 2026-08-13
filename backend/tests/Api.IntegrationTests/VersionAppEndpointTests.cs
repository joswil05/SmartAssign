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
/// UT-E14.6 (docs/PROGRESO.md): "Distribución del APK + verificación de
/// versión" — 00 §F3, 04 §10.1, contra la Api real de punta a punta.
/// </summary>
public class VersionAppEndpointTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private async Task<(int usuarioId, string username, string password)> CrearUsuarioAsync(string rol, string prefijo)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var credenciales = new ServicioCredenciales();
        var password = $"Clave#{Guid.NewGuid():N}"[..16];
        var (hash, salt) = credenciales.HashConSal(password);
        var username = $"{prefijo}_{Guid.NewGuid():N}"[..30];

        var usuario = new Usuario
        {
            Username = username, NombreCompleto = username, Rol = rol, OrigenIdentidad = "local",
            PasswordHash = hash, PasswordSalt = salt, Activo = true,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return (usuario.Id, username, password);
    }

    private static async Task<string> LoginAsync(HttpClient cliente, string username, string password, string deviceId)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { username, password, deviceId });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static void ConAutorizacion(HttpClient cliente, string token) =>
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<HttpResponseMessage> PublicarComoCoordinadorAsync(
        HttpClient cliente, string versionNombre, int versionCodigo, string rutaApk, int versionMinimaApi, string? notas = null)
    {
        return await cliente.PostAsJsonAsync("/api/maestros/version-app",
            new { versionNombre, versionCodigo, rutaApk, versionMinimaApi, notas });
    }

    // ═══ GET /api/version-app/actual ═══
    //
    // "Sin ninguna versión publicada todavía" vive en su propia clase
    // (VersionAppSinPublicarTests, más abajo) con su propio
    // SmartAssignApiFactory: VersionApp no tiene una clave de alcance por
    // prueba como línea/jornada — solo una fila puede ser vigente en
    // TODA la base a la vez, así que "todavía no hay ninguna" chocaría
    // contra cualquier otra prueba de esta clase que ya publicó una.

    [Fact]
    public async Task Un_coordinador_publica_una_version_y_queda_como_la_vigente()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord_ver_1");
        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-ver-1"));

        var publicar = await PublicarComoCoordinadorAsync(cliente, "1.0.0", 100, @"C:\apks\v100.apk", 90, "Primera versión publicada.");
        publicar.StatusCode.Should().Be(HttpStatusCode.OK, await publicar.Content.ReadAsStringAsync());
        (await publicar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("versionAppId").GetInt32().Should().BePositive();

        var actual = await cliente.GetAsync("/api/version-app/actual");
        actual.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await actual.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("versionNombre").GetString().Should().Be("1.0.0");
        cuerpo.GetProperty("versionCodigo").GetInt32().Should().Be(100);
        cuerpo.GetProperty("versionMinimaApi").GetInt32().Should().Be(90);
        cuerpo.GetProperty("notas").GetString().Should().Be("Primera versión publicada.");
    }

    [Fact]
    public async Task Publicar_una_version_nueva_desmarca_la_anterior_como_vigente()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord_ver_2");
        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-ver-2"));

        await PublicarComoCoordinadorAsync(cliente, "1.1.0", 110, @"C:\apks\v110.apk", 90);
        await PublicarComoCoordinadorAsync(cliente, "1.2.0", 120, @"C:\apks\v120.apk", 100);

        var actual = await cliente.GetAsync("/api/version-app/actual");
        var cuerpo = await actual.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("versionCodigo").GetInt32().Should().Be(120, "solo la última publicada debe quedar vigente");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var filas = await db.VersionesApp.AsNoTracking()
            .Where(v => v.VersionCodigo == 110 || v.VersionCodigo == 120).ToListAsync();
        filas.Single(v => v.VersionCodigo == 110).Vigente.Should().BeFalse();
        filas.Single(v => v.VersionCodigo == 120).Vigente.Should().BeTrue();
    }

    [Fact]
    public async Task Publicar_con_un_codigo_de_version_repetido_se_rechaza()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord_ver_3");
        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-ver-3"));

        await PublicarComoCoordinadorAsync(cliente, "1.3.0", 130, @"C:\apks\v130.apk", 100);
        var repetida = await PublicarComoCoordinadorAsync(cliente, "1.3.1", 130, @"C:\apks\v130b.apk", 100);

        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cuerpo = await repetida.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("codigo").GetString().Should().Be("VERSION_CODIGO_YA_EXISTE");
    }

    [Fact]
    public async Task Un_supervisor_no_puede_publicar_una_version()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_ver_1");
        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-ver-4"));

        var respuesta = await PublicarComoCoordinadorAsync(cliente, "1.4.0", 140, @"C:\apks\v140.apk", 100);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden, "05_TRD.md §2.3 reserva esta ruta bajo Coordinador");
    }

    // ═══ GET /api/version-app/apk ═══

    [Fact]
    public async Task Descargar_el_apk_vigente_devuelve_el_archivo_real_del_disco()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord_ver_5");
        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-ver-5"));

        var rutaTemporal = Path.Combine(Path.GetTempPath(), $"SmartAssign_{Guid.NewGuid():N}.apk");
        var contenido = "contenido de prueba del apk"u8.ToArray();
        await File.WriteAllBytesAsync(rutaTemporal, contenido);
        try
        {
            await PublicarComoCoordinadorAsync(cliente, "1.5.0", 150, rutaTemporal, 100);

            // Sin autorización a propósito: un dispositivo bloqueado por
            // versión mínima no tiene sesión con la que descargar lo que
            // lo desbloquearía.
            using var clienteAnonimo = factory.CreateClient();
            var descarga = await clienteAnonimo.GetAsync("/api/version-app/apk");

            descarga.StatusCode.Should().Be(HttpStatusCode.OK);
            descarga.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.android.package-archive");
            (await descarga.Content.ReadAsByteArrayAsync()).Should().Equal(contenido);
        }
        finally
        {
            File.Delete(rutaTemporal);
        }
    }

    // "Descargar sin ninguna versión publicada" — misma razón que arriba,
    // vive en VersionAppSinPublicarTests.

    [Fact]
    public async Task Descargar_el_apk_cuando_el_archivo_ya_no_esta_en_disco_devuelve_404_no_un_error()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord_ver_6");
        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-ver-6"));

        // Ruta que nunca existió en disco — simula el hueco entre
        // "se publicó el metadato" y "alguien de verdad copió el archivo".
        await PublicarComoCoordinadorAsync(cliente, "1.6.0", 160, @"C:\apks\no-existe\v160.apk", 100);

        var descarga = await cliente.GetAsync("/api/version-app/apk");

        descarga.StatusCode.Should().Be(HttpStatusCode.NotFound, "un archivo ausente en disco es honesto como 404, no un 500");
    }
}

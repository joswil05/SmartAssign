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
/// UT-E6.6 (docs/PROGRESO.md): <c>GET /api/personal/por-ficha/{ficha}</c>
/// — la resolución del escaneo de gafete que exige 00 §E1/§12.2, contra
/// la Api real. Mismo patrón que MallaLineaEndpointTests (E6.4): HTTP +
/// JWT reales, nada de reimplementación en memoria.
/// </summary>
public class PersonalEndpointTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
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

    private async Task<int> CrearPersonaAsync(string ficha, string categoria)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var p = new Personal { Ficha = ficha, NombreCompleto = "María López Hernández", Categoria = categoria };
        db.Personas.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<short> CrearCapacidadAsync(string nombre)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var capacidad = new CapacidadFisica { Codigo = $"C{Guid.NewGuid():N}"[..10], Nombre = nombre };
        db.CapacidadesFisicas.Add(capacidad);
        await db.SaveChangesAsync();
        return capacidad.Id;
    }

    private async Task AgregarRestriccionAsync(int personalId, short capacidadId, int registradoPor, DateOnly inicio, DateOnly? fin)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        db.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = personalId, CapacidadId = capacidadId,
            FechaInicio = inicio, FechaFin = fin, FechaDictamen = inicio,
            Fuente = "Enfermería", RegistradoPor = registradoPor,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Una_ficha_existente_sin_restricciones_trae_la_lista_vacia_no_ausente()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_pf_1");
        var ficha = $"F{Guid.NewGuid():N}"[..10];
        await CrearPersonaAsync(ficha, "operario");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-pf-1"));
        var respuesta = await cliente.GetAsync($"/api/personal/por-ficha/{ficha}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("nombreCompleto").GetString().Should().Be("María López Hernández");
        cuerpo.GetProperty("ficha").GetString().Should().Be(ficha);
        cuerpo.GetProperty("categoria").GetString().Should().Be("operario");
        cuerpo.GetProperty("restriccionesMedicas").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Una_ficha_inexistente_responde_404_no_un_objeto_vacio()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_pf_2");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-pf-2"));
        var respuesta = await cliente.GetAsync("/api/personal/por-ficha/no-existe-esta-ficha");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Las_restricciones_vigentes_aparecen_explicitas_por_nombre()
    {
        var coord = await CrearUsuarioAsync("coordinador", "coord_pf_3");
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_pf_3");
        var ficha = $"F{Guid.NewGuid():N}"[..10];
        var personalId = await CrearPersonaAsync(ficha, "operario");
        var capacidad = await CrearCapacidadAsync("No levantar carga superior a 10 kg");
        await AgregarRestriccionAsync(personalId, capacidad, coord.usuarioId, new DateOnly(2020, 1, 1), null);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-pf-3"));
        var cuerpo = await (await cliente.GetAsync($"/api/personal/por-ficha/{ficha}")).Content.ReadFromJsonAsync<JsonElement>();

        var restricciones = cuerpo.GetProperty("restriccionesMedicas").EnumerateArray().Select(r => r.GetString()).ToList();
        restricciones.Should().ContainSingle().Which.Should().Be("No levantar carga superior a 10 kg");
    }

    [Fact]
    public async Task Una_restriccion_ya_caducada_no_aparece()
    {
        var coord = await CrearUsuarioAsync("coordinador", "coord_pf_4");
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_pf_4");
        var ficha = $"F{Guid.NewGuid():N}"[..10];
        var personalId = await CrearPersonaAsync(ficha, "operario");
        var capacidad = await CrearCapacidadAsync("Restricción ya vencida");
        await AgregarRestriccionAsync(personalId, capacidad, coord.usuarioId, new DateOnly(2020, 1, 1), new DateOnly(2020, 6, 1));

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-pf-4"));
        var cuerpo = await (await cliente.GetAsync($"/api/personal/por-ficha/{ficha}")).Content.ReadFromJsonAsync<JsonElement>();

        cuerpo.GetProperty("restriccionesMedicas").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Resuelve_la_ficha_de_una_persona_de_otra_linea_sin_alcance_restringido()
    {
        // §12.2: el escáner resuelve por el número impreso en el gafete —
        // la restricción por línea es de la búsqueda manual (03 §3.5), no
        // de esta resolución directa. Un supervisor puede recibir a
        // cualquier persona de la planta (relevos/recepciones, E8/E9).
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_pf_5");
        var ficha = $"F{Guid.NewGuid():N}"[..10];
        await CrearPersonaAsync(ficha, "operador_a");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-pf-5"));
        var respuesta = await cliente.GetAsync($"/api/personal/por-ficha/{ficha}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sin_token_la_peticion_se_rechaza()
    {
        var ficha = $"F{Guid.NewGuid():N}"[..10];
        await CrearPersonaAsync(ficha, "operario");

        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync($"/api/personal/por-ficha/{ficha}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

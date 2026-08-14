using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Api.Endpoints;
using SmartAssign.Application.Preparacion;

namespace Api.IntegrationTests;

/// <summary>
/// Revisión de producción, hallazgos <b>P-04</b>, <b>P-06</b> y <b>P-07</b>.
///
/// Los tres son la misma clase de fallo: <b>el sistema no decía lo que le
/// faltaba</b>. <c>/api/servidor/info</c> respondía "OK" sin tocar la base;
/// nadie comprobaba que el esquema estuviera migrado; y los diez parámetros
/// de planta se siembran vacíos —correcto según R2— pero nada avisaba de
/// cuáles faltaban ni de qué reglas quedaban apagadas por eso. Una planta
/// podía arrancar creyendo que vigila la fatiga y que las notificaciones
/// críticas escalan, sin que ninguna de las dos cosas ocurriera.
/// </summary>
public class SaludDelServidorTests(SmartAssignApiFactory fabrica) : IClassFixture<SmartAssignApiFactory>
{
    private async Task<HttpClient> ComoCoordinadorAsync()
    {
        string username, password;
        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
            var credenciales = new ServicioCredenciales();
            password = $"Clave#{Guid.NewGuid():N}"[..16];
            var (hash, salt) = credenciales.HashConSal(password);
            username = $"salud_{Guid.NewGuid():N}"[..28];

            db.Usuarios.Add(new Usuario
            {
                Username = username, NombreCompleto = username, Rol = "coordinador",
                OrigenIdentidad = "local", PasswordHash = hash, PasswordSalt = salt, Activo = true,
            });
            await db.SaveChangesAsync();
        }

        var cliente = fabrica.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username, password, deviceId = $"dev-salud-{Guid.NewGuid():N}"[..20] });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cuerpo.GetProperty("accessToken").GetString()!);
        return cliente;
    }

    [Fact]
    public async Task La_verificacion_del_alta_consulta_la_base_de_verdad()
    {
        // P-06: con la base en pie sigue diciendo lo mismo de siempre, que
        // es lo que el teléfono espera al escanear el QR (05 §2.3).
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/servidor/info");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("SmartAssign");
    }

    [Fact]
    public async Task La_salud_exige_Coordinador()
    {
        // Qué reglas están apagadas es información de operación.
        var anonimo = fabrica.CreateClient();

        var respuesta = await anonimo.GetAsync("/api/servidor/salud");

        respuesta.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task La_salud_nombra_cada_parametro_que_falta_y_la_regla_que_deja_dormida()
    {
        // P-04: la base de pruebas se migra pero no se siembra ningún
        // parámetro — exactamente el estado de una planta recién instalada.
        var cliente = await ComoCoordinadorAsync();

        var salud = await cliente.GetFromJsonAsync<ServidorEndpoints.SaludRespuesta>("/api/servidor/salud");

        salud.Should().NotBeNull();
        salud!.ParametrosSinConfigurar.Should().NotBeEmpty();

        // El que más duele: sin él, "supervisor no localizable" (00 §D5) no
        // existe, y nadie se entera.
        salud.ParametrosSinConfigurar.Should()
            .Contain(p => p.Clave == "notificacion_acuse_timeout_min");

        salud.ParametrosSinConfigurar.Should()
            .OnlyContain(p => !string.IsNullOrWhiteSpace(p.ReglaDormida),
                "nombrar la clave sin decir qué se apaga no sirve de nada a quien opera");

        salud.Estado.Should().Be("degradado", "la base está bien, pero faltan valores de planta");
    }

    [Fact]
    public async Task La_salud_no_reclama_los_dos_parametros_que_traen_valor_propio()
    {
        // factor_doble_turno y duracion_maxima_transito sí tienen default en
        // el procedimiento: listarlos como huecos sería ruido.
        var cliente = await ComoCoordinadorAsync();

        var salud = await cliente.GetFromJsonAsync<ServidorEndpoints.SaludRespuesta>("/api/servidor/salud");

        salud!.ParametrosSinConfigurar.Should().NotContain(p => p.Clave == "factor_doble_turno");
        salud.ParametrosSinConfigurar.Should().NotContain(p => p.Clave == "duracion_maxima_transito");
    }

    [Fact]
    public async Task La_salud_publica_el_esquema_y_el_reloj()
    {
        var cliente = await ComoCoordinadorAsync();

        var salud = await cliente.GetFromJsonAsync<ServidorEndpoints.SaludRespuesta>("/api/servidor/salud");

        // P-07: la base de pruebas se migra en InitializeAsync, así que aquí
        // no puede quedar ninguna pendiente. Si quedara, el estado sería
        // "no_listo" y esto lo diría en vez de callarlo.
        salud!.BaseDatos.Alcanzable.Should().BeTrue();
        salud.BaseDatos.MigracionesPendientes.Should().Be(0);

        // P-01: el desfase del reloj queda a la vista, para que un servidor
        // puesto en UTC se detecte antes de decidir sobre nadie.
        salud.Reloj.FechaPlanta.Should().Be(SmartAssign.Application.Tiempo.FechaPlanta.Hoy());
        salud.Reloj.DesfaseUtc.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void El_catalogo_cubre_todos_los_parametros_que_el_motor_lee()
    {
        // Guarda contra el olvido: si un procedimiento nuevo lee una clave
        // que no está en el catálogo, la salud no la reclamaría nunca y el
        // hueco volvería a ser invisible. La lista de referencia son las
        // diez claves que hoy leen los procedimientos del esquema.
        string[] enElMotor =
        [
            "duracion_maxima_transito", "eficiencia_umbral_aceptable_pct", "eficiencia_umbral_optimo_pct",
            "factor_doble_turno", "fatiga_critico_default_min", "fatiga_sugerido_default_min",
            "minimo_operarios_default", "notificacion_acuse_timeout_min",
            "umbral_desperdicio_justificacion_pct", "ventana_arranque_min",
        ];

        CatalogoDeParametros.Todos.Select(p => p.Clave).Should().BeEquivalentTo(enElMotor);
    }
}

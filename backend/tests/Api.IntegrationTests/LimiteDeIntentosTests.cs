using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Api.IntegrationTests;

/// <summary>
/// Fábrica con el límite de credenciales bajado a algo comprobable. La
/// normal lo deja prácticamente infinito porque en <c>TestServer</c> no hay
/// IP remota y todas las pruebas caerían en la misma partición.
/// </summary>
public class FabricaConLimiteBajo : SmartAssignApiFactory
{
    protected override void AjustesExtra(IDictionary<string, string?> config) =>
        config["Credenciales:IntentosPorMinuto"] = "3";
}

/// <summary>
/// Revisión de producción, hallazgo <b>P-11</b>: <c>/api/auth/*</c> no tenía
/// ningún límite de tasa. El bloqueo por intentos fallidos de E2 es <b>por
/// usuario</b>: frena a quien adivina la contraseña de una persona, pero no
/// a quien prueba una contraseña común contra las 160 fichas del padrón, ni
/// a un cliente en bucle que tumbe el login justo al arranque del turno,
/// que es cuando 160 personas necesitan entrar a la vez.
/// </summary>
public class LimiteDeIntentosTests(FabricaConLimiteBajo fabrica) : IClassFixture<FabricaConLimiteBajo>
{
    [Fact]
    public async Task Pasado_el_limite_el_login_responde_429_con_Retry_After()
    {
        var cliente = fabrica.CreateClient();
        var credencialesFalsas = new { username = "no_existe", password = "loQueSea", deviceId = "dev-limite" };

        // Las primeras caen dentro del límite: rechazadas por credenciales,
        // que es un 4xx normal, no un 429.
        for (var i = 0; i < 3; i++)
        {
            var dentro = await cliente.PostAsJsonAsync("/api/auth/login", credencialesFalsas);
            dentro.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                $"el intento {i + 1} todavía está dentro del límite de 3 por minuto");
        }

        var pasada = await cliente.PostAsJsonAsync("/api/auth/login", credencialesFalsas);

        pasada.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        pasada.Headers.RetryAfter.Should().NotBeNull(
            "un cliente que no sabe cuánto esperar reintenta en bucle y empeora lo que el límite contiene");

        var cuerpo = await pasada.Content.ReadAsStringAsync();
        cuerpo.Should().Contain("DEMASIADOS_INTENTOS");
    }

    [Fact]
    public async Task El_limite_no_alcanza_al_resto_de_la_Api()
    {
        // Limitar los endpoints con sesión penalizaría a un supervisor
        // llenando su línea a toda velocidad, que es exactamente lo que el
        // sistema quiere que ocurra (§8.4, la ventana de arranque es corta).
        var cliente = fabrica.CreateClient();

        for (var i = 0; i < 12; i++)
        {
            var respuesta = await cliente.GetAsync("/api/servidor/info");
            respuesta.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }
}

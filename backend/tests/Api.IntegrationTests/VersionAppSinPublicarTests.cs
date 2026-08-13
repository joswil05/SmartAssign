using System.Net;
using FluentAssertions;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E14.6 (docs/PROGRESO.md): los dos casos "todavía no se publicó
/// ninguna versión" de <c>VersionAppEndpointTests</c>, en su propia
/// clase con su propio <c>SmartAssignApiFactory</c> — <c>VersionApp</c>
/// no tiene una clave de alcance por prueba (línea, jornada, ficha...):
/// solo una fila puede ser vigente en TODA la base a la vez, así que
/// "todavía no hay ninguna" solo se puede probar en una base que
/// ninguna otra prueba haya tocado.
/// </summary>
public class VersionAppSinPublicarTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    [Fact]
    public async Task Consultar_la_version_actual_sin_ninguna_publicada_todavia_devuelve_404_no_una_inventada()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/version-app/actual");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound, "§1.3: honestidad del dato, nunca una versión inventada por defecto");
    }

    [Fact]
    public async Task Descargar_el_apk_sin_ninguna_version_publicada_todavia_devuelve_404()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/version-app/apk");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

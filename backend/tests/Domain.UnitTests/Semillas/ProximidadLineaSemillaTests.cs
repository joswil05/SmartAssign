using FluentAssertions;
using SmartAssign.Domain.Semillas;
using Xunit;

namespace Domain.UnitTests.Semillas;

/// <summary>
/// UT-E1.4 (docs/PROGRESO.md). Verifica la semilla de proximidad contra
/// la tabla corregida de docs/00_DECISIONES.md §A1, celda por celda.
///
/// Por qué existe esta prueba y no basta con revisar el código a simple
/// vista: si esta tabla entra mal, el motor de relevos (etapa E9)
/// funcionará perfectamente enviando gente al sitio equivocado, y el
/// fallo será invisible hasta que alguien camine de más en la planta real.
/// </summary>
public class ProximidadLineaSemillaTests
{
    [Fact]
    public void La_fila_de_L10_es_la_corregida_por_el_cliente()
    {
        var l10 = DatosEstructurales.Proximidad()
            .Where(p => p.LineaOrigenId == 10)
            .OrderBy(p => p.Orden)
            .Select(p => p.LineaDestinoId)
            .ToArray();

        // docs/00_DECISIONES.md §A1 — corrección literal del cliente.
        // La fuente original traía "L3, L9, ..." con L2 repetido; la
        // corrección cambia también las dos primeras posiciones, no solo
        // el duplicado.
        byte[] esperado = [9, 3, 6, 7, 4, 2, 1, 5, 8];

        l10.Should().Equal(esperado);
    }

    [Fact]
    public void L8_nunca_aparece_como_linea_origen()
    {
        DatosEstructurales.Proximidad()
            .Any(p => p.LineaOrigenId == 8)
            .Should().BeFalse("la L8 nunca busca 'la línea más cercana': es siempre el destino de respaldo (§9.5)");
    }

    [Fact]
    public void Cada_una_de_las_9_lineas_origen_tiene_exactamente_9_destinos_sin_repetir()
    {
        var porOrigen = DatosEstructurales.Proximidad().GroupBy(p => p.LineaOrigenId);

        porOrigen.Should().HaveCount(9);
        foreach (var grupo in porOrigen)
        {
            grupo.Select(p => p.LineaDestinoId).Distinct().Should().HaveCount(9,
                $"la línea origen {grupo.Key} no puede repetir ni omitir ningún destino");
            grupo.Should().NotContain(p => p.LineaDestinoId == grupo.Key,
                "una línea nunca es la más cercana a sí misma");
        }
    }

    [Fact]
    public void La_proximidad_es_asimetrica_a_proposito_entre_L1_y_L5()
    {
        // A3: L1 es la más cercana a L5, pero L5 es la penúltima para L1.
        // No es un error de captura — es un grafo dirigido, no una distancia.
        var datos = DatosEstructurales.Proximidad();

        var l5DesdeL1 = datos.Single(p => p.LineaOrigenId == 1 && p.LineaDestinoId == 5).Orden;
        var l1DesdeL5 = datos.Single(p => p.LineaOrigenId == 5 && p.LineaDestinoId == 1).Orden;

        l1DesdeL5.Should().Be(1, "L1 es la más cercana a L5 (A3)");
        l5DesdeL1.Should().Be(8, "L5 es la penúltima para L1 — la asimetría es intencional");
    }
}

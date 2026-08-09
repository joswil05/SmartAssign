using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Arquitectura.Tests;

/// <summary>
/// Reglas de dependencia de docs/05_TRD.md §4.1. La regla más importante
/// del proyecto (A9: el motor de relevos nunca referencia el servicio de
/// prioridad) se añade en la etapa E9, cuando ambos existan — ver
/// UT-E9.9 en docs/PROGRESO.md. Esta clase arranca con las reglas que
/// ya son verificables desde la etapa E1: el Dominio no depende de nada.
/// </summary>
public class CapasTests
{
    private const string Dominio = "SmartAssign.Domain";
    private const string Aplicacion = "SmartAssign.Application";
    private const string Infraestructura = "SmartAssign.Infrastructure";
    private const string Api = "SmartAssign.Api";

    [Fact]
    public void El_dominio_no_depende_de_ninguna_otra_capa()
    {
        var resultado = Types.InAssembly(typeof(SmartAssign.Domain.Entities.Linea).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Aplicacion, Infraestructura, Api)
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(
            "el Dominio es el centro de Clean Architecture: nada de fuera debe filtrarse hacia adentro. " +
            $"Tipos que lo violan: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void La_aplicacion_no_depende_de_infraestructura_ni_de_api()
    {
        var asamblea = System.Reflection.Assembly.Load(Aplicacion);

        var resultado = Types.InAssembly(asamblea)
            .Should()
            .NotHaveDependencyOnAny(Infraestructura, Api)
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(
            $"Tipos que lo violan: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }
}

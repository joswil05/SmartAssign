using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Infrastructure.Persistence;

namespace Arquitectura.Tests;

/// <summary>
/// UT-E9.9 (docs/PROGRESO.md): "Relevos no referencia prioridad" — 00
/// §A9, 05 §4.1, literal: "el ensamblado Relevos no referencia el
/// servicio de prioridad. Hay una prueba de arquitectura que falla la
/// compilación si esa dependencia aparece."
///
/// El diseño real de este backend no separó los cuatro motores en
/// ensamblados/namespaces C# (<c>Dominio/Motores/AsignacionInicial</c>,
/// <c>.../Relevos</c>, etc., como esbozaba 05 §4.1) — la lógica de
/// negocio vive en procedimientos y funciones T-SQL (04 §7.5, DENY +
/// SPs como único camino de escritura), no en clases C#. Por eso esta
/// UT NO usa <c>NetArchTest</c> (que solo entiende ensamblados/tipos
/// .NET, y aquí no hay ninguno que verificar) — verifica lo que
/// realmente existe: el TEXTO de los objetos SQL desplegados, leído de
/// una LocalDB real (<c>OBJECT_DEFINITION</c>), mismo criterio R1 de
/// "nunca mockeado" que el resto del proyecto.
///
/// "El motor de relevos" se identifica por lo que de verdad lo define:
/// cualquier procedimiento/función/vista cuyo texto mencione
/// <c>SolicitudRelevo</c> o <c>RelevoDescartado</c> — las dos tablas que
/// son el sustantivo propio del motor (04 §5.3). Esto descubre
/// automáticamente los objetos de E9.1-E9.7 y cualquiera que se agregue
/// después, sin mantener una lista manual — así es como el propio 05
/// §4.1 pide que se comporte: "falla si esa dependencia aparece",
/// también en el futuro.
///
/// Control positivo: <c>sp_ArrancarTurno</c> (E5.7) SÍ referencia
/// <c>PrioridadLinea</c> — la comprobación no está probando algo vacío
/// por casualidad.
/// </summary>
public class MotorDeRelevosTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    public async Task InitializeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    private async Task<List<(string Nombre, string Definicion)>> ObjetosQueMencionanAsync(params string[] terminos)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            SELECT o.name, sm.definition
              FROM sys.sql_modules sm
              JOIN sys.objects o ON o.object_id = sm.object_id
             WHERE o.type IN ('P', 'FN', 'TF', 'IF', 'V'); -- procedimientos, funciones (escalares y de tabla), vistas
            """;
        var resultado = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var nombre = reader.GetString(0);
            var definicion = reader.IsDBNull(1) ? "" : reader.GetString(1);
            if (terminos.Any(t => definicion.Contains(t, StringComparison.OrdinalIgnoreCase)))
                resultado.Add((nombre, definicion));
        }
        return resultado;
    }

    [Fact]
    public async Task El_motor_de_relevos_nunca_referencia_PrioridadLinea()
    {
        // 00 §A9 / 05 §4.1, literal: "el motor de relevos se rige SOLO por
        // proximidad y compatibilidad; la prioridad de líneas SOLO aplica
        // a la asignación inicial."
        var objetosDelMotorDeRelevos = await ObjetosQueMencionanAsync("SolicitudRelevo", "RelevoDescartado");

        objetosDelMotorDeRelevos.Should().NotBeEmpty("si esto viene vacío, la comprobación de abajo no prueba nada — E9.1-E9.7 ya deberían existir");

        var violaciones = objetosDelMotorDeRelevos
            .Where(o => o.Definicion.Contains("PrioridadLinea", StringComparison.OrdinalIgnoreCase))
            .Select(o => o.Nombre)
            .ToList();

        violaciones.Should().BeEmpty(
            "el motor de relevos (sp_DetectarFatiga, sp_MarcarRelevoSolicitado, sp_ProponerRelevista, " +
            "sp_AceptarRelevo, sp_RechazarPropuestaRelevo, sp_LimpiarDescartado, sp_SugerirDestinoRelevado, " +
            "vw_SolicitudRelevo_L8...) nunca debe consultar PrioridadLinea (00 §A9) — " +
            "implementarlo como un motor parametrizado es exactamente lo que A9 prohíbe");
    }

    [Fact]
    public async Task Control_positivo_el_motor_de_asignacion_inicial_si_referencia_PrioridadLinea()
    {
        // Si esta prueba fallara, la de arriba estaría comprobando algo vacío por casualidad.
        var objetosDeAsignacionInicial = await ObjetosQueMencionanAsync("PrioridadLinea");

        objetosDeAsignacionInicial.Select(o => o.Nombre).Should().Contain("sp_ArrancarTurno",
            "el barrido por prioridad vigente (§8.3, A9) es el único motor que sí usa PrioridadLinea");
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.SqlClient;
using SmartAssign.Infrastructure.Persistence;
using Xunit;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E1.1–E1.5 (docs/PROGRESO.md), como prueba automatizada y no solo
/// como verificación manual de sesión: contra una base descartable, aplica
/// las migraciones desde cero, comprueba las restricciones de integridad
/// y la semilla, y confirma que la migración revierte limpiamente.
/// </summary>
public class EsquemaBaseTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<SmartAssignDbContext>()
            .UseSqlServer(CadenaConexion)
            .Options;
        return new SmartAssignDbContext(options);
    }

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

    [Fact]
    public async Task La_semilla_estructural_siembra_10_lineas_con_un_solo_bolson()
    {
        await using var ctx = CrearContexto();

        var lineas = await ctx.Lineas.ToListAsync();
        lineas.Should().HaveCount(10);
        lineas.Count(l => l.EsBolson).Should().Be(1);
        lineas.Single(l => l.EsBolson).Codigo.Should().Be("L8");
    }

    [Fact]
    public async Task La_prioridad_base_tiene_10_ordenes_distintos_vigentes()
    {
        await using var ctx = CrearContexto();

        var vigentes = await ctx.PrioridadesLinea.Where(p => p.VigenteHasta == null).ToListAsync();
        vigentes.Should().HaveCount(10);
        vigentes.Select(p => p.Orden).Distinct().Should().HaveCount(10);

        // §3.3: L4 > L1 > L2 > L6 > L7 > L5 > L3 > L8 > L9 > L10
        vigentes.Single(p => p.Orden == 1).LineaId.Should().Be(4);
        vigentes.Single(p => p.Orden == 10).LineaId.Should().Be(10);
    }

    [Fact]
    public async Task No_se_puede_insertar_un_segundo_bolson()
    {
        await using var ctx = CrearContexto();

        ctx.Lineas.Add(new SmartAssign.Domain.Entities.Linea
        {
            Id = 11,
            Codigo = "L11",
            Nombre = "Línea de prueba",
            EsBolson = true // ya existe L8 — debe violar UX_Linea_bolson
        });

        var accion = async () => await ctx.SaveChangesAsync();
        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task No_se_puede_asignar_el_mismo_supervisor_a_dos_lineas()
    {
        await using var ctx = CrearContexto();

        var l1 = await ctx.Lineas.SingleAsync(l => l.Codigo == "L1");
        var l2 = await ctx.Lineas.SingleAsync(l => l.Codigo == "L2");
        l1.SupervisorActualId = 999;
        l2.SupervisorActualId = 999;

        var accion = async () => await ctx.SaveChangesAsync();
        await accion.Should().ThrowAsync<DbUpdateException>("UX_Linea_supervisor exige un supervisor por línea, §2.3");
    }

    [Fact]
    public async Task La_migracion_revierte_limpiamente()
    {
        await using (var ctx = CrearContexto())
        {
            await ctx.Database.GetDbConnection().OpenAsync();
            await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sys.tables";
            var tablasAntes = (int)(await cmd.ExecuteScalarAsync())!;
            tablasAntes.Should().BeGreaterThan(1, "las migraciones deben haber creado las tablas del dominio");
        }

        // Revertir todas las migraciones (target "0") y confirmar que solo
        // queda el historial de EF Core — el mismo comando que se verificó
        // manualmente en la sesión, ahora como prueba permanente.
        await using var ctxRevertir = CrearContexto();
        var migrador = ctxRevertir.Database.GetService<IMigrator>();
        await migrador.MigrateAsync("0");

        await using var verificacion = new SqlConnection(CadenaConexion);
        await verificacion.OpenAsync();
        await using var cmdVerificacion = verificacion.CreateCommand();
        cmdVerificacion.CommandText = "SELECT COUNT(*) FROM sys.tables";
        var tablasDespues = (int)(await cmdVerificacion.ExecuteScalarAsync())!;
        tablasDespues.Should().Be(1, "tras revertir todo, solo debe quedar __EFMigrationsHistory");

        // Dejar la base en el estado esperado por el resto de la clase
        await using var ctxFinal = CrearContexto();
        await ctxFinal.Database.MigrateAsync();
    }
}

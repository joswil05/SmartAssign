using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// Levanta la Api real (el mismo Program.cs, el mismo pipeline de
/// autenticación/autorización/RLS) contra una base LocalDB descartable
/// y exclusiva de esta instancia — nunca contra SmartAssignDev.
/// </summary>
public class SmartAssignApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignApiTest_{Guid.NewGuid():N}";

    public string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SmartAssignDb"] = CadenaConexion,
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// E12.3 obligó a reordenar esto: <c>EventoSalienteDispatcher</c> es un
    /// <c>IHostedService</c> que sondea la base cada segundo mientras el
    /// host siga vivo — si se intenta <c>DROP DATABASE</c> antes de
    /// detenerlo, SQL Server rechaza el borrado porque la base sigue "en
    /// uso" por una conexión pooled del dispatcher. Se detiene el host
    /// PRIMERO (con él, cualquier `IHostedService` y sus conexiones), se
    /// limpia el pool de conexiones explícitamente, y solo entonces se
    /// abre una conexión nueva —ajena a los servicios del host, que ya no
    /// existen— para soltar la base.
    /// </summary>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        SqlConnection.ClearAllPools();

        await using var db = new SmartAssignDbContext(
            new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);
        await db.Database.EnsureDeletedAsync();
    }
}

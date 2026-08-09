using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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

    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Api.Notificaciones;
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

    protected virtual void AjustesExtra(IDictionary<string, string?> config) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var ajustes = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SmartAssignDb"] = CadenaConexion,
                // P-03: BarridosDelMotorService abre solicitudes de relevo
                // por su cuenta cada 30 s. En pruebas competiría con los
                // escenarios que construyen su propia fatiga a mano, así
                // que aquí se apaga el temporizador — los barridos se
                // prueban llamando a sus métodos directamente
                // (BarridosDelMotorTests), que es lo que hace el trabajo.
                ["Barridos:Habilitado"] = "false",
                // P-11: en TestServer no hay IP remota, así que todas las
                // pruebas caerían en la misma partición del limitador y se
                // estrangularían entre sí. El límite real se prueba en
                // LimiteDeIntentosTests, con su propia fábrica.
                ["Credenciales:IntentosPorMinuto"] = "100000",
            };
            AjustesExtra(ajustes);
            config.AddInMemoryCollection(ajustes);
        });

        // E12.4: no hay credenciales reales de Firebase en CI — el único
        // servicio "externo" que esta suite reemplaza, para poder
        // observar NotificacionDispatcher entregando de punta a punta en
        // vez de quedarse pendiente para siempre contra el stub real.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IServicioNotificacionesPush, ServicioNotificacionesPushDeCaptura>();
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

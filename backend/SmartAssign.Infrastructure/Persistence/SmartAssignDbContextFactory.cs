using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartAssign.Infrastructure.Persistence;

/// <summary>
/// Permite ejecutar `dotnet ef migrations add` directamente desde este
/// proyecto sin depender del hosting de la Api. Solo se usa en tiempo de
/// diseño; en tiempo de ejecución la cadena real viene de
/// appsettings (Program.cs de SmartAssign.Api).
/// </summary>
public class SmartAssignDbContextFactory : IDesignTimeDbContextFactory<SmartAssignDbContext>
{
    public SmartAssignDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SmartAssignDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=SmartAssignDev;Trusted_Connection=True;TrustServerCertificate=True;");
        return new SmartAssignDbContext(optionsBuilder.Options);
    }
}

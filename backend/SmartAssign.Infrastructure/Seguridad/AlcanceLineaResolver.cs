using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Seguridad;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Seguridad;

public class AlcanceLineaResolver(SmartAssignDbContext db) : IAlcanceLineaResolver
{
    public Task<byte?> LineaDeSupervisorAsync(int usuarioId, CancellationToken ct = default) =>
        db.Lineas.Where(l => l.SupervisorActualId == usuarioId)
            .Select(l => (byte?)l.Id)
            .SingleOrDefaultAsync(ct);
}

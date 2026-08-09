using Microsoft.EntityFrameworkCore;
using SmartAssign.Api.Seguridad;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// Primeros endpoints reales protegidos por el aislamiento de la etapa
/// E2 (04 §6.2: "Ver malla de cualquier línea" / "Ver malla de su
/// línea"). Exponen solo el resumen de la línea — la malla completa de
/// puestos llega en la etapa E6; esto existe para que PC-1 tenga algo
/// real que dos supervisores puedan golpear desde dos teléfonos.
/// </summary>
public static class LineaEndpoints
{
    public static IEndpointRouteBuilder MapLineaEndpoints(this IEndpointRouteBuilder app)
    {
        // Coordinador: las 10 líneas, sin filtro (04 §6.2).
        app.MapGet("/api/lineas", async (SmartAssignDbContext db, CancellationToken ct) =>
            {
                var lineas = await db.Lineas.OrderBy(l => l.Id)
                    .Select(l => new { l.Id, l.Codigo, l.Nombre, l.Situacion })
                    .ToListAsync(ct);
                return Results.Ok(lineas);
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        // Coordinador: cualquier línea. Supervisor: únicamente la suya,
        // resuelta en vivo — nunca la elige de una lista (§2.3).
        app.MapGet("/api/lineas/{lineaId}", async (byte lineaId, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var linea = await db.Lineas.Where(l => l.Id == lineaId)
                    .Select(l => new { l.Id, l.Codigo, l.Nombre, l.Situacion })
                    .SingleOrDefaultAsync(ct);
                return linea is null ? Results.NotFound() : Results.Ok(linea);
            })
            .RequireAuthorization()
            .AddEndpointFilter<AlcanceLineaEndpointFilter>();

        return app;
    }
}

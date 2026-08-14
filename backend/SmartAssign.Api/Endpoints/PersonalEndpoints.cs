using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Tiempo;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// 05_TRD.md §2.3: "GET /personal/por-ficha/{ficha} — Resolución del
/// escaneo (§12.2)". La trae aquí, antes de su etapa nominal, el modal de
/// confirmación de identidad (E6.6, mismo patrón que E6.3 trajo
/// /auth/me y /servidor/info, y E6.4 trajo /lineas/{id}/puestos): sin
/// este endpoint el modal no tiene con qué confirmar nada de verdad.
///
/// Deliberadamente **sin alcance de línea**: un gafete escaneado puede
/// pertenecer a cualquier persona de la planta (recepciones, relevos
/// entre líneas — E8/E9), no solo a quien ya está en la línea del
/// supervisor que escanea. La restricción por línea que sí exige §12.2
/// aplica a la **búsqueda manual** (03 §3.5, /personal/buscar), no a la
/// resolución directa de un gafete — esa pantalla queda fuera de esta UT.
/// </summary>
public static class PersonalEndpoints
{
    /// <summary>Exactamente lo que exige 03 §3.3: nombre, ficha, categoría y restricciones vigentes explícitas.</summary>
    public record PersonalPorFichaRespuesta(
        int PersonalId, string NombreCompleto, string Ficha, string Categoria, IReadOnlyList<string> RestriccionesMedicas);

    public static IEndpointRouteBuilder MapPersonalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/personal/por-ficha/{ficha}", async (string ficha, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var persona = await db.Personas
                    .Where(p => p.Ficha == ficha && p.Activo)
                    .Select(p => new { p.Id, p.NombreCompleto, p.Ficha, p.Categoria })
                    .SingleOrDefaultAsync(ct);

                if (persona is null) return Results.NotFound();

                // Mismo filtro de vigencia que el indicador médico de la
                // malla (E6.4, LineaEndpoints) — aquí se listan los
                // nombres, no solo se cuentan.
                // P-01: era DateTime.UtcNow. Con el servidor en UTC−6 eso
                // devolvía la fecha de mañana desde las 18:00, y una
                // restricción médica vencida hoy dejaba de listarse seis
                // horas antes. 00 §C6: la hora es la del servidor.
                var hoy = FechaPlanta.Hoy();
                var restricciones = await db.RestriccionesMedicas
                    .Where(r => r.PersonalId == persona.Id && r.FechaInicio <= hoy && (r.FechaFin == null || r.FechaFin >= hoy))
                    .OrderBy(r => r.Id)
                    .Select(r => r.Capacidad.Nombre)
                    .ToListAsync(ct);

                return Results.Ok(new PersonalPorFichaRespuesta(persona.Id, persona.NombreCompleto, persona.Ficha, persona.Categoria, restricciones));
            })
            .RequireAuthorization();

        return app;
    }
}

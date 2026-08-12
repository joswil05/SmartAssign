using Microsoft.EntityFrameworkCore;
using SmartAssign.Api.Seguridad;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// Primeros endpoints reales protegidos por el aislamiento de la etapa
/// E2 (04 §6.2: "Ver malla de cualquier línea" / "Ver malla de su
/// línea"). El resumen de línea es de E2; la malla completa de puestos
/// (`/puestos`) es de E6.4 — mismo filtro de alcance, mismo patrón.
/// </summary>
public static class LineaEndpoints
{
    /// <summary>
    /// Puesto tal como lo pinta la tarjeta central de la app (03 §3.1).
    /// <c>NivelFatiga</c>/<c>ExcesoFatiga</c> (E7.4): la barra continua,
    /// null en fijos y en rotativos sin nadie asignado (§9.1) — nunca se
    /// dibuja una barra sin dato real detrás (§1.3).
    /// </summary>
    public record PuestoMallaRespuesta(
        int Id, string Codigo, string NombrePuesto, string Tipo, string Situacion,
        OcupanteRespuesta? Ocupante, int IndicadorMedico, string MicroCopia,
        string? NivelFatiga, decimal? ExcesoFatiga);

    /// <summary>
    /// <c>DobleTurno</c> (00 §B7): "distintivo visual permanente en la
    /// persona, en toda pantalla donde aparezca" — la malla es una de ellas.
    /// </summary>
    public record OcupanteRespuesta(int PersonalId, string NombreCompleto, string Ficha, string Categoria, bool DobleTurno);

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

        // 03 §4.3: fijos primero, rotativos después, cada grupo por
        // identificador de puesto — orden estable, nunca decidido en el
        // cliente. El agrupado visual (fuera de operación colapsado al
        // final) es del cliente (E6.4): esto es dato, no interacción.
        app.MapGet("/api/lineas/{lineaId}/puestos", async (byte lineaId, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var hoy = DateOnly.FromDateTime(DateTime.UtcNow); // hora del servidor, 00 §C6

                var filas = await db.Puestos
                    .Where(p => p.LineaId == lineaId && p.Activo)
                    .Select(p => new
                    {
                        p.Id,
                        p.Codigo,
                        p.NombrePuesto,
                        p.Tipo,
                        Situacion = SmartAssignDbContext.SituacionPuesto(p.Id),
                        NivelFatiga = SmartAssignDbContext.NivelFatiga(p.Id),
                        ExcesoFatiga = SmartAssignDbContext.ExcesoRelativoFatiga(p.Id),
                        Asignacion = db.Asignaciones
                            .Where(a => a.PuestoId == p.Id && a.Fin == null)
                            .Select(a => new { a.PersonalId, a.TitularOriginalId, a.Origen })
                            .SingleOrDefault(),
                    })
                    .OrderBy(p => p.Tipo == "fijo" ? 0 : 1)
                    .ThenBy(p => p.Codigo)
                    .ToListAsync(ct);

                var personalIds = filas.Where(f => f.Asignacion != null).Select(f => f.Asignacion!.PersonalId).ToList();
                var personas = await db.Personas
                    .Where(per => personalIds.Contains(per.Id))
                    .Select(per => new { per.Id, per.NombreCompleto, per.Ficha, per.Categoria, per.DobleTurno })
                    .ToDictionaryAsync(per => per.Id, ct);

                var restriccionesVigentes = await db.RestriccionesMedicas
                    .Where(r => personalIds.Contains(r.PersonalId)
                        && r.FechaInicio <= hoy && (r.FechaFin == null || r.FechaFin >= hoy))
                    .GroupBy(r => r.PersonalId)
                    .Select(g => new { PersonalId = g.Key, Cantidad = g.Count() })
                    .ToDictionaryAsync(g => g.PersonalId, g => g.Cantidad, ct);

                var respuesta = filas.Select(f =>
                {
                    // §2.1: un rotativo vacío no es "libre" (blanco) ni
                    // "vacante crítica" (roja) — es su propio estado
                    // visual, con tratamiento propio. fn_SituacionPuesto
                    // (E5, §5.3) es del motor de barrido, que solo conoce
                    // fijos — la distinción es de presentación, aquí.
                    var situacionMostrada = f.Situacion == "libre" && f.Tipo == "rotativo" ? "descubierto" : f.Situacion;

                    OcupanteRespuesta? ocupante = null;
                    var indicadorMedico = 0;
                    if (f.Asignacion is { } asignacion && personas.TryGetValue(asignacion.PersonalId, out var persona))
                    {
                        ocupante = new OcupanteRespuesta(persona.Id, persona.NombreCompleto, persona.Ficha, persona.Categoria, persona.DobleTurno);
                        indicadorMedico = restriccionesVigentes.GetValueOrDefault(persona.Id, 0);
                    }

                    var microCopia = MicroCopiaDePuesto(situacionMostrada, f.Tipo, f.Asignacion?.TitularOriginalId is not null);

                    return new PuestoMallaRespuesta(
                        f.Id, f.Codigo, f.NombrePuesto, f.Tipo, situacionMostrada, ocupante, indicadorMedico, microCopia,
                        f.NivelFatiga, f.ExcesoFatiga);
                }).ToList();

                return Results.Ok(respuesta);
            })
            .RequireAuthorization()
            .AddEndpointFilter<AlcanceLineaEndpointFilter>();

        return app;
    }

    /// <summary>
    /// Catálogo LITERAL de 03_UIUX_BRIEF.md §7.1 (transcrito de la fuente
    /// §12.5) más las entradas "añadidas" de §7.2, en el mismo registro.
    /// "Esperando el arranque del turno" es la única frase nueva de esta
    /// UT (E6.4): ningún puesto fijo "libre" antes de que arranque el
    /// turno estaba contemplado en el catálogo original — no inventa un
    /// estado, describe con honestidad (§1.3) uno que ya existía.
    /// </summary>
    private static string MicroCopiaDePuesto(string situacion, string tipo, bool cubiertoPorSuplente) => situacion switch
    {
        "fuera_de_operacion" => "Puesto no requerido por el SKU de hoy",
        "ocupado" when cubiertoPorSuplente => "Cubierto por suplente — titular ausente",
        "ocupado" => "Asignado automáticamente por asistencia",
        "vacante_critica" => "Sin titular ni suplente disponible",
        "descubierto" => "Sin ocupante — pendiente de cubrir",
        _ => "Esperando el arranque del turno", // "libre" en un fijo — añadida
    };
}

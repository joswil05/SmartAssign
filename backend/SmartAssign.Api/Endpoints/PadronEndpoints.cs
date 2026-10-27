using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Api.Seguridad;
using SmartAssign.Application.Tiempo;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// Revisión de producción, hallazgo <b>P-13</b>: <c>05_TRD.md §2.3</c> reserva
/// <c>GET/POST/PUT /maestros/personal</c> (padrón, §2.1.6) y
/// <c>POST /maestros/personal/{id}/restricciones</c> (con vigencia, 00 §C14),
/// y <b>ninguno existía</b>. El Coordinador no podía dar de alta a un
/// trabajador, cambiarle el estado, ni registrar una limitación física para
/// que el motor la tuviera en cuenta — todo eso solo se podía por SQL.
///
/// <b>Por qué la guarda de P-02 no lo vio.</b> Aquella prueba comprueba que
/// cada procedimiento almacenado tenga una vía desde la Api. El
/// mantenimiento del padrón no es un procedimiento: es CRUD sobre tablas.
/// Pasó en verde estando ausente. Medía lo construido, no lo pedido.
///
/// <b>Una restricción médica NUNCA se borra</b> (04 §7.5, <c>DENY DELETE</c>
/// real sobre la tabla desde E3): un dictamen que dejó de aplicar se cierra
/// poniéndole <c>fecha_fin</c>, y la fila se queda como traza. Por eso aquí
/// no hay <c>DELETE</c> sino un cierre de vigencia.
/// </summary>
public static class PadronEndpoints
{
    public record AltaPersonal(
        string Ficha, string NombreCompleto, string Categoria,
        string? Sexo, byte? LineaHabitual);

    /// <summary>
    /// Todo opcional: se cambia solo lo que venga. Un PUT que exigiera el
    /// objeto entero obligaría al cliente a reenviar campos que no está
    /// tocando, y cualquier desajuste los pisaría en silencio.
    /// </summary>
    public record CambioPersonal(
        string? NombreCompleto, string? Categoria, string? Sexo,
        byte? LineaHabitual, bool? DobleTurno, bool? Activo);

    public record AltaRestriccion(
        short CapacidadId, DateOnly FechaInicio, DateOnly? FechaFin,
        string Fuente, DateOnly FechaDictamen, string? Observacion);

    public record CierreRestriccion(DateOnly FechaFin);

    private static readonly string[] CategoriasValidas =
        ["operario", "operador_a", "operador_b", "operador_c", "averiero", "liderazgo"];

    public static IEndpointRouteBuilder MapPadronEndpoints(this IEndpointRouteBuilder app)
    {
        var padron = app.MapGroup("/api/maestros/personal")
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        // ── Consultar el padrón (§2.1.6) ────────────────────────────────
        padron.MapGet("/", async (
                string? busqueda, byte? lineaId, bool? soloActivos,
                SmartAssignDbContext db, CancellationToken ct) =>
            {
                var consulta = db.Personas.AsQueryable();

                if (soloActivos ?? true) consulta = consulta.Where(p => p.Activo);
                if (lineaId is { } l) consulta = consulta.Where(p => p.LineaHabitual == l);
                if (!string.IsNullOrWhiteSpace(busqueda))
                    consulta = consulta.Where(p =>
                        p.Ficha.Contains(busqueda) || p.NombreCompleto.Contains(busqueda));

                var hoy = FechaPlanta.Hoy();

                // El conteo de restricciones vigentes viaja con cada persona:
                // es lo que el Coordinador necesita ver para saber a quién
                // revisar, sin abrir 160 fichas una por una.
                var filas = await consulta
                    .OrderBy(p => p.LineaHabitual).ThenBy(p => p.Ficha)
                    .Select(p => new
                    {
                        p.Id, p.Ficha, p.NombreCompleto, p.Categoria, p.Sexo,
                        p.LineaHabitual, p.Situacion, p.DobleTurno, p.Activo, p.OrigenDato,
                        RestriccionesVigentes = db.RestriccionesMedicas.Count(r =>
                            r.PersonalId == p.Id && r.FechaInicio <= hoy &&
                            (r.FechaFin == null || r.FechaFin >= hoy)),
                    })
                    .Take(500)
                    .ToListAsync(ct);

                return Results.Ok(filas);
            });

        // ── Alta de trabajador ──────────────────────────────────────────
        padron.MapPost("/", async (AltaPersonal alta, SmartAssignDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(alta.Ficha) || string.IsNullOrWhiteSpace(alta.NombreCompleto))
                return Results.BadRequest(new { error = "La ficha y el nombre son obligatorios." });

            if (!CategoriasValidas.Contains(alta.Categoria))
                return Results.BadRequest(new { error = "Categoría no válida.", validas = CategoriasValidas });

            if (await db.Personas.AnyAsync(p => p.Ficha == alta.Ficha, ct))
                return Results.Conflict(new { codigoRechazo = "FICHA_YA_EXISTE" });

            // Sexo NUNCA se infiere del nombre (§7.3): nulo es un valor
            // válido y significa "no registrado", no "desconocido, adivina".
            var persona = new Personal
            {
                Ficha = alta.Ficha.Trim(),
                NombreCompleto = alta.NombreCompleto.Trim(),
                Categoria = alta.Categoria,
                Sexo = string.IsNullOrWhiteSpace(alta.Sexo) ? null : alta.Sexo,
                LineaHabitual = alta.LineaHabitual,
                OrigenDato = "real",
            };
            db.Personas.Add(persona);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/maestros/personal/{persona.Id}",
                new { persona.Id, persona.Ficha, persona.NombreCompleto });
        });

        // ── Gestionar su estado ─────────────────────────────────────────
        padron.MapPut("/{personalId:int}", async (
                int personalId, CambioPersonal cambio, SmartAssignDbContext db, CancellationToken ct) =>
        {
            var persona = await db.Personas.SingleOrDefaultAsync(p => p.Id == personalId, ct);
            if (persona is null) return Results.NotFound();

            if (cambio.Categoria is { } cat && !CategoriasValidas.Contains(cat))
                return Results.BadRequest(new { error = "Categoría no válida.", validas = CategoriasValidas });

            // Situacion NO se toca desde aquí: es estado operativo que
            // mueven los procedimientos (asignar, despachar, recibir). Que
            // el Coordinador la editara a mano dejaría a una persona
            // "asignada" sin asignación, o libre estando en tránsito.
            if (cambio.NombreCompleto is { } nombre) persona.NombreCompleto = nombre.Trim();
            if (cambio.Categoria is { } categoria) persona.Categoria = categoria;
            if (cambio.Sexo is { } sexo) persona.Sexo = string.IsNullOrWhiteSpace(sexo) ? null : sexo;
            if (cambio.LineaHabitual is { } linea) persona.LineaHabitual = linea;
            if (cambio.DobleTurno is { } doble) persona.DobleTurno = doble;
            if (cambio.Activo is { } activo) persona.Activo = activo;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { persona.Id, persona.Ficha, persona.Categoria, persona.Activo });
        });

        // ── Limitaciones físicas (00 §C14, §7.2) ────────────────────────

        padron.MapGet("/{personalId:int}/restricciones", async (
                int personalId, SmartAssignDbContext db, CancellationToken ct) =>
        {
            if (!await db.Personas.AnyAsync(p => p.Id == personalId, ct)) return Results.NotFound();

            var hoy = FechaPlanta.Hoy();

            // Se devuelven TODAS, vigentes e históricas, con la vigencia
            // calculada por el servidor. Mostrar solo las activas escondería
            // el historial médico a quien tiene que decidir sobre él.
            var filas = await db.RestriccionesMedicas
                .Where(r => r.PersonalId == personalId)
                .OrderByDescending(r => r.FechaInicio)
                .Select(r => new
                {
                    r.Id, r.CapacidadId,
                    Capacidad = r.Capacidad.Nombre,
                    r.FechaInicio, r.FechaFin, r.Fuente, r.FechaDictamen, r.Observacion,
                    Vigente = r.FechaInicio <= hoy && (r.FechaFin == null || r.FechaFin >= hoy),
                    Permanente = r.FechaFin == null,
                })
                .ToListAsync(ct);

            return Results.Ok(filas);
        });

        padron.MapPost("/{personalId:int}/restricciones", async (
                int personalId, AltaRestriccion alta, SmartAssignDbContext db,
                ClaimsPrincipal usuario, CancellationToken ct) =>
        {
            if (!await db.Personas.AnyAsync(p => p.Id == personalId, ct)) return Results.NotFound();

            if (!await db.CapacidadesFisicas.AnyAsync(c => c.Id == alta.CapacidadId, ct))
                return Results.BadRequest(new { error = "Esa capacidad física no está en el catálogo." });

            if (alta.FechaFin is { } fin && fin < alta.FechaInicio)
                return Results.BadRequest(new { error = "La fecha de fin no puede ser anterior a la de inicio." });

            if (string.IsNullOrWhiteSpace(alta.Fuente))
                return Results.BadRequest(new { error = "La fuente del dictamen es obligatoria: quién lo emitió." });

            // fecha_fin nula = permanente (00 §C14), no "sin definir".
            db.RestriccionesMedicas.Add(new RestriccionMedica
            {
                PersonalId = personalId,
                CapacidadId = alta.CapacidadId,
                FechaInicio = alta.FechaInicio,
                FechaFin = alta.FechaFin,
                Fuente = alta.Fuente.Trim(),
                FechaDictamen = alta.FechaDictamen,
                Observacion = alta.Observacion,
                RegistradoPor = usuario.UsuarioId(),
                OrigenDato = "real",
            });
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/maestros/personal/{personalId}/restricciones", new { registrada = true });
        });

        // Cerrar la vigencia. NO hay DELETE: la tabla tiene DENY DELETE real
        // (04 §7.5) y un dictamen que dejó de aplicar sigue siendo traza.
        padron.MapPut("/{personalId:int}/restricciones/{restriccionId:int}/cerrar", async (
                int personalId, int restriccionId, CierreRestriccion cierre,
                SmartAssignDbContext db, CancellationToken ct) =>
        {
            var restriccion = await db.RestriccionesMedicas
                .SingleOrDefaultAsync(r => r.Id == restriccionId && r.PersonalId == personalId, ct);

            if (restriccion is null) return Results.NotFound();

            if (cierre.FechaFin < restriccion.FechaInicio)
                return Results.BadRequest(new { error = "La fecha de fin no puede ser anterior a la de inicio." });

            restriccion.FechaFin = cierre.FechaFin;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { restriccion.Id, restriccion.FechaFin });
        });

        // ── Catálogo de capacidades físicas (H6) ────────────────────────
        app.MapGet("/api/maestros/capacidades", async (SmartAssignDbContext db, CancellationToken ct) =>
                Results.Ok(await db.CapacidadesFisicas
                    .OrderBy(c => c.Id)
                    .Select(c => new { c.Id, c.Codigo, c.Nombre, c.Activo, EsPlaceholder = c.OrigenDato != "real" })
                    .ToListAsync(ct)))
            .RequireAuthorization();

        return app;
    }
}

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Api.Seguridad;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Application.Preparacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// Revisión de producción, hallazgos <b>P-08</b> y <b>P-04</b>.
///
/// <c>Turno</c> se siembra vacía a propósito (00 §C6: los horarios son dato
/// del cliente) y los parámetros de planta también (R2: no se inventan
/// umbrales). Correcto — pero <b>no había ninguna forma de llenarlos salvo
/// SQL directo</b>, así que una instalación nueva no podía ni planificar un
/// turno. 05_TRD.md §2.3 ya reservaba <c>/maestros</c> para esto.
///
/// <b>Esto no inventa ningún valor.</b> Sigue siendo el cliente quien
/// decide los horarios y los umbrales; lo que cambia es que ahora puede
/// escribirlos desde la aplicación (§2.1.10, "el Coordinador amplía los
/// catálogos") en vez de necesitar acceso a la base.
/// </summary>
public static class MaestrosEndpoints
{
    public record TurnoPeticion(string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin);
    public record ParametroPeticion(string Valor);

    public record ParametroRespuesta(string Clave, string? Valor, string ReglaDormida, bool Configurado, bool TieneValorPorDefecto);

    public static IEndpointRouteBuilder MapMaestrosEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Turnos (00 §C6) ─────────────────────────────────────────────
        app.MapGet("/api/maestros/turnos", async (SmartAssignDbContext db, CancellationToken ct) =>
                Results.Ok(await db.Turnos.OrderBy(t => t.Id)
                    .Select(t => new { t.Id, t.Nombre, t.HoraInicio, t.HoraFin, t.CruzaMedianoche, t.Activo })
                    .ToListAsync(ct)))
            .RequireAuthorization();

        app.MapPost("/api/maestros/turnos", async (
                TurnoPeticion peticion, SmartAssignDbContext db, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(peticion.Nombre))
                    return Results.BadRequest(new { error = "El turno necesita un nombre." });

                if (peticion.HoraInicio == peticion.HoraFin)
                    return Results.BadRequest(new { error = "La hora de inicio y la de fin no pueden ser la misma." });

                if (await db.Turnos.AnyAsync(t => t.Nombre == peticion.Nombre, ct))
                    return Results.Conflict(new { codigoRechazo = "TURNO_YA_EXISTE" });

                // CruzaMedianoche lo deriva la entidad del propio horario
                // (00 §C6: "un turno que cruza medianoche pertenece entero a
                // su fecha de inicio") — no se recibe del cliente, para que
                // no pueda contradecir a las horas que sí manda.
                var turno = new Turno
                {
                    Nombre = peticion.Nombre.Trim(),
                    HoraInicio = peticion.HoraInicio,
                    HoraFin = peticion.HoraFin,
                };
                db.Turnos.Add(turno);
                await db.SaveChangesAsync(ct);

                return Results.Created($"/api/maestros/turnos/{turno.Id}", new { turno.Id, turno.Nombre });
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        // ── Parámetros de planta (§12.6, 04 §9) ─────────────────────────
        // Devuelve el catálogo ENTERO, configurados y no, con la regla que
        // cada hueco deja dormida. Un listado que solo mostrara los que ya
        // tienen valor escondería justo lo que hay que ver (P-04).
        app.MapGet("/api/maestros/parametros", async (SmartAssignDbContext db, CancellationToken ct) =>
            {
                var configurados = await db.Parametros.ToDictionaryAsync(p => p.Clave, p => p.Valor, ct);

                var respuesta = CatalogoDeParametros.Todos
                    .Select(p => new ParametroRespuesta(
                        p.Clave,
                        configurados.GetValueOrDefault(p.Clave),
                        p.ReglaDormida,
                        configurados.ContainsKey(p.Clave),
                        p.TieneValorPorDefecto))
                    .ToList();

                return Results.Ok(respuesta);
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        app.MapPut("/api/maestros/parametros/{clave}", async (
                string clave, ParametroPeticion peticion, SmartAssignDbContext db,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                // Solo claves que el motor de verdad lee: aceptar cualquier
                // cadena dejaría escribir parámetros que nadie consulta y
                // que darían una falsa sensación de estar configurado.
                var conocido = CatalogoDeParametros.Todos.SingleOrDefault(p => p.Clave == clave);
                if (conocido is null)
                    return Results.NotFound(new
                    {
                        codigoRechazo = "PARAMETRO_DESCONOCIDO",
                        mensaje = $"'{clave}' no lo lee ningún procedimiento del motor.",
                        conocidos = CatalogoDeParametros.Todos.Select(p => p.Clave),
                    });

                if (string.IsNullOrWhiteSpace(peticion.Valor))
                    return Results.BadRequest(new { error = "El valor no puede quedar vacío." });

                var usuarioId = int.Parse(usuario.FindFirstValue(ClaimsSmartAssign.UsuarioId)!);
                var existente = await db.Parametros.SingleOrDefaultAsync(p => p.Clave == clave, ct);

                if (existente is null)
                {
                    db.Parametros.Add(new Parametro
                    {
                        Clave = clave,
                        Valor = peticion.Valor.Trim(),
                        Tipo = "texto",
                        Descripcion = conocido.ReglaDormida,
                        ModificadoPor = usuarioId,
                        ModificadoEn = DateTime.UtcNow,
                    });
                }
                else
                {
                    existente.Valor = peticion.Valor.Trim();
                    existente.ModificadoPor = usuarioId;
                    existente.ModificadoEn = DateTime.UtcNow;
                }

                await db.SaveChangesAsync(ct);
                return Results.Ok(new { clave, valor = peticion.Valor.Trim() });
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        return app;
    }
}

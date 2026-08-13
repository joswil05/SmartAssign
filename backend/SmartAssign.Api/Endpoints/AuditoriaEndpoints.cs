using Microsoft.EntityFrameworkCore;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// UT-E14.3 (docs/PROGRESO.md): "auditoría consultable" — §12.7, literal:
/// "toda operación que mueva a una persona debe quedar registrada: quién
/// la hizo, cuándo, sobre quién y con qué resultado." 05_TRD.md §2.3 ya
/// reserva <c>GET /auditoria</c> bajo la sección "Coordinador" — el
/// único rol con visión sin restricción de línea (E2); ninguna fuente le
/// da al Supervisor una vía para consultar auditoría, ni siquiera de su
/// propia línea.
///
/// <c>Auditoria</c> (E2.5) ya escribe una fila por operación, éxito o
/// rechazo, desde el principio del proyecto — esta UT no añade nada al
/// camino de escritura, solo abre el primer camino de lectura.
///
/// <c>Take(500)</c> (más reciente primero) es una decisión de ingeniería,
/// no de negocio: 00 §D7 declara retención indefinida para datos
/// operativos como este, así que la tabla crece sin límite — ninguna
/// fuente pide paginación todavía, pero devolver la tabla entera sin
/// límite en un único <c>GET</c> no es una interpretación razonable de
/// "consultable". Los filtros por línea/persona/actor/rango de fecha
/// dejan acotar antes de llegar a ese límite.
/// </summary>
public static class AuditoriaEndpoints
{
    public record AuditoriaRespuesta(
        long Id, int UsuarioId, string UsuarioNombre, string Rol, string Accion, string Entidad, long? EntidadId,
        int? PersonalId, string? PersonalNombre, byte? LineaId, string? LineaCodigo,
        string Resultado, string? CodigoRechazo, string? DeviceId, DateTime OcurridoEn);

    private const int LimiteFilas = 500;

    public static IEndpointRouteBuilder MapAuditoriaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auditoria", async (
                int? usuarioId, int? personalId, byte? lineaId, string? accion, string? resultado,
                DateTime? desde, DateTime? hasta, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var consulta = db.Auditorias.AsQueryable();
                if (usuarioId is { } u) consulta = consulta.Where(a => a.UsuarioId == u);
                if (personalId is { } per) consulta = consulta.Where(a => a.PersonalId == per);
                if (lineaId is { } l) consulta = consulta.Where(a => a.LineaId == l);
                if (accion is { } acc) consulta = consulta.Where(a => a.Accion == acc);
                if (resultado is { } res) consulta = consulta.Where(a => a.Resultado == res);
                if (desde is { } d) consulta = consulta.Where(a => a.OcurridoEn >= d);
                if (hasta is { } h) consulta = consulta.Where(a => a.OcurridoEn <= h);

                var filas = await consulta
                    .OrderByDescending(a => a.OcurridoEn).ThenByDescending(a => a.Id)
                    .Take(LimiteFilas)
                    .Select(a => new AuditoriaRespuesta(
                        a.Id, a.UsuarioId, db.Usuarios.Where(u => u.Id == a.UsuarioId).Select(u => u.NombreCompleto).SingleOrDefault() ?? "",
                        a.Rol, a.Accion, a.Entidad, a.EntidadId,
                        a.PersonalId, a.PersonalId == null ? null : db.Personas.Where(p => p.Id == a.PersonalId).Select(p => p.NombreCompleto).SingleOrDefault(),
                        a.LineaId, a.LineaId == null ? null : db.Lineas.Where(l => l.Id == a.LineaId).Select(l => l.Codigo).SingleOrDefault(),
                        a.Resultado, a.CodigoRechazo, a.DeviceId, a.OcurridoEn))
                    .ToListAsync(ct);

                return Results.Ok(filas);
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        return app;
    }
}

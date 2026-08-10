using Microsoft.AspNetCore.Authorization;
using SmartAssign.Application.Asignaciones;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Application.Seguridad;
using SmartAssign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// 05_TRD.md §2.3: <c>GET /asignaciones/sugerencia?personalId=</c>
/// (§8.5) y <c>POST /puestos/{id}/asignar</c> (§7.1, "requiere
/// confirmación previa" — el modal de E6.6). Traídos aquí porque E6.8 es
/// la primera UT en la que sp_SugerirPuesto (E6.7) y sp_AsignarPersona
/// (E6.8) tienen un consumidor real — mismo patrón que toda la etapa E6.
/// </summary>
public static class AsignacionEndpoints
{
    public record SugerenciaRespuesta(int? PuestoId, byte? Nivel, string? CodigoRechazo, string? Mensaje);
    public record AsignarPeticion(int PersonalId, Guid IdempotencyKey, bool CederPerfil = false);
    public record AsignarRespuesta(long AsignacionId);
    public record RechazoAsignacion(string Codigo, string Mensaje);

    public static IEndpointRouteBuilder MapAsignacionEndpoints(this IEndpointRouteBuilder app)
    {
        // Alcance forzado por servidor (§2.2), igual que /lineas/mi-linea:
        // la línea nunca la elige quien llama, es la suya en vivo.
        app.MapGet("/api/asignaciones/sugerencia", async (
                int personalId, System.Security.Claims.ClaimsPrincipal usuario,
                IContextoSesionActual contextoSesion, IServicioAsignacion servicio, CancellationToken ct) =>
            {
                if (contextoSesion.Rol != "supervisor" || contextoSesion.LineaId is not { } lineaId)
                    return Results.Forbid();

                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                var resultado = await servicio.SugerirPuestoAsync(personalId, lineaId, usuarioId, ct);
                return Results.Ok(new SugerenciaRespuesta(resultado.PuestoId, resultado.Nivel, resultado.CodigoRechazo, resultado.Mensaje));
            })
            .RequireAuthorization();

        app.MapPost("/api/puestos/{id}/asignar", async (
                int id, AsignarPeticion peticion, System.Security.Claims.ClaimsPrincipal usuario,
                IAuthorizationService autorizacion, IServicioAsignacion servicio, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var lineaDelPuesto = await db.Puestos.Where(p => p.Id == id).Select(p => (byte?)p.LineaId).SingleOrDefaultAsync(ct);
                if (lineaDelPuesto is null) return Results.NotFound();

                var autorizado = await autorizacion.AuthorizeAsync(usuario, lineaDelPuesto.Value, "AlcanceLinea");
                if (!autorizado.Succeeded) return Results.Forbid();

                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                var resultado = await servicio.AsignarPersonaAsync(
                    peticion.PersonalId, id, usuarioId, peticion.IdempotencyKey, peticion.CederPerfil, ct);

                if (resultado.CodigoRechazo is not null)
                    return Results.Json(new RechazoAsignacion(resultado.CodigoRechazo, resultado.Mensaje ?? ""), statusCode: StatusCodes.Status409Conflict);

                return Results.Ok(new AsignarRespuesta(resultado.AsignacionId!.Value));
            })
            .RequireAuthorization();

        return app;
    }
}

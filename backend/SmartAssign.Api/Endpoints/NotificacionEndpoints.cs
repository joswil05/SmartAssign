using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// UT-E12.4: 05 §2.5, "<c>GET /notificaciones/{id}</c> — Contenido real
/// de la notificación, tras despertar por el ping". El paso que descarga
/// lo que la campana vacía (D5) nunca trae — solo aparece aquí, detrás
/// de autenticación y con JWT, tal como el diagrama de la fuente exige.
/// </summary>
public static class NotificacionEndpoints
{
    public static IEndpointRouteBuilder MapNotificacionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notificaciones/{id:long}", async (
                long id, System.Security.Claims.ClaimsPrincipal usuario, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                // Nunca la notificación de otro usuario — mismo criterio
                // que el resto del aislamiento (E2): no se distingue "no
                // existe" de "no es tuya", ninguna pista sobre
                // notificaciones ajenas.
                var notificacion = await db.Notificaciones
                    .Where(n => n.Id == id && n.UsuarioId == usuarioId)
                    .SingleOrDefaultAsync(ct);

                if (notificacion is null) return Results.NotFound();

                object? payload = null;
                if (notificacion.PayloadJson is not null)
                {
                    using var doc = JsonDocument.Parse(notificacion.PayloadJson);
                    payload = doc.RootElement.Clone();
                }

                return Results.Ok(new
                {
                    id = notificacion.Id,
                    tipo = notificacion.Tipo,
                    criticidad = notificacion.Criticidad,
                    titulo = notificacion.Titulo,
                    cuerpo = notificacion.Cuerpo,
                    payload,
                    creadaEn = notificacion.CreadaEn,
                });
            })
            .RequireAuthorization();

        return app;
    }
}

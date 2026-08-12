using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// 05 §2.5 — el catálogo literal de "Dispositivo y notificaciones": el
/// contenido real detrás del ping vacío (E12.4, D5), y la capa de
/// garantía del "sí o sí" (E12.6) — acuse y sincronización al volver al
/// primer plano. El escalado en sí (marcar <c>escalada_en</c> y avisar
/// al Coordinador) lo hace <c>sp_EscalarNotificacionesVencidas</c>
/// (E12.6) por su cuenta; esta clase solo expone lo que el USUARIO
/// puede hacer sobre sus propias notificaciones.
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

                return notificacion is null ? Results.NotFound() : Results.Ok(ARespuesta(notificacion));
            })
            .RequireAuthorization();

        // UT-E12.6: 05 §2, literal — "Marca acusada. Sin esto, la crítica
        // escala (D5)". Idempotente a propósito: el PRIMER acuse es el
        // dato honesto (§12.4) — un acuse tardío, después de que ya
        // escaló, sigue siendo un acuse real y se registra igual
        // (EscaladaEn no se deshace, es un hecho histórico), pero
        // reenviar el acuse dos veces no debe correr la marca de tiempo.
        app.MapPost("/api/notificaciones/{id:long}/acuse", async (
                long id, System.Security.Claims.ClaimsPrincipal usuario, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                var notificacion = await db.Notificaciones
                    .Where(n => n.Id == id && n.UsuarioId == usuarioId)
                    .SingleOrDefaultAsync(ct);

                if (notificacion is null) return Results.NotFound();

                notificacion.AcusadaEn ??= DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                return Results.NoContent();
            })
            .RequireAuthorization();

        // UT-E12.6: 05 §2, literal — "Sincronización al volver al primer
        // plano". 05 §2.5: "la app reconecta SignalR y sincroniza lo que
        // ocurrió mientras estuvo fuera, consultando las notificaciones
        // SIN ACUSAR" — el filtro es deliberadamente AcusadaEn, no
        // EntregadaEn: la app quiere ponerse al día con todo lo que
        // todavía no confirmó, haya llegado el ping de FCM o no (si ya
        // está en primer plano reconectando, no necesita haber recibido
        // el ping para enterarse — por eso NotificacionDispatcher, que
        // solo entrega el PING, no es el único camino de entrega real).
        app.MapGet("/api/notificaciones/pendientes", async (
                System.Security.Claims.ClaimsPrincipal usuario, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                var pendientes = await db.Notificaciones
                    .Where(n => n.UsuarioId == usuarioId && n.AcusadaEn == null)
                    .OrderBy(n => n.CreadaEn)
                    .ToListAsync(ct);

                return Results.Ok(pendientes.Select(ARespuesta));
            })
            .RequireAuthorization();

        return app;
    }

    private static object ARespuesta(Notificacion notificacion)
    {
        object? payload = null;
        if (notificacion.PayloadJson is not null)
        {
            using var doc = JsonDocument.Parse(notificacion.PayloadJson);
            payload = doc.RootElement.Clone();
        }

        return new
        {
            id = notificacion.Id,
            tipo = notificacion.Tipo,
            criticidad = notificacion.Criticidad,
            titulo = notificacion.Titulo,
            cuerpo = notificacion.Cuerpo,
            payload,
            creadaEn = notificacion.CreadaEn,
        };
    }
}

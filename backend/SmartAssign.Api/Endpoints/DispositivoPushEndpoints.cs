using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

public record RegistrarPushTokenPeticion(string DeviceId, string PushToken, string Plataforma = "android");

/// <summary>
/// UT-E12.4: 05 §2.5, "<c>POST /dispositivos/push-token</c> — Registra o
/// renueva el token de mensajería". Escritura directa por EF, sin SP —
/// mismo criterio que <c>SesionDispositivo</c> (E2.2): tabla de
/// bookkeeping de sesión/dispositivo, nunca listada en el DENY de
/// escritura crítica (04 §7.5). <c>UQ_DispositivoPush</c> (un solo
/// <c>device_id</c>) es la especificación misma del upsert — "registra
/// O renueva": el teléfono se trata como compartido por línea (D6), así
/// que cuando otra persona entra en el mismo aparato este mismo endpoint
/// reapunta el token al usuario nuevo, sin dejar un segundo token
/// huérfano para el mismo dispositivo.
/// </summary>
public static class DispositivoPushEndpoints
{
    public static IEndpointRouteBuilder MapDispositivoPushEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dispositivos/push-token", async (
                RegistrarPushTokenPeticion peticion, System.Security.Claims.ClaimsPrincipal usuario,
                SmartAssignDbContext db, CancellationToken ct) =>
            {
                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                var ahora = DateTime.UtcNow;
                var existente = await db.DispositivosPush.SingleOrDefaultAsync(d => d.DeviceId == peticion.DeviceId, ct);

                if (existente is null)
                {
                    db.DispositivosPush.Add(new DispositivoPush
                    {
                        UsuarioId = usuarioId,
                        DeviceId = peticion.DeviceId,
                        PushToken = peticion.PushToken,
                        Plataforma = peticion.Plataforma,
                        RegistradoEn = ahora,
                    });
                }
                else
                {
                    existente.UsuarioId = usuarioId;
                    existente.PushToken = peticion.PushToken;
                    existente.Plataforma = peticion.Plataforma;
                    existente.RegistradoEn = ahora;
                    existente.RevocadoEn = null;
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .RequireAuthorization();

        return app;
    }
}

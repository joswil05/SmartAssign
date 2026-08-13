using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.VersionesApp;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// UT-E14.6 (docs/PROGRESO.md): "Distribución del APK + verificación de
/// versión" — 00 §F3, 04 §10.1. El APK vive en el propio servidor de
/// planta, servido por la misma API (00 §F3, literal) — estos tres
/// endpoints son esa distribución completa.
///
/// <c>GET /api/version-app/actual</c> — anónimo a propósito, mismo
/// criterio que <c>GET /api/servidor/info</c> (E6.3): "la app comprueba
/// la versión al iniciar sesión" (00 §F3) — antes de que exista
/// cualquier sesión, así que no puede exigir un token. Comparar números
/// de versión no es un dato sensible.
///
/// <c>GET /api/version-app/apk</c> — también anónimo: un dispositivo
/// bloqueado por versión mínima (por debajo de <c>version_minima_api</c>)
/// no puede iniciar sesión, así que exigir sesión para descargar la
/// actualización que lo desbloquea sería un candado circular.
///
/// <c>POST /api/maestros/version-app</c> — Coordinador únicamente, 05_TRD.md
/// §2.3 ya la reservaba bajo "Coordinador": "Publica una versión nueva
/// del APK (F3)".
/// </summary>
public static class VersionAppEndpoints
{
    public record VersionActualRespuesta(
        string VersionNombre, int VersionCodigo, int VersionMinimaApi, string? Notas, DateTime PublicadaEn);

    public record PublicarVersionPeticion(
        string VersionNombre, int VersionCodigo, string RutaApk, int VersionMinimaApi, string? Notas);

    public record PublicarVersionRespuesta(int VersionAppId);

    public record RechazoPublicarVersion(string Codigo, string Mensaje);

    public static IEndpointRouteBuilder MapVersionAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/version-app/actual", async (SmartAssignDbContext db, CancellationToken ct) =>
            {
                var version = await db.VersionesApp.Where(v => v.Vigente)
                    .Select(v => new VersionActualRespuesta(v.VersionNombre, v.VersionCodigo, v.VersionMinimaApi, v.Notas, v.PublicadaEn))
                    .SingleOrDefaultAsync(ct);

                // §1.3, honestidad del dato: ninguna versión publicada
                // todavía no es un error — es un servidor recién
                // desplegado. Nunca se inventa una versión "0" por defecto.
                return version is null ? Results.NotFound() : Results.Ok(version);
            })
            .AllowAnonymous();

        app.MapGet("/api/version-app/apk", async (SmartAssignDbContext db, CancellationToken ct) =>
            {
                var rutaApk = await db.VersionesApp.Where(v => v.Vigente)
                    .Select(v => v.RutaApk)
                    .SingleOrDefaultAsync(ct);

                if (rutaApk is null) return Results.NotFound();
                if (!File.Exists(rutaApk)) return Results.NotFound();

                return Results.File(rutaApk, "application/vnd.android.package-archive", "SmartAssign.apk");
            })
            .AllowAnonymous();

        app.MapPost("/api/maestros/version-app", async (
                PublicarVersionPeticion peticion, IServicioVersionApp servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.PublicarVersionAsync(
                    peticion.VersionNombre, peticion.VersionCodigo, peticion.RutaApk, peticion.VersionMinimaApi, peticion.Notas, ct);

                if (resultado.CodigoRechazo is not null)
                    return Results.Json(new RechazoPublicarVersion(resultado.CodigoRechazo, resultado.Mensaje ?? ""), statusCode: StatusCodes.Status409Conflict);

                return Results.Ok(new PublicarVersionRespuesta(resultado.VersionAppId!.Value));
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        return app;
    }
}

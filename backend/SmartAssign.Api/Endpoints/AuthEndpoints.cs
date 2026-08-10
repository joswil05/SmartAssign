using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

public record LoginPeticion(string Username, string Password, string DeviceId);
public record RefreshPeticion(string RefreshToken, string DeviceId);
public record PinPeticion(int UsuarioId, string Pin, string DeviceId);
public record LogoutPeticion(string DeviceId);

/// <summary>El ciclo de sesión de D6 / 04 §6.4 expuesto como API.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth").AllowAnonymous();

        grupo.MapPost("/login", async (LoginPeticion peticion, IServicioAutenticacion servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.IniciarSesionAsync(peticion.Username, peticion.Password, peticion.DeviceId, ct);
            return AResultadoHttp(resultado);
        });

        grupo.MapPost("/refresh", async (RefreshPeticion peticion, IServicioAutenticacion servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.RenovarAsync(peticion.RefreshToken, peticion.DeviceId, ct);
            return AResultadoHttp(resultado);
        });

        grupo.MapPost("/pin", async (PinPeticion peticion, IServicioAutenticacion servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.ReentrarConPinAsync(peticion.UsuarioId, peticion.Pin, peticion.DeviceId, ct);
            return AResultadoHttp(resultado);
        });

        // 05_TRD.md §2.3: "GET /auth/me — Rol, nombre y línea vigente".
        // La línea NUNCA viaja en el token (§2.3, ClaimsSmartAssign) — se
        // resuelve aquí, en vivo, en cada petición, desde la única fuente
        // de verdad (Linea.SupervisorActualId). Es lo que permite que el
        // mapa de navegación 02 §1.1 decida <¿Tiene línea asignada?> y
        // <¿Su línea es la L8?> sin que la app elija nunca de una lista.
        app.MapGet("/api/auth/me", async (
                System.Security.Claims.ClaimsPrincipal usuario, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                var rol = usuario.FindFirst(ClaimsSmartAssign.Rol)?.Value ?? "";
                var nombre = usuario.FindFirst(ClaimsSmartAssign.Nombre)?.Value ?? "";

                var linea = await db.Lineas
                    .Where(l => l.SupervisorActualId == usuarioId)
                    .Select(l => new { l.Id, l.Codigo, l.Nombre, l.EsBolson })
                    .SingleOrDefaultAsync(ct);

                return Results.Ok(new { usuarioId, rol, nombre, linea });
            })
            .RequireAuthorization();

        app.MapPost("/api/auth/logout", async (
                LogoutPeticion peticion, System.Security.Claims.ClaimsPrincipal usuario,
                IServicioAutenticacion servicio, CancellationToken ct) =>
            {
                var usuarioIdTexto = usuario.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                if (!int.TryParse(usuarioIdTexto, out var usuarioId)) return Results.Unauthorized();

                await servicio.CerrarSesionAsync(usuarioId, peticion.DeviceId, ct);
                return Results.NoContent();
            })
            .RequireAuthorization();

        return app;
    }

    private static IResult AResultadoHttp(ResultadoLogin r) =>
        r.Exitoso
            ? Results.Ok(new
            {
                usuarioId = r.UsuarioId,
                rol = r.Rol,
                nombre = r.NombreCompleto,
                accessToken = r.AccessToken,
                accessExpiraEn = r.AccessExpiraEn,
                refreshToken = r.RefreshToken,
                refreshExpiraEn = r.RefreshExpiraEn,
            })
            : Results.Json(new { codigo = r.CodigoRechazo }, statusCode: StatusCodes.Status401Unauthorized);
}

using SmartAssign.Application.Autenticacion;

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

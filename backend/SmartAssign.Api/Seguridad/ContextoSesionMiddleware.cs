using SmartAssign.Application.Autenticacion;
using SmartAssign.Application.Seguridad;

namespace SmartAssign.Api.Seguridad;

/// <summary>
/// Traduce el usuario autenticado de la petición a <see cref="IContextoSesionActual"/>
/// para que la capa 3 (RLS, 04 §6.3) tenga qué fijar en la conexión. Debe
/// ejecutarse después de <c>UseAuthentication</c> y antes de que cualquier
/// código toque la base de datos. La línea se resuelve aquí en vivo —
/// nunca se lee del token (§2.3) — con la misma fuente de verdad que usa
/// la capa 2 (<see cref="IAlcanceLineaResolver"/>).
/// </summary>
public class ContextoSesionMiddleware(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext http, IContextoSesionActual contexto, IAlcanceLineaResolver resolver)
    {
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var rol = http.User.FindFirst(ClaimsSmartAssign.Rol)?.Value;
            if (rol is not null)
            {
                byte? lineaId = null;
                if (rol == "supervisor")
                {
                    var usuarioIdTexto = http.User.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
                    if (int.TryParse(usuarioIdTexto, out var usuarioId))
                        lineaId = await resolver.LineaDeSupervisorAsync(usuarioId, http.RequestAborted);
                }

                contexto.Establecer(rol, lineaId);
            }
        }

        await siguiente(http);
    }
}

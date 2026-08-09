using Microsoft.AspNetCore.Authorization;

namespace SmartAssign.Api.Seguridad;

/// <summary>
/// Aplica <see cref="AlcanceLineaRequirement"/> a cualquier endpoint con
/// una ruta <c>{lineaId}</c>, sin repetir la lógica en cada uno — "el
/// filtro de línea aplicado en el repositorio, nunca en el controlador
/// ni en el cliente" (04 §6.3) se traduce aquí como: aplicado una sola
/// vez, en un filtro común a todo endpoint de línea.
/// </summary>
public class AlcanceLineaEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (!http.Request.RouteValues.TryGetValue("lineaId", out var crudo) ||
            !byte.TryParse(crudo?.ToString(), out var lineaId))
        {
            return Results.BadRequest(new { error = "lineaId inválido" });
        }

        var autorizacion = http.RequestServices.GetRequiredService<IAuthorizationService>();
        var resultado = await autorizacion.AuthorizeAsync(http.User, lineaId, "AlcanceLinea");

        return resultado.Succeeded ? await next(context) : Results.Forbid();
    }
}

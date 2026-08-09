using Microsoft.AspNetCore.Authorization;
using SmartAssign.Application.Seguridad;

namespace SmartAssign.Api.Seguridad;

/// <summary>
/// Capa 2 del aislamiento (04 §6.3): filtro obligatorio de alcance,
/// aplicado en un único lugar reutilizable por todo endpoint que declare
/// <see cref="AlcanceLineaRequirement"/> — nunca copiado en cada
/// controlador. Lee <see cref="IContextoSesionActual"/> en vez de volver
/// a resolver la línea: ese contexto ya lo llenó
/// <c>ContextoSesionMiddleware</c> con la misma consulta en vivo contra
/// <c>Linea.SupervisorActualId</c> (§2.3) que exige esta capa, así que
/// reconsultar aquí sería una segunda ida a la base por la misma
/// pregunta dentro de la misma petición.
/// </summary>
public class AlcanceLineaHandler(IContextoSesionActual contexto)
    : AuthorizationHandler<AlcanceLineaRequirement, byte>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AlcanceLineaRequirement requirement, byte lineaId)
    {
        if (contexto.Rol == "coordinador")
        {
            context.Succeed(requirement);
        }
        else if (contexto.Rol == "supervisor" && contexto.LineaId == lineaId)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}

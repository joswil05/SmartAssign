using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Api.TiempoReal;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Application.Seguridad;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Hubs;

/// <summary>
/// UT-E12.1 (docs/PROGRESO.md): concentrador único <c>/hub/planta</c>
/// (05_TRD.md §2.4) — el grupo es lo que garantiza el aislamiento de
/// §2.2 a nivel de transporte:
///
/// | Grupo | Miembros |
/// |---|---|
/// | <c>linea:{id}</c> | El supervisor de esa línea + el Coordinador |
/// | <c>planta</c> | Solo el Coordinador |
/// | <c>bolson</c> | El supervisor de L8 + el Coordinador |
/// | <c>avisos</c> | Todos los supervisores |
///
/// **"El aislamiento se aplica al suscribirse, no al emitir"** (05 §2.4,
/// literal) — por eso este Hub, a propósito, no expone NINGÚN método
/// invocable por el cliente: no hay <c>JoinGroup</c>, no hay ningún
/// camino para pedir un grupo. La única membresía posible es la que
/// asigna el servidor en <see cref="OnConnectedAsync"/>, leyendo la
/// identidad ya autenticada (JWT) contra <c>Linea.SupervisorActualId</c>
/// en vivo — nunca de un valor que mande el cliente. Un cliente
/// manipulado no tiene qué "pedir": la ausencia de API es la garantía,
/// no una validación en tiempo de ejecución.
///
/// La L8 se distingue por <see cref="Domain.Entities.Linea.EsBolson"/>,
/// nunca por su Id (mismo criterio que todo el resto del backend) — un
/// supervisor de L8 entra a <c>bolson</c>, no a <c>linea:8</c>.
///
/// `[Authorize]` reafirma explícitamente lo que la `FallbackPolicy`
/// global de <c>Program.cs</c> ya exigiría por defecto — una conexión
/// sin token válido nunca llega a completar el *handshake*.
///
/// Sin limpieza manual en `OnDisconnectedAsync`: el `DefaultHubLifetimeManager`
/// de SignalR ya olvida las membresías de grupo de una conexión cuando
/// esta se cierra — añadir una limpieza propia sería repetir algo que la
/// librería ya garantiza (R2, sin ceremonia no pedida).
/// </summary>
[Authorize]
public class PlantaHub(SmartAssignDbContext db, IAlcanceLineaResolver alcanceLineaResolver) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var rol = Context.User?.FindFirst(ClaimsSmartAssign.Rol)?.Value;

        if (rol == "coordinador")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NombresDeGrupo.Planta);
            await Groups.AddToGroupAsync(Context.ConnectionId, NombresDeGrupo.Bolson);

            // linea:{id} para cada línea que NO es el Bolsón (nunca se asume el Id 8).
            // Sin Context.ConnectionAborted aquí a propósito: con el transporte
            // LongPolling ese token puede cancelarse al cerrar el primer poll
            // aunque la conexión lógica siga viva, cortando esta consulta a medio
            // camino — el propio OnConnectedAsync ya delimita cuánto puede durar.
            var lineaIds = await db.Lineas
                .Where(l => !l.EsBolson)
                .Select(l => l.Id)
                .ToListAsync();

            foreach (var lineaId in lineaIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, NombresDeGrupo.DeLinea(lineaId));
        }
        else if (rol == "supervisor")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NombresDeGrupo.Avisos);

            var usuarioIdTexto = Context.User?.FindFirst(ClaimsSmartAssign.UsuarioId)?.Value;
            if (int.TryParse(usuarioIdTexto, out var usuarioId))
            {
                var lineaId = await alcanceLineaResolver.LineaDeSupervisorAsync(usuarioId);
                if (lineaId is { } id)
                {
                    var esBolson = await db.Lineas
                        .Where(l => l.Id == id)
                        .Select(l => l.EsBolson)
                        .SingleAsync();

                    await Groups.AddToGroupAsync(Context.ConnectionId, esBolson ? NombresDeGrupo.Bolson : NombresDeGrupo.DeLinea(id));
                }
            }
        }

        await base.OnConnectedAsync();
    }
}

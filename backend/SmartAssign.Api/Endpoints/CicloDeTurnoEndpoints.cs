using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Api.Seguridad;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Application.CicloDeTurno;
using SmartAssign.Application.Tiempo;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>. El ciclo diario completo
/// existía en SQL desde E5 y E14 —planificar (§8.1), confirmar,
/// arrancar (§8.3/§8.4) y cerrar (00 §C13)— pero sin ninguna vía desde la
/// app: correr un turno exigía SQL directo contra la base de planta.
///
/// <b>Quién puede hacer qué.</b> Planificar, confirmar y arrancar son del
/// Coordinador: deciden qué líneas operan el día (00 §G3, "el Coordinador
/// tiene la función de activar o desactivar alguna línea"). Cerrar es de
/// alcance de línea, porque C13 está escrito entero en primera persona de
/// línea — el Coordinador puede cerrar cualquiera por no tener restricción
/// de alcance, y el Supervisor solo la suya; es el mismo filtro que ya usa
/// la malla, no un permiso nuevo.
///
/// <b>El día de operación no se recibe del cliente.</b> Sale de
/// <c>FechaPlanta.Hoy()</c> salvo que se indique explícitamente uno, para
/// que ningún teléfono con el reloj corrido decida a qué día pertenece un
/// turno (00 §C6, y el hallazgo P-01 de esta misma revisión).
/// </summary>
public static class CicloDeTurnoEndpoints
{
    public record PlanificarPeticion(byte LineaId, byte TurnoId, DateOnly? DiaOperacion, int? SkuId, int? SupervisorId);
    public record TurnoPeticion(byte TurnoId, DateOnly? DiaOperacion);
    public record CerrarPeticion(short? JustificacionMotivoId, string? JustificacionTexto);

    public record BloqueoDeCierre(string Tipo, string Detalle);
    public record CierreRespuesta(bool Cerrada, string? CodigoRechazo, string? Mensaje, JsonElement? Bloqueos);

    private static int UsuarioId(ClaimsPrincipal usuario) =>
        int.Parse(usuario.FindFirstValue(ClaimsSmartAssign.UsuarioId)!);

    public static IEndpointRouteBuilder MapCicloDeTurnoEndpoints(this IEndpointRouteBuilder app)
    {
        // §8.1 — el Coordinador arma el día: qué línea, con qué SKU y quién la supervisa.
        app.MapPost("/api/jornadas/planificar", async (
                PlanificarPeticion peticion, IServicioCicloDeTurno servicio,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var resultado = await servicio.PlanificarLineaAsync(
                    peticion.LineaId, peticion.TurnoId, peticion.DiaOperacion ?? FechaPlanta.Hoy(),
                    peticion.SkuId, peticion.SupervisorId, UsuarioId(usuario), ct);

                return resultado.CodigoRechazo is { } codigo
                    ? Results.Conflict(new { codigoRechazo = codigo })
                    : Results.Ok(new { jornadaLineaId = resultado.JornadaLineaId });
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        // E5.4 — rechazo NOMINAL: nombra las líneas sin supervisor, nunca un "no se pudo".
        app.MapPost("/api/jornadas/confirmar", async (
                TurnoPeticion peticion, IServicioCicloDeTurno servicio,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var resultado = await servicio.ConfirmarAsync(
                    peticion.TurnoId, peticion.DiaOperacion ?? FechaPlanta.Hoy(), UsuarioId(usuario), ct);

                return resultado.CodigoRechazo is { } codigo
                    ? Results.Conflict(new { codigoRechazo = codigo, lineasSinSupervisor = resultado.LineasSinSupervisor })
                    : Results.Ok(new { confirmada = true });
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        // §8.3/§8.4 — barrido de puestos fijos y apertura de la ventana de arranque.
        app.MapPost("/api/jornadas/arrancar", async (
                TurnoPeticion peticion, IServicioCicloDeTurno servicio,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var resultado = await servicio.ArrancarAsync(
                    peticion.TurnoId, peticion.DiaOperacion ?? FechaPlanta.Hoy(), UsuarioId(usuario), ct);

                return resultado.CodigoRechazo is { } codigo
                    ? Results.Conflict(new { codigoRechazo = codigo })
                    : Results.Ok(new { arrancada = true });
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        // 00 §C13 — devuelve la LISTA EXACTA de bloqueos, no un rechazo
        // genérico. Repetir la llamada con justificación fuerza el cierre
        // (00 §A6), que es el mismo mecanismo que ya construyó E14.2.
        app.MapPost("/api/jornadas/{jornadaLineaId:int}/cerrar", async (
                int jornadaLineaId, CerrarPeticion? peticion, IServicioCicloDeTurno servicio,
                SmartAssignDbContext db, IAuthorizationService autorizador,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                // Alcance: el Supervisor solo cierra la jornada de su línea.
                // Se resuelve contra la línea de la jornada, no contra nada
                // que venga en el token (§2.3).
                var lineaId = await db.JornadasLinea
                    .Where(j => j.Id == jornadaLineaId)
                    .Select(j => (byte?)j.LineaId)
                    .SingleOrDefaultAsync(ct);

                if (lineaId is null) return Results.NotFound();

                // Mismo alcance que la malla (04 §6.2/§6.3), resuelto sobre
                // la línea de la jornada: el Coordinador no tiene
                // restricción, el Supervisor solo la suya. No se usa un
                // filtro de ruta porque aquí la línea no viaja en la URL —
                // se deduce de la jornada, que es lo correcto: el cliente
                // no debería poder afirmar de qué línea es una jornada.
                var autorizacion = await autorizador.AuthorizeAsync(usuario, lineaId.Value, "AlcanceLinea");
                if (!autorizacion.Succeeded) return Results.Forbid();

                var resultado = await servicio.CerrarTurnoAsync(
                    jornadaLineaId, UsuarioId(usuario),
                    peticion?.JustificacionMotivoId, peticion?.JustificacionTexto, ct);

                JsonElement? bloqueos = resultado.BloqueosJson is { Length: > 0 } json
                    ? JsonDocument.Parse(json).RootElement.Clone()
                    : null;

                var respuesta = new CierreRespuesta(
                    resultado.CodigoRechazo is null, resultado.CodigoRechazo, resultado.Mensaje, bloqueos);

                return resultado.CodigoRechazo is null
                    ? Results.Ok(respuesta)
                    : Results.Conflict(respuesta);
            })
            .RequireAuthorization();

        return app;
    }
}

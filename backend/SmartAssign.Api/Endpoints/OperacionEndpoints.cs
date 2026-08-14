using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SmartAssign.Api.Seguridad;
using SmartAssign.Application.Operacion;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>, segunda mitad. Movimiento
/// entre líneas (E8), relevos (E9), contingencias (E10) y producción (E11):
/// todo el motor estaba construido y probado, y nada de esto se podía
/// invocar desde un teléfono.
///
/// <b>El alcance se resuelve desde la base, nunca desde lo que afirme el
/// cliente.</b> Lo que llega por la ruta es un puesto, un movimiento o un
/// lote; de qué línea son lo dice la base (ver <see cref="AlcanceDeRecurso"/>).
/// Cuando el recurso no está al alcance responde 404 y no 403: las tablas
/// llevan RLS, así que para el supervisor de otra línea no existe, y un 403
/// confirmaría que sí.
///
/// <b>Rechazo estructurado, nunca genérico</b> (§1.3, §12.4): cada respuesta
/// de conflicto lleva su <c>codigoRechazo</c> y el mensaje que compuso el
/// procedimiento, que es quien conoce la regla.
/// </summary>
public static class OperacionEndpoints
{
    // ── Peticiones ──────────────────────────────────────────────────────
    public record DespacharPeticion(
        byte LineaDestino, string Motivo, int? PuestoDestinoId,
        short? JustificacionMotivoId, string? JustificacionTexto);

    public record RechazarRecepcionPeticion(short? MotivoRechazoId, string? NotaRechazo);
    public record RechazarPropuestaPeticion(int PersonalId);
    public record CerrarLotePeticion(decimal ProduccionReal, decimal DanoOrigen, decimal DanoProceso, string? Justificacion);
    public record CambiarSkuPeticion(int SkuNuevoId);
    public record JustificacionPeticion(short? JustificacionMotivoId, string? JustificacionTexto);
    public record PrioridadPeticion(byte OrdenNuevo);

    private static IResult Responder(string? codigo, string? mensaje, object exito) =>
        codigo is null ? Results.Ok(exito) : Results.Conflict(new { codigoRechazo = codigo, mensaje });

    public static IEndpointRouteBuilder MapOperacionEndpoints(this IEndpointRouteBuilder app)
    {
        // ══ Movimiento entre líneas (§10) ═══════════════════════════════

        // §10.1 — despachar. El alcance es la línea DESTINO: quien manda a
        // alguien a otra línea tiene que poder actuar sobre ese destino.
        app.MapPost("/api/personal/{personalId:int}/despachar", async (
                int personalId, DespacharPeticion peticion, IServicioOperacion servicio,
                IAuthorizationService autorizador, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(autorizador, usuario, peticion.LineaDestino);
                if (veto is not null) return veto;

                var r = await servicio.DespacharAsync(
                    personalId, peticion.LineaDestino, peticion.Motivo, usuario.UsuarioId(),
                    peticion.PuestoDestinoId, peticion.JustificacionMotivoId, peticion.JustificacionTexto, ct);

                return Responder(r.CodigoRechazo, r.Mensaje, new { movimientoId = r.MovimientoId });
            })
            .RequireAuthorization();

        // §10.2 — recepción individual, persona por persona (00 §C8: nunca
        // en bloque). El aviso de línea en paro NO es un rechazo: la persona
        // se recibe igual, pero quien la recibe tiene que saberlo.
        app.MapPost("/api/movimientos/{movimientoId:long}/recibir", async (
                long movimientoId, IServicioOperacion servicio, SmartAssignDbContext db,
                IAuthorizationService autorizador, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDestinoDeMovimientoAsync(movimientoId, ct));
                if (veto is not null) return veto;

                var r = await servicio.RecibirAsync(movimientoId, usuario.UsuarioId(), ct);

                return Responder(r.CodigoRechazo, r.Mensaje,
                    new { recibida = true, destinoEnParo = r.DestinoEnParo, aviso = r.AvisoLineaEnParo });
            })
            .RequireAuthorization();

        app.MapPost("/api/movimientos/{movimientoId:long}/rechazar", async (
                long movimientoId, RechazarRecepcionPeticion? peticion, IServicioOperacion servicio,
                SmartAssignDbContext db, IAuthorizationService autorizador,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDestinoDeMovimientoAsync(movimientoId, ct));
                if (veto is not null) return veto;

                var r = await servicio.RechazarRecepcionAsync(
                    movimientoId, usuario.UsuarioId(), peticion?.MotivoRechazoId, peticion?.NotaRechazo, ct);

                return Responder(r.CodigoRechazo, r.Mensaje, new { rechazada = true });
            })
            .RequireAuthorization();

        // ══ Relevos (§9.4) ══════════════════════════════════════════════

        // Paso 1 en su forma manual: el supervisor pide el relevo sin
        // esperar al umbral. El automático lo abre BarridosDelMotorService.
        app.MapPost("/api/puestos/{puestoId:int}/solicitar-relevo", async (
                int puestoId, IServicioOperacion servicio, SmartAssignDbContext db,
                IAuthorizationService autorizador, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDePuestoAsync(puestoId, ct));
                if (veto is not null) return veto;

                var r = await servicio.SolicitarRelevoAsync(puestoId, usuario.UsuarioId(), ct);
                return Responder(r.CodigoRechazo, r.Mensaje, new { solicitudId = r.SolicitudId });
            })
            .RequireAuthorization();

        // Paso 2: a quién propone la L8. Es una CONSULTA, no compromete
        // nada — por eso GET: se puede pedir dos veces sin efectos.
        app.MapGet("/api/puestos/{puestoId:int}/relevista-propuesto", async (
                int puestoId, IServicioOperacion servicio, SmartAssignDbContext db,
                IAuthorizationService autorizador, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDePuestoAsync(puestoId, ct));
                if (veto is not null) return veto;

                var r = await servicio.ProponerRelevistaAsync(puestoId, ct);

                return Responder(r.CodigoRechazo, r.Mensaje,
                    new { candidatoId = r.CandidatoId, cedePerfil = r.CedePerfil });
            })
            .RequireAuthorization();

        app.MapPost("/api/relevos/{solicitudId:long}/aceptar", async (
                long solicitudId, IServicioOperacion servicio, SmartAssignDbContext db,
                IAuthorizationService autorizador, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await LineaDeSolicitudAsync(db, solicitudId, ct));
                if (veto is not null) return veto;

                var r = await servicio.AceptarRelevoAsync(solicitudId, usuario.UsuarioId(), ct);

                return Responder(r.CodigoRechazo, r.Mensaje,
                    new { candidatoId = r.CandidatoId, movimientoId = r.MovimientoId });
            })
            .RequireAuthorization();

        // Paso 3: rechazar al propuesto lo manda a la lista de descartados
        // de esa jornada (00 §B10), para que no vuelva a proponerse.
        app.MapPost("/api/relevos/{solicitudId:long}/rechazar", async (
                long solicitudId, RechazarPropuestaPeticion peticion, IServicioOperacion servicio,
                SmartAssignDbContext db, IAuthorizationService autorizador,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await LineaDeSolicitudAsync(db, solicitudId, ct));
                if (veto is not null) return veto;

                var r = await servicio.RechazarPropuestaAsync(
                    solicitudId, peticion.PersonalId, usuario.UsuarioId(), ct);

                return Responder(r.CodigoRechazo, r.Mensaje, new { rechazada = true });
            })
            .RequireAuthorization();

        // A dónde mandar a quien acaba de ser relevado (§9.5).
        app.MapGet("/api/personal/{personalId:int}/destino-tras-relevo", async (
                int personalId, byte lineaActual, IServicioOperacion servicio,
                IAuthorizationService autorizador, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(autorizador, usuario, lineaActual);
                if (veto is not null) return veto;

                var r = await servicio.SugerirDestinoRelevadoAsync(personalId, lineaActual, ct);

                return Responder(r.CodigoRechazo, r.Mensaje,
                    new { puestoSugeridoId = r.PuestoSugeridoId, lineaSugerida = r.LineaSugerida });
            })
            .RequireAuthorization();

        // Sacar a alguien de la lista de descartados antes de que caduque.
        app.MapDelete("/api/relevos/descartados/{descarteId:long}", async (
                long descarteId, IServicioOperacion servicio, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var r = await servicio.LimpiarDescartadoAsync(descarteId, usuario.UsuarioId(), ct);
                return Responder(r.CodigoRechazo, r.Mensaje, new { limpiado = true });
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        // ══ Producción (§11) ════════════════════════════════════════════

        app.MapPost("/api/lotes/{loteId:int}/cerrar", async (
                int loteId, CerrarLotePeticion peticion, IServicioOperacion servicio,
                SmartAssignDbContext db, IAuthorizationService autorizador,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDeLoteAsync(loteId, ct));
                if (veto is not null) return veto;

                var r = await servicio.CerrarLoteAsync(
                    loteId, peticion.ProduccionReal, peticion.DanoOrigen, peticion.DanoProceso,
                    peticion.Justificacion, usuario.UsuarioId(), ct);

                return Responder(r.CodigoRechazo, r.Mensaje, new { desperdicioId = r.DesperdicioId });
            })
            .RequireAuthorization();

        // §11.2 — cambiar de producto activa unos puestos y saca otros de
        // operación. Los dos contadores son lo que hace visible el cambio.
        app.MapPost("/api/jornadas/{jornadaLineaId:int}/sku", async (
                int jornadaLineaId, CambiarSkuPeticion peticion, IServicioOperacion servicio,
                SmartAssignDbContext db, IAuthorizationService autorizador,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDeJornadaAsync(jornadaLineaId, ct));
                if (veto is not null) return veto;

                var r = await servicio.CambiarSkuAsync(jornadaLineaId, peticion.SkuNuevoId, usuario.UsuarioId(), ct);

                return Responder(r.CodigoRechazo, r.Mensaje, new
                {
                    loteNuevoId = r.LoteNuevoId,
                    puestosActivados = r.PuestosActivados,
                    puestosDesactivados = r.PuestosDesactivados,
                });
            })
            .RequireAuthorization();

        // ══ Contingencias (§8.6, 00 §C1) ════════════════════════════════

        // Ambas pueden tener que forzar el piso de seguridad (00 §B5), y
        // entonces exigen justificación — el mecanismo de A6 que ya
        // construyó E10.5. Repetir la llamada con justificación es la vía.
        app.MapPost("/api/puestos/{puestoId:int}/extraccion-inversa", async (
                int puestoId, JustificacionPeticion? peticion, IServicioOperacion servicio,
                SmartAssignDbContext db, IAuthorizationService autorizador,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDePuestoAsync(puestoId, ct));
                if (veto is not null) return veto;

                var r = await servicio.ExtraccionInversaAsync(
                    puestoId, usuario.UsuarioId(), peticion?.JustificacionMotivoId, peticion?.JustificacionTexto, ct);

                return Responder(r.CodigoRechazo, r.Mensaje, new
                {
                    candidatoId = r.CandidatoId, lineaOrigen = r.LineaOrigen, movimientoId = r.MovimientoId,
                });
            })
            .RequireAuthorization();

        app.MapPost("/api/puestos/{puestoId:int}/cubrir-vacante-critica", async (
                int puestoId, JustificacionPeticion? peticion, IServicioOperacion servicio,
                SmartAssignDbContext db, IAuthorizationService autorizador,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var veto = await AlcanceDeRecurso.ComprobarAsync(
                    autorizador, usuario, await db.LineaDePuestoAsync(puestoId, ct));
                if (veto is not null) return veto;

                var r = await servicio.CubrirVacanteCriticaAsync(
                    puestoId, usuario.UsuarioId(), peticion?.JustificacionMotivoId, peticion?.JustificacionTexto, ct);

                // nivelAplicado dice por cuál escalón se resolvió: quien
                // decide necesita saber si se cubrió barato o hubo que
                // extraer un Operador B de otra línea.
                return Responder(r.CodigoRechazo, r.Mensaje, new
                {
                    nivelAplicado = r.NivelAplicado, candidatoId = r.CandidatoId,
                    lineaOrigen = r.LineaOrigen, solicitudId = r.SolicitudId, movimientoId = r.MovimientoId,
                });
            })
            .RequireAuthorization();

        // 00 §C1 — el titular vuelve y recupera su puesto; el suplente que
        // queda liberado sale con una sugerencia de destino, no al aire.
        app.MapPost("/api/personal/{titularId:int}/reincorporar", async (
                int titularId, IServicioOperacion servicio, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var r = await servicio.ReincorporarTitularAsync(titularId, usuario.UsuarioId(), ct);

                return Responder(r.CodigoRechazo, r.Mensaje, new
                {
                    puestoId = r.PuestoId,
                    suplenteLiberadoId = r.SuplenteLiberadoId,
                    asignacionId = r.AsignacionId,
                    lineaSugeridaSuplente = r.LineaSugeridaSuplente,
                    puestoSugeridoSuplente = r.PuestoSugeridoSuplente,
                });
            })
            .RequireAuthorization();

        app.MapPost("/api/personal/{personalId:int}/finalizar-retiro-temporal", async (
                int personalId, IServicioOperacion servicio, ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var r = await servicio.FinalizarRetiroTemporalAsync(personalId, usuario.UsuarioId(), ct);
                return Responder(r.CodigoRechazo, r.Mensaje, new { finalizado = true });
            })
            .RequireAuthorization();

        // 00 §B8 — la jerarquía de líneas es del Coordinador: cambiarla
        // reordena a quién se le extrae gente primero en toda la planta.
        app.MapPut("/api/lineas/{lineaId:int}/prioridad", async (
                byte lineaId, PrioridadPeticion peticion, IServicioOperacion servicio,
                ClaimsPrincipal usuario, CancellationToken ct) =>
            {
                var r = await servicio.CambiarPrioridadAsync(lineaId, peticion.OrdenNuevo, usuario.UsuarioId(), ct);
                return Responder(r.CodigoRechazo, r.Mensaje, new { prioridad = peticion.OrdenNuevo });
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        return app;
    }

    /// <summary>La línea del puesto al que pertenece la solicitud de relevo.</summary>
    private static async Task<byte?> LineaDeSolicitudAsync(
        SmartAssignDbContext db, long solicitudId, CancellationToken ct)
    {
        var puestoId = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(db.SolicitudesRelevo.Where(s => s.Id == solicitudId).Select(s => (int?)s.PuestoId), ct);

        return puestoId is null ? null : await db.LineaDePuestoAsync(puestoId.Value, ct);
    }
}

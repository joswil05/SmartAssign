namespace SmartAssign.Application.Operacion;

// ── Movimiento entre líneas (etapa E8, §10) ─────────────────────────────

/// <summary>Espejo de <c>sp_DespacharPersona</c> (E8.1/E8.5).</summary>
public record ResultadoDespacho(long? MovimientoId, string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Espejo de <c>sp_RecibirPersona</c> (E8.2). <c>AvisoLineaEnParo</c> no es
/// un rechazo: la persona SÍ se recibe, pero el supervisor tiene que saber
/// que la línea a la que llega está parada (00 §C8).
/// </summary>
public record ResultadoRecepcion(bool DestinoEnParo, string? AvisoLineaEnParo, string? CodigoRechazo, string? Mensaje);

/// <summary>Espejo de <c>sp_RechazarRecepcion</c> (E8.3).</summary>
public record ResultadoRechazoRecepcion(string? CodigoRechazo, string? Mensaje);

// ── Relevos (etapa E9, §9.4) ────────────────────────────────────────────

/// <summary>Espejo de <c>sp_MarcarRelevoSolicitado</c> (E9.1).</summary>
public record ResultadoSolicitudRelevo(long? SolicitudId, string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Espejo de <c>sp_ProponerRelevista</c> (E9.5). <c>CedePerfil</c> marca que
/// el candidato entra cediendo el perfil preferente (§8.5 niveles 2 y 4) —
/// la única regla que cede, y quien decide tiene que verlo.
/// </summary>
public record ResultadoPropuesta(int? CandidatoId, bool CedePerfil, string? CodigoRechazo, string? Mensaje);

/// <summary>Espejo de <c>sp_AceptarRelevo</c> (E9.6).</summary>
public record ResultadoAceptacion(int? CandidatoId, long? MovimientoId, string? CodigoRechazo, string? Mensaje);

/// <summary>Espejo de <c>sp_SugerirDestinoRelevado</c> (E9.7).</summary>
public record ResultadoDestinoRelevado(int? PuestoSugeridoId, byte? LineaSugerida, string? CodigoRechazo, string? Mensaje);

// ── Producción y contingencias (etapas E10 y E11) ───────────────────────

/// <summary>Espejo de <c>sp_CerrarLote</c> (E11.3, §11.3).</summary>
public record ResultadoCierreLote(int? DesperdicioId, string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Espejo de <c>sp_CambiarSKU</c> (E11.4, §11.2). Los dos contadores son el
/// dato que hace visible el cambio: cuántos puestos entran en operación y
/// cuántos salen al cambiar de producto (§5.3).
/// </summary>
public record ResultadoCambioSku(
    int? LoteNuevoId, int PuestosActivados, int PuestosDesactivados, string? CodigoRechazo, string? Mensaje);

/// <summary>Espejo de <c>sp_ExtraccionInversa</c> (E10.3, §8.6).</summary>
public record ResultadoExtraccion(
    int? CandidatoId, byte? LineaOrigen, long? MovimientoId, string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Espejo de <c>sp_CubrirVacanteCritica</c> (E10.4, 00 §C1).
/// <c>NivelAplicado</c> dice por cuál de los niveles de la escalera se
/// resolvió — quien decide necesita saber si se cubrió por lo barato o
/// hubo que llegar a extraer un Operador B de otra línea.
/// </summary>
public record ResultadoVacanteCritica(
    string? NivelAplicado, int? CandidatoId, byte? LineaOrigen, long? SolicitudId, long? MovimientoId,
    string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Espejo de <c>sp_ReincorporarTitular</c> (E10.6, 00 §C1: "el titular
/// vuelve a su puesto"). Devuelve además a dónde mandar al suplente que
/// queda liberado, para no dejarlo sin sitio.
/// </summary>
public record ResultadoReincorporacion(
    int? PuestoId, int? SuplenteLiberadoId, long? AsignacionId,
    byte? LineaSugeridaSuplente, int? PuestoSugeridoSuplente, string? CodigoRechazo, string? Mensaje);

/// <summary>Rechazo simple: los procedimientos que solo dicen si pudieron o no.</summary>
public record ResultadoSimple(string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>, segunda mitad. El motor de
/// movimiento (E8), relevos (E9), contingencias (E10) y producción (E11)
/// estaba construido y probado, pero <b>sin ninguna vía desde la app</b>:
/// 23 procedimientos que solo se podían invocar con SQL directo contra la
/// base de planta.
///
/// Fachada delgada, igual que <c>IServicioAsignacion</c> y
/// <c>IServicioCicloDeTurno</c>: la lógica de negocio sigue viviendo entera
/// en los procedimientos. Esto solo la hace alcanzable desde un teléfono.
/// </summary>
public interface IServicioOperacion
{
    // Movimiento
    Task<ResultadoDespacho> DespacharAsync(
        int personalId, byte lineaDestino, string motivo, int usuarioId, int? puestoDestinoId,
        short? justificacionMotivoId, string? justificacionTexto, CancellationToken ct = default);

    Task<ResultadoRecepcion> RecibirAsync(long movimientoId, int usuarioId, CancellationToken ct = default);

    Task<ResultadoRechazoRecepcion> RechazarRecepcionAsync(
        long movimientoId, int usuarioId, short? motivoRechazoId, string? notaRechazo, CancellationToken ct = default);

    // Relevos
    Task<ResultadoSolicitudRelevo> SolicitarRelevoAsync(int puestoId, int usuarioId, CancellationToken ct = default);
    Task<ResultadoPropuesta> ProponerRelevistaAsync(int puestoId, CancellationToken ct = default);
    Task<ResultadoAceptacion> AceptarRelevoAsync(long solicitudId, int usuarioId, CancellationToken ct = default);
    Task<ResultadoSimple> RechazarPropuestaAsync(long solicitudId, int personalId, int usuarioId, CancellationToken ct = default);
    Task<ResultadoDestinoRelevado> SugerirDestinoRelevadoAsync(int personalId, byte lineaActual, CancellationToken ct = default);
    Task<ResultadoSimple> LimpiarDescartadoAsync(long descarteId, int usuarioId, CancellationToken ct = default);

    // Producción
    Task<ResultadoCierreLote> CerrarLoteAsync(
        int loteId, decimal produccionReal, decimal danoOrigen, decimal danoProceso,
        string? justificacion, int usuarioId, CancellationToken ct = default);

    Task<ResultadoCambioSku> CambiarSkuAsync(int jornadaLineaId, int skuNuevoId, int usuarioId, CancellationToken ct = default);

    // Contingencias
    Task<ResultadoExtraccion> ExtraccionInversaAsync(
        int puestoSolicitanteId, int usuarioId, short? justificacionMotivoId, string? justificacionTexto,
        CancellationToken ct = default);

    Task<ResultadoVacanteCritica> CubrirVacanteCriticaAsync(
        int puestoVacanteId, int usuarioId, short? justificacionMotivoId, string? justificacionTexto,
        CancellationToken ct = default);

    Task<ResultadoReincorporacion> ReincorporarTitularAsync(int titularId, int usuarioId, CancellationToken ct = default);
    Task<ResultadoSimple> FinalizarRetiroTemporalAsync(int personalId, int usuarioId, CancellationToken ct = default);
    Task<ResultadoSimple> CambiarPrioridadAsync(byte lineaId, byte ordenNuevo, int usuarioId, CancellationToken ct = default);
}

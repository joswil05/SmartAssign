using SmartAssign.Application.Operacion;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Procedimientos;

namespace SmartAssign.Infrastructure.Operacion;

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>, segunda mitad. Los 16
/// procedimientos de movimiento, relevos, contingencias y producción que no
/// tenían vía desde la app.
///
/// Todo pasa por <see cref="EjecutorDeProcedimiento"/>, que abre la conexión
/// por el pipeline de EF para no perder
/// <c>SessionContextConnectionInterceptor</c> — sin él la RLS esconde las
/// filas y el procedimiento trabaja sobre una base vacía sin dar error.
/// </summary>
public class ServicioOperacion(SmartAssignDbContext db) : IServicioOperacion
{
    private readonly EjecutorDeProcedimiento _ejecutor = new(db);

    // ── Movimiento entre líneas (E8, §10) ───────────────────────────────

    public async Task<ResultadoDespacho> DespacharAsync(
        int personalId, byte lineaDestino, string motivo, int usuarioId, int? puestoDestinoId,
        short? justificacionMotivoId, string? justificacionTexto, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_DespacharPersona", new Dictionary<string, object?>
        {
            ["personal_id"] = personalId,
            ["linea_destino"] = lineaDestino,
            ["motivo"] = motivo,
            ["usuario_id"] = usuarioId,
            ["puesto_destino_id"] = puestoDestinoId,
            ["justificacion_motivo_id"] = justificacionMotivoId,
            ["justificacion_texto"] = justificacionTexto,
        }, [Salida.Largo("movimiento_id"), .. Salida.Rechazo], ct);

        return new ResultadoDespacho(s.Largo("movimiento_id"), s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoRecepcion> RecibirAsync(long movimientoId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_RecibirPersona", new Dictionary<string, object?>
        {
            ["movimiento_id"] = movimientoId,
            ["usuario_id"] = usuarioId,
        }, [Salida.Bit("destino_en_paro"), Salida.Texto("aviso_linea_en_paro", 200), .. Salida.Rechazo], ct);

        return new ResultadoRecepcion(
            s.Bit("destino_en_paro"), s.Texto("aviso_linea_en_paro"),
            s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoRechazoRecepcion> RechazarRecepcionAsync(
        long movimientoId, int usuarioId, short? motivoRechazoId, string? notaRechazo, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_RechazarRecepcion", new Dictionary<string, object?>
        {
            ["movimiento_id"] = movimientoId,
            ["usuario_id"] = usuarioId,
            ["motivo_rechazo_id"] = motivoRechazoId,
            ["nota_rechazo"] = notaRechazo,
        }, Salida.Rechazo, ct);

        return new ResultadoRechazoRecepcion(s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    // ── Relevos (E9, §9.4) ──────────────────────────────────────────────

    public async Task<ResultadoSolicitudRelevo> SolicitarRelevoAsync(int puestoId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_MarcarRelevoSolicitado", new Dictionary<string, object?>
        {
            ["puesto_id"] = puestoId,
            ["usuario_id"] = usuarioId,
        }, [Salida.Largo("solicitud_id"), .. Salida.Rechazo], ct);

        return new ResultadoSolicitudRelevo(s.Largo("solicitud_id"), s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoPropuesta> ProponerRelevistaAsync(int puestoId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_ProponerRelevista", new Dictionary<string, object?>
        {
            ["puesto_id"] = puestoId,
        }, [Salida.Entero("candidato_id"), Salida.Bit("cede_perfil"), .. Salida.Rechazo], ct);

        return new ResultadoPropuesta(
            s.Entero("candidato_id"), s.Bit("cede_perfil"), s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoAceptacion> AceptarRelevoAsync(long solicitudId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_AceptarRelevo", new Dictionary<string, object?>
        {
            ["solicitud_id"] = solicitudId,
            ["usuario_id"] = usuarioId,
        }, [Salida.Entero("candidato_id"), Salida.Largo("movimiento_id"), .. Salida.Rechazo], ct);

        return new ResultadoAceptacion(
            s.Entero("candidato_id"), s.Largo("movimiento_id"), s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoSimple> RechazarPropuestaAsync(
        long solicitudId, int personalId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_RechazarPropuestaRelevo", new Dictionary<string, object?>
        {
            ["solicitud_id"] = solicitudId,
            ["personal_id"] = personalId,
            ["usuario_id"] = usuarioId,
        }, Salida.Rechazo, ct);

        return new ResultadoSimple(s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoDestinoRelevado> SugerirDestinoRelevadoAsync(
        int personalId, byte lineaActual, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_SugerirDestinoRelevado", new Dictionary<string, object?>
        {
            ["personal_id"] = personalId,
            ["linea_actual"] = lineaActual,
        }, [Salida.Entero("puesto_id_sugerido"), Salida.Byte("linea_sugerida"), .. Salida.Rechazo], ct);

        return new ResultadoDestinoRelevado(
            s.Entero("puesto_id_sugerido"), s.Byte("linea_sugerida"),
            s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoSimple> LimpiarDescartadoAsync(long descarteId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_LimpiarDescartado", new Dictionary<string, object?>
        {
            ["descarte_id"] = descarteId,
            ["usuario_id"] = usuarioId,
        }, Salida.Rechazo, ct);

        return new ResultadoSimple(s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    // ── Producción (E11) ────────────────────────────────────────────────

    public async Task<ResultadoCierreLote> CerrarLoteAsync(
        int loteId, decimal produccionReal, decimal danoOrigen, decimal danoProceso,
        string? justificacion, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_CerrarLote", new Dictionary<string, object?>
        {
            ["lote_id"] = loteId,
            ["produccion_real"] = produccionReal,
            ["dano_origen"] = danoOrigen,
            ["dano_proceso"] = danoProceso,
            ["justificacion"] = justificacion,
            ["usuario_id"] = usuarioId,
        }, [Salida.Entero("desperdicio_id"), .. Salida.Rechazo], ct);

        return new ResultadoCierreLote(s.Entero("desperdicio_id"), s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoCambioSku> CambiarSkuAsync(
        int jornadaLineaId, int skuNuevoId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_CambiarSKU", new Dictionary<string, object?>
        {
            ["jornada_linea_id"] = jornadaLineaId,
            ["sku_nuevo_id"] = skuNuevoId,
            ["usuario_id"] = usuarioId,
        }, [Salida.Entero("lote_nuevo_id"), Salida.Entero("puestos_activados"),
            Salida.Entero("puestos_desactivados"), .. Salida.Rechazo], ct);

        return new ResultadoCambioSku(
            s.Entero("lote_nuevo_id"), s.Entero("puestos_activados") ?? 0,
            s.Entero("puestos_desactivados") ?? 0, s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    // ── Contingencias (E10) ─────────────────────────────────────────────

    public async Task<ResultadoExtraccion> ExtraccionInversaAsync(
        int puestoSolicitanteId, int usuarioId, short? justificacionMotivoId, string? justificacionTexto,
        CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_ExtraccionInversa", new Dictionary<string, object?>
        {
            ["puesto_id_solicitante"] = puestoSolicitanteId,
            ["usuario_id"] = usuarioId,
            ["justificacion_motivo_id"] = justificacionMotivoId,
            ["justificacion_texto"] = justificacionTexto,
        }, [Salida.Entero("candidato_id"), Salida.Byte("linea_origen"),
            Salida.Largo("movimiento_id"), .. Salida.Rechazo], ct);

        return new ResultadoExtraccion(
            s.Entero("candidato_id"), s.Byte("linea_origen"), s.Largo("movimiento_id"),
            s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoVacanteCritica> CubrirVacanteCriticaAsync(
        int puestoVacanteId, int usuarioId, short? justificacionMotivoId, string? justificacionTexto,
        CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_CubrirVacanteCritica", new Dictionary<string, object?>
        {
            ["puesto_id_vacante"] = puestoVacanteId,
            ["usuario_id"] = usuarioId,
            ["justificacion_motivo_id"] = justificacionMotivoId,
            ["justificacion_texto"] = justificacionTexto,
        }, [Salida.Texto("nivel_aplicado", 2), Salida.Entero("candidato_id"), Salida.Byte("linea_origen"),
            Salida.Largo("solicitud_id"), Salida.Largo("movimiento_id"), .. Salida.Rechazo], ct);

        return new ResultadoVacanteCritica(
            s.Texto("nivel_aplicado"), s.Entero("candidato_id"), s.Byte("linea_origen"),
            s.Largo("solicitud_id"), s.Largo("movimiento_id"),
            s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoReincorporacion> ReincorporarTitularAsync(
        int titularId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_ReincorporarTitular", new Dictionary<string, object?>
        {
            ["titular_id"] = titularId,
            ["usuario_id"] = usuarioId,
        }, [Salida.Entero("puesto_id"), Salida.Entero("suplente_liberado_id"), Salida.Largo("asignacion_id"),
            Salida.Byte("linea_sugerida_suplente"), Salida.Entero("puesto_sugerido_suplente"), .. Salida.Rechazo], ct);

        return new ResultadoReincorporacion(
            s.Entero("puesto_id"), s.Entero("suplente_liberado_id"), s.Largo("asignacion_id"),
            s.Byte("linea_sugerida_suplente"), s.Entero("puesto_sugerido_suplente"),
            s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoSimple> FinalizarRetiroTemporalAsync(
        int personalId, int usuarioId, CancellationToken ct = default)
    {
        var s = await _ejecutor.EjecutarAsync("dbo.sp_FinalizarRetiroTemporal", new Dictionary<string, object?>
        {
            ["personal_id"] = personalId,
            ["usuario_id"] = usuarioId,
        }, Salida.Rechazo, ct);

        return new ResultadoSimple(s.Texto("codigo_rechazo"), s.Texto("mensaje"));
    }

    public async Task<ResultadoSimple> CambiarPrioridadAsync(
        byte lineaId, byte ordenNuevo, int usuarioId, CancellationToken ct = default)
    {
        // Único del grupo sin @mensaje: sp_CambiarPrioridadLinea (E5.2) solo
        // devuelve el código. No se inventa un texto que el procedimiento no
        // produce (§1.3, honestidad del dato).
        var s = await _ejecutor.EjecutarAsync("dbo.sp_CambiarPrioridadLinea", new Dictionary<string, object?>
        {
            ["linea_id"] = lineaId,
            ["orden_nuevo"] = ordenNuevo,
            ["usuario_id"] = usuarioId,
        }, [Salida.Texto("codigo_rechazo", 40)], ct);

        return new ResultadoSimple(s.Texto("codigo_rechazo"), null);
    }
}

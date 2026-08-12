namespace SmartAssign.Domain.Entities;

/// <summary>
/// Despacho, tránsito y recepción entre líneas — Parte X, 04 §5.2.
/// Proceso de tres pasos con confirmación, porque implica un
/// desplazamiento físico real: el despacho (E8.1) solo abre la fila;
/// la persona queda <c>en_transito</c> hasta que el supervisor destino
/// confirma que llegó de verdad (E8.3) o la rechaza (E8.4).
/// </summary>
public class Movimiento
{
    public long Id { get; set; }
    public int PersonalId { get; set; }
    public byte LineaOrigen { get; set; }
    public byte LineaDestino { get; set; }

    /// <summary>Puesto reservado para este relevista (§9.4 p3) — nulo hasta que exista esa mecánica (E8.5).</summary>
    public int? PuestoDestinoId { get; set; }

    /// <summary>relevo | reasignacion_relevado | liberacion_bolson | paro | cambio_sku | linea_inactiva | rechazo_recepcion | intervencion_coordinador | cobertura_vacante_critica (CK_Mov_motivo).</summary>
    public string Motivo { get; set; } = default!;

    /// <summary>en_transito | recibido | rechazado | cancelado (CK_Mov_estado).</summary>
    public string Estado { get; set; } = "en_transito";

    /// <summary>00 §12.7: hora exacta de salida — no solo el resultado final.</summary>
    public DateTime HoraSalida { get; set; }

    public DateTime? HoraLlegada { get; set; }

    /// <summary>Columna calculada persistida (DATEDIFF SECOND) — la razón de ser de §12.7: materia prima para calibrar B11/A1 con datos reales.</summary>
    public int? DuracionSeg { get; private set; }

    public int DespachadoPor { get; set; }
    public int? RecibidoPor { get; set; }

    /// <summary>Obligatorio si Estado='rechazado' (CK_Mov_rechazo, C10) — nunca un canal silencioso para esquivar relevos.</summary>
    public short? MotivoRechazoId { get; set; }

    public string? NotaRechazo { get; set; }

    /// <summary>B11 — sp_CaducarTransitos (E8.6) lo marca, no lo borra.</summary>
    public DateTime? CaducadoEn { get; set; }

    public int? CanceladoPor { get; set; }
    public long? JustificacionId { get; set; }

    public Personal? Personal { get; set; }
    public Linea? Origen { get; set; }
    public Linea? Destino { get; set; }
    public Puesto? PuestoDestino { get; set; }
    public Usuario? Despachante { get; set; }
    public Usuario? Receptor { get; set; }
    public MotivoRechazoRecepcion? MotivoRechazo { get; set; }
    public Usuario? Cancelante { get; set; }
    public JustificacionExcepcion? Justificacion { get; set; }
}

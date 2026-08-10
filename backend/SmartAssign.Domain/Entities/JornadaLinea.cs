namespace SmartAssign.Domain.Entities;

/// <summary>
/// El turno de una línea en un día de operación (§8, 04 §4.1).
///
/// <b>Completa desde la etapa E5</b> — la versión mínima de la etapa E4
/// (<see cref="LineaId"/>, <see cref="ArrancadoEn"/>,
/// <see cref="VentanaArranqueFin"/>, <see cref="CerradoEn"/>) se amplía
/// aquí con el resto del diseño de 04 §4.1 (<see cref="TurnoId"/>,
/// <see cref="DiaOperacion"/>, <see cref="SkuId"/>,
/// <see cref="SupervisorId"/>, <see cref="Estado"/>,
/// <see cref="CerradoForzadoPor"/>) — mismo patrón ya usado con
/// <c>Puesto</c> y <c>Personal</c> en E3. Ver docs/PROGRESO.md, notas de
/// ingeniería E4 y E5.
/// </summary>
public class JornadaLinea
{
    public int Id { get; set; }
    public byte LineaId { get; set; }
    public byte TurnoId { get; set; }

    /// <summary>Fecha de inicio del turno (00 §C6) — no la fecha calendario del cierre si cruza medianoche.</summary>
    public DateOnly DiaOperacion { get; set; }

    /// <summary>NULL = línea inactiva ese día/turno (§8.1). No nulo = línea activa con ese SKU.</summary>
    public int? SkuId { get; set; }

    /// <summary>
    /// Supervisor planificado para ESTA jornada (§8.1 paso 2) — registro
    /// histórico de la planificación. Distinto de <see cref="Linea.SupervisorActualId"/>,
    /// que es la fuente de verdad en vivo para el aislamiento (RLS, 04 §6.1,
    /// D6); <c>sp_ConfirmarPlanificacion</c> (E5.4) sincroniza ambos al confirmar.
    /// </summary>
    public int? SupervisorId { get; set; }

    /// <summary>planificada | confirmada | arrancada | cerrada (04 §4.1).</summary>
    public string Estado { get; set; } = "planificada";

    public DateTime? ArrancadoEn { get; set; }

    /// <summary>
    /// Momento en que cierra la ventana de arranque local aislado (§8.4).
    /// Se calcula UNA vez, al arrancar, sumando el parámetro configurable
    /// <c>ventana_arranque_min</c> (04 §9) — nunca se recalcula en cada
    /// validación. Nulo mientras la jornada no ha arrancado.
    /// </summary>
    public DateTime? VentanaArranqueFin { get; set; }

    public DateTime? CerradoEn { get; set; }

    /// <summary>Solo si el cierre fue forzado con bloqueos pendientes (00 §C13, A6) — llega en E14.</summary>
    public int? CerradoForzadoPor { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public Linea? Linea { get; set; }
    public Turno? Turno { get; set; }
    public Sku? Sku { get; set; }
    public Usuario? Supervisor { get; set; }
    public Usuario? CerradoPor { get; set; }
}

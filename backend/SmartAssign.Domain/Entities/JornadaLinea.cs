namespace SmartAssign.Domain.Entities;

/// <summary>
/// El turno de una línea en un día de operación (§8, 04 §4.1).
///
/// <b>Versión mínima, deliberadamente incompleta.</b> El motor de
/// validación (etapa E4) solo necesita saber, de la jornada ABIERTA de una
/// línea, cuándo termina su ventana de arranque (§8.4) — por eso esta
/// entidad trae únicamente <see cref="LineaId"/>, <see cref="ArrancadoEn"/>,
/// <see cref="VentanaArranqueFin"/> y <see cref="CerradoEn"/>. El resto del
/// diseño ya completo en docs/04_ESQUEMA_BACKEND.md §4.1
/// (<c>turno_id</c>, <c>dia_operacion</c>, <c>sku_id</c>,
/// <c>supervisor_id</c>, <c>estado</c>...) se añade con <c>ALTER TABLE</c>
/// en la etapa E5, cuando <c>Turno</c> y el resto del ciclo de vida de la
/// jornada se construyen — mismo patrón ya usado con <c>Puesto</c> y
/// <c>Personal</c> en E3. Ver docs/PROGRESO.md, notas de ingeniería E4.
/// </summary>
public class JornadaLinea
{
    public int Id { get; set; }
    public byte LineaId { get; set; }

    public DateTime? ArrancadoEn { get; set; }

    /// <summary>
    /// Momento en que cierra la ventana de arranque local aislado (§8.4).
    /// Se calcula UNA vez, al arrancar, sumando el parámetro configurable
    /// <c>ventana_arranque_min</c> (04 §9) — nunca se recalcula en cada
    /// validación. Nulo mientras la jornada no ha arrancado.
    /// </summary>
    public DateTime? VentanaArranqueFin { get; set; }

    public DateTime? CerradoEn { get; set; }
    public byte[] RowVersion { get; set; } = default!;

    public Linea? Linea { get; set; }
}

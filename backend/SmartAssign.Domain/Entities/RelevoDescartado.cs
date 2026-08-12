namespace SmartAssign.Domain.Entities;

/// <summary>
/// Ciclo de vida de la lista de descartados — 00 §B10, 04 §5.3. Del par
/// (puesto, persona), nunca de la persona en general: un rechazo puntual
/// para un puesto concreto no debe vetar a nadie en ningún otro lado.
/// <c>JornadaDia</c> es la caducidad automática al cierre de turno — el
/// mismo <c>DiaOperacion</c> de <see cref="JornadaLinea"/>, no un
/// calendario crudo (§C6: un turno nocturno cruza medianoche sin que eso
/// cambie de "día de turno"). No se borra, se cierra con
/// <see cref="LimpiadoEn"/>, para que quede constancia de que existió el
/// veto (B10: "que nadie quede vetado en silencio").
/// </summary>
public class RelevoDescartado
{
    public long Id { get; set; }
    public int PuestoId { get; set; }
    public int PersonalId { get; set; }
    public DateOnly JornadaDia { get; set; }
    public int DescartadoPor { get; set; }
    public DateTime DescartadoEn { get; set; }
    public DateTime? LimpiadoEn { get; set; }
    public int? LimpiadoPor { get; set; }

    public Puesto? Puesto { get; set; }
    public Personal? Personal { get; set; }
    public Usuario? Descartante { get; set; }
    public Usuario? Limpiador { get; set; }
}

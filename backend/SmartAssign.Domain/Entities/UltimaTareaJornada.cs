namespace SmartAssign.Domain.Entities;

/// <summary>
/// El último puesto ocupado por cada persona al cerrar su jornada
/// trabajada (00 §B6, §7.4, 04 §3.4) — la referencia exacta de la regla de
/// no repetición: "la que esa persona hizo al cerrar su jornada anterior",
/// nunca "el día calendario anterior". Se escribe al cierre de turno
/// (00 §C13, etapa E14); esta entidad existe ya en E4 porque
/// <c>fn_ViolaNoRepeticion24h</c> necesita esta tabla para poder leerla.
/// </summary>
public class UltimaTareaJornada
{
    public int PersonalId { get; set; }
    public short TipoActividadId { get; set; }
    public int PuestoId { get; set; }

    /// <summary>Fecha de inicio del turno (00 §C6), no la fecha calendario — un turno que cruza medianoche pertenece entero a su día de arranque.</summary>
    public DateOnly DiaOperacion { get; set; }

    public DateTime RegistradoEn { get; set; }

    public Personal? Personal { get; set; }
    public TipoActividad? TipoActividad { get; set; }
    public Puesto? Puesto { get; set; }
}

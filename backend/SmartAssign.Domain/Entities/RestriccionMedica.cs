namespace SmartAssign.Domain.Entities;

/// <summary>
/// `[SEGURIDAD DE DATOS]` (§7.2, 00 §C14). Enfermería no es usuario del
/// sistema en el MVP — el Coordinador transcribe (§2.1.6), y
/// <see cref="Fuente"/>/<see cref="FechaDictamen"/> existen para que el
/// origen en papel quede rastreable.
///
/// <b>Nunca se borra.</b> Una restricción que deja de aplicar se cierra
/// con <see cref="FechaFin"/> — borrar historial médico rompe la
/// auditabilidad de una regla de seguridad ocupacional (§12.7). La cuenta
/// de aplicación tiene DELETE denegado sobre esta tabla a nivel de base
/// de datos (04 §7.5) — ver la migración de esta etapa.
/// </summary>
public class RestriccionMedica
{
    public int Id { get; set; }
    public int PersonalId { get; set; }
    public short CapacidadId { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>NULL = permanente (00 §C14).</summary>
    public DateOnly? FechaFin { get; set; }

    /// <summary>Quién la dictó en Enfermería — el registro en papel que el Coordinador transcribe.</summary>
    public string Fuente { get; set; } = default!;
    public DateOnly FechaDictamen { get; set; }
    public string? Observacion { get; set; }

    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }

    public Personal Personal { get; set; } = default!;
    public CapacidadFisica Capacidad { get; set; } = default!;
}

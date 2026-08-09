namespace SmartAssign.Domain.Entities;

/// <summary>
/// Alimenta el estado `ausente_justificado` de <see cref="Personal"/>, que
/// es `[REGLA DURA]`: "Quien está ausente justificado NUNCA puede ser
/// asignado. Sin excepciones" (§6.1, 04 §3.3). Viene de la hoja real
/// "Personal ausente" del cliente (07 §4.1).
/// </summary>
public class AusenciaJustificada
{
    public int Id { get; set; }
    public int PersonalId { get; set; }

    /// <summary>vacaciones | permiso | cita_medica | subsidio | accidente_laboral | otro (04 §3.3).</summary>
    public string Tipo { get; set; } = default!;

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    public int RegistradoPor { get; set; }

    public Personal Personal { get; set; } = default!;
}

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

    /// <summary>
    /// "real" | "simulado" (07 §4.4). La hoja "Personal ausente" del cliente
    /// importa 'real'; la ausencia que la semilla adversaria fabrica para
    /// forzar la vacante crítica (C1) es 'simulado' y la purga previa a
    /// producción la borra — ver sp_PurgarDatosSimulados (UT-E14.7).
    /// </summary>
    public string OrigenDato { get; set; } = "real";

    public Personal Personal { get; set; } = default!;
}

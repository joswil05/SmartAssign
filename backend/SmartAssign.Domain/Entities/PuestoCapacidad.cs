namespace SmartAssign.Domain.Entities;

/// <summary>
/// Qué capacidades físicas EXIGE un puesto (§7.2). Junto con
/// <see cref="RestriccionMedica"/>, es el vocabulario compartido completo
/// de la regla médica: "cada persona tiene registradas las capacidades
/// que tiene prohibidas, cada puesto declara las que exige; si hay
/// coincidencia, la asignación se deniega" (04 §2.7).
/// </summary>
public class PuestoCapacidad
{
    public int PuestoId { get; set; }
    public short CapacidadId { get; set; }

    public Puesto Puesto { get; set; } = default!;
    public CapacidadFisica Capacidad { get; set; } = default!;
}

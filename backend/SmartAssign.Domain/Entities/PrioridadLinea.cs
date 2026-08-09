namespace SmartAssign.Domain.Entities;

/// <summary>
/// Prioridad de líneas, versionada — nunca se sobrescribe (B8: los cambios
/// solo aplican hacia adelante). Solo gobierna el barrido de asignación
/// inicial y la extracción inversa (A9) — el motor de relevos NUNCA la usa.
/// Ver docs/04_ESQUEMA_BACKEND.md §2.2.
/// </summary>
public class PrioridadLinea
{
    public int Id { get; set; }
    public byte LineaId { get; set; }

    /// <summary>1 = máxima prioridad.</summary>
    public byte Orden { get; set; }

    public DateTime VigenteDesde { get; set; }

    /// <summary>Nulo = es la fila vigente actualmente.</summary>
    public DateTime? VigenteHasta { get; set; }

    /// <summary>FK a Usuario(Id) — ver nota de <see cref="Linea.SupervisorActualId"/>.</summary>
    public int CambiadoPor { get; set; }

    public Linea Linea { get; set; } = default!;
}

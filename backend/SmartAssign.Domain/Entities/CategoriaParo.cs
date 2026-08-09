namespace SmartAssign.Domain.Entities;

/// <summary>Categoría general de un paro técnico — mecánico, eléctrico, calidad, falta de material (§11.1).</summary>
public class CategoriaParo
{
    public short Id { get; set; }
    public string Nombre { get; set; } = default!;
    public bool Activo { get; set; } = true;

    public ICollection<CausaParo> Causas { get; set; } = new List<CausaParo>();
}

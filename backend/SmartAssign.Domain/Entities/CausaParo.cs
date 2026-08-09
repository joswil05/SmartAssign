namespace SmartAssign.Domain.Entities;

/// <summary>Causa concreta de un paro, filtrada por su categoría (§11.1). Datos reales del cliente: se cargarán en la etapa E11 (dato D10 del plan).</summary>
public class CausaParo
{
    public short Id { get; set; }
    public short CategoriaId { get; set; }
    public string Nombre { get; set; } = default!;
    public bool Activo { get; set; } = true;

    public CategoriaParo Categoria { get; set; } = default!;
}

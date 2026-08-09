namespace SmartAssign.Domain.Entities;

/// <summary>
/// Catálogo de producto. El ritmo teórico nunca es un valor fijo en código
/// (§11.4): siempre sale de aquí. En el archivo real del cliente corresponde
/// a la columna "Velocidad" del programa de producción.
/// Ver docs/04_ESQUEMA_BACKEND.md §2.5.
/// </summary>
public class Sku
{
    public int Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Descripcion { get; set; } = default!;

    /// <summary>Unidades por hora — §11.4, §12.6.</summary>
    public decimal RitmoTeoricoHora { get; set; }

    public bool Activo { get; set; } = true;
}

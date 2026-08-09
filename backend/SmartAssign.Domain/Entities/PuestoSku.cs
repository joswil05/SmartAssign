namespace SmartAssign.Domain.Entities;

/// <summary>
/// Qué puestos requiere cada SKU. Es lo que hace computable "fuera de
/// operación" (§5.3): un puesto está fuera de operación si su línea no
/// está activa, o si no tiene fila aquí para el SKU del lote vigente.
/// Ver docs/04_ESQUEMA_BACKEND.md §2.5.
/// </summary>
public class PuestoSku
{
    public int PuestoId { get; set; }
    public int SkuId { get; set; }

    public Puesto Puesto { get; set; } = default!;
    public Sku Sku { get; set; } = default!;
}

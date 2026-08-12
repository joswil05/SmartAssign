namespace SmartAssign.Domain.Entities;

/// <summary>
/// Lote de producción (00 §C5): ( línea, SKU, turno, apertura, cierre ).
/// Abre al arrancar el turno o tras un cambio de SKU (§11.2); cierra con
/// la acción explícita "Cerrar lote" (§11.3, C4). Varios lotes por
/// turno están permitidos — <see cref="Numero"/> los distingue dentro
/// de la misma jornada-línea. Ver docs/04_ESQUEMA_BACKEND.md §4.2.
/// </summary>
public class Lote
{
    public int Id { get; set; }
    public int JornadaLineaId { get; set; }
    public int SkuId { get; set; }

    /// <summary>1, 2, 3... dentro de la jornada — nunca global.</summary>
    public short Numero { get; set; }

    public DateTime AbiertoEn { get; set; }

    /// <summary>NULL mientras el lote sigue abierto.</summary>
    public DateTime? CerradoEn { get; set; }

    /// <summary>Producción real capturada al cierre (00 §C4) — nula hasta entonces.</summary>
    public decimal? ProduccionReal { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public JornadaLinea JornadaLinea { get; set; } = default!;
    public Sku Sku { get; set; } = default!;
}

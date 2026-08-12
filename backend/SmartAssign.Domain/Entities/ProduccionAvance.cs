namespace SmartAssign.Domain.Entities;

/// <summary>
/// Avance parcial de producción durante un lote todavía abierto — "un
/// contador simple" (00 §C4) que alimenta la lectura en vivo de eficiencia
/// (§11.4) sin esperar al cierre del lote.
///
/// Al cerrar el lote, el número oficial pasa a ser <see cref="Lote.ProduccionReal"/>
/// — los avances de ESE lote dejan de sumarse (<c>sp_CalcularEficiencia</c>
/// solo lee <c>ProduccionAvance</c> del lote que sigue abierto, para no
/// contar dos veces la misma producción). Ver docs/04_ESQUEMA_BACKEND.md §4.3.
/// </summary>
public class ProduccionAvance
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public decimal Cantidad { get; set; }
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }

    public Lote Lote { get; set; } = default!;
    public Usuario Registrante { get; set; } = default!;
}

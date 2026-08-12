namespace SmartAssign.Domain.Entities;

/// <summary>
/// Desperdicio registrado al cierre de un lote (§11.3) — separado por
/// causa porque cada una apunta a un responsable distinto: daño de
/// ORIGEN (proveedor, almacén, transporte) vs. daño de PROCESO
/// (maquinaria, normalmente descalibración).
///
/// <see cref="Justificacion"/> es NULL salvo que el daño de proceso
/// supere el umbral configurable (`Parametro['umbral_desperdicio_justificacion_pct']`,
/// 04 §9) — validado en <c>sp_CerrarLote</c>, no aquí: el contexto no
/// decide reglas de negocio (05_TRD.md §1.4). Ver docs/04_ESQUEMA_BACKEND.md §4.3.
/// </summary>
public class Desperdicio
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public decimal DanoOrigen { get; set; }
    public decimal DanoProceso { get; set; }
    public string? Justificacion { get; set; }
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }

    public Lote Lote { get; set; } = default!;
    public Usuario Registrante { get; set; } = default!;
}

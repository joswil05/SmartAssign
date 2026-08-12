namespace SmartAssign.Domain.Entities;

/// <summary>
/// La bandeja de salida (05_TRD.md §4.1, patrón "Bandeja de salida"):
/// "garantiza que el evento se emite si y solo si la transacción
/// confirmó". Un procedimiento que registra un cambio de negocio inserta
/// aquí, dentro de SU MISMA transacción (vía <c>sp_EncolarEvento</c>) —
/// si esa transacción revierte, la fila nunca existió; si confirma, la
/// fila queda ahí para que <c>EventoSalienteDispatcher</c> (Api) la
/// entregue a <c>PlantaHub</c> por su cuenta, en un momento posterior.
///
/// <see cref="Grupos"/> es la lista de nombres de grupo REALES (CSV,
/// p. ej. <c>"linea:4,planta"</c>), ya resuelta al escribir — la misma
/// cadena que <c>NombresDeGrupo</c>/<c>CatalogoEventos.NombresDeGrupoPara</c>
/// (E12.2) producen, para que Hub, catálogo y bandeja nunca diverjan.
/// <see cref="ProcesadoEn"/> NULL = pendiente de entrega.
/// </summary>
public class EventoSaliente
{
    public long Id { get; set; }
    public string TipoEvento { get; set; } = default!;
    public string Grupos { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTime CreadoEn { get; set; }
    public DateTime? ProcesadoEn { get; set; }
}

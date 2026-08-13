namespace SmartAssign.Application.Historico;

/// <summary>
/// Espejo de las salidas de <c>sp_CalcularEficiencia</c> (E11.5/E11.7,
/// §11.4) — mismos campos que ya usan los paneles en vivo (E11.8), sin
/// invención de forma nueva. <c>EficienciaPct</c>/<c>Tramo</c> nulos son
/// datos honestos (§1.3, honestidad del dato): tiempo efectivo cero o
/// umbrales sin sembrar, nunca un 0 % o una clasificación inventados.
/// </summary>
public record ResultadoEficienciaHistorica(
    decimal? EficienciaPct, string? Tramo, decimal ProduccionReal,
    int TiempoEfectivoMarchaMin, decimal? RitmoTeoricoHora, int ParosAcumuladosMin,
    DateTime? UltimaActualizacionProduccion, string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Fachada delgada sobre <c>sp_CalcularEficiencia</c> para el histórico
/// (E14.3, §2.1.11) — misma lógica de negocio en SQL, ya construida y
/// verificada en E11.5/E11.7/E11.8; esta interfaz solo la hace
/// invocable desde una jornada YA CERRADA en vez de la línea en vivo.
/// </summary>
public interface IServicioHistorico
{
    Task<ResultadoEficienciaHistorica> CalcularEficienciaAsync(int jornadaLineaId, CancellationToken ct = default);
}

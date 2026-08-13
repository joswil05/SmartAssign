namespace SmartAssign.Application.VersionesApp;

/// <summary>Espejo de las salidas de <c>sp_PublicarVersionApp</c> (E14.6, 00 §F3, 04 §10.1).</summary>
public record ResultadoPublicarVersion(int? VersionAppId, string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Fachada delgada sobre <c>sp_PublicarVersionApp</c> — la lógica de
/// negocio (único código de versión, transición atómica de "vigente")
/// vive en SQL; esta interfaz solo la hace invocable desde la Api.
/// Mismo patrón que <c>IServicioAsignacion</c>/<c>IServicioHistorico</c>.
/// </summary>
public interface IServicioVersionApp
{
    Task<ResultadoPublicarVersion> PublicarVersionAsync(
        string versionNombre, int versionCodigo, string rutaApk, int versionMinimaApi, string? notas,
        CancellationToken ct = default);
}

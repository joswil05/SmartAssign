namespace SmartAssign.Domain.Entities;

/// <summary>
/// Una versión publicada del APK (00 §F3, 04 §10.1). El APK vive en el
/// propio servidor de planta, servido por la misma API — no Play Store,
/// no Firebase App Distribution, no MDM. Esquema literal de
/// 04_ESQUEMA_BACKEND.md §10.1.
/// </summary>
public class VersionApp
{
    public int Id { get; set; }

    /// <summary>Ej. "1.4.2" — informativa, para mostrar al usuario.</summary>
    public string VersionNombre { get; set; } = default!;

    /// <summary>Entero incremental — el mismo que <c>versionCode</c> del APK, comparable.</summary>
    public int VersionCodigo { get; set; }

    /// <summary>Ruta en el servidor donde vive el archivo del APK — nunca expuesta directo al cliente, solo vía el endpoint de descarga.</summary>
    public string RutaApk { get; set; } = default!;

    /// <summary>
    /// Rompe compatibilidad por debajo de este código (Anexo §3). La app
    /// solo se bloquea si SU PROPIO <c>versionCode</c> queda por debajo
    /// de este valor — nunca se fuerza a actualizar solo porque exista
    /// una versión más nueva.
    /// </summary>
    public int VersionMinimaApi { get; set; }

    public string? Notas { get; set; }

    public DateTime PublicadaEn { get; set; }

    /// <summary>Solo una fila puede tener este valor en true a la vez (UX_VersionApp_vigente).</summary>
    public bool Vigente { get; set; } = true;
}

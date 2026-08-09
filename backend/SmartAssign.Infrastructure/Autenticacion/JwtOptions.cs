namespace SmartAssign.Infrastructure.Autenticacion;

/// <summary>
/// Configuración del ciclo de tokens (D6, 04 §6.4). La clave de firma
/// vive en configuración (user-secrets en desarrollo, Key Vault o
/// equivalente en producción) — nunca en el código ni en el repositorio.
/// </summary>
public class JwtOptions
{
    public const string Seccion = "Jwt";

    public string Emisor { get; set; } = "SmartAssign";
    public string Audiencia { get; set; } = "SmartAssign.App";
    public string ClaveSecreta { get; set; } = default!;

    /// <summary>04 §6.4: access token de 15 minutos.</summary>
    public int AccessMinutos { get; set; } = 15;

    /// <summary>04 §6.4: refresh token de 12 horas, ligado al dispositivo.</summary>
    public int RefreshHoras { get; set; } = 12;
}

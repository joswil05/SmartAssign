namespace SmartAssign.Domain.Entities;

/// <summary>
/// Token de mensajería de un dispositivo (D5, 04 §10). Identifica un
/// teléfono, no a una persona de la plantilla — es el único dato que sale
/// hacia el servicio de mensajería (§12.1): la campana está vacía, el
/// contenido real se descarga por HTTPS desde el propio servidor.
/// </summary>
public class DispositivoPush
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string DeviceId { get; set; } = default!;
    public string PushToken { get; set; } = default!;

    /// <summary>Hoy solo "android" (04 §10).</summary>
    public string Plataforma { get; set; } = "android";

    public DateTime RegistradoEn { get; set; }
    public DateTime? RevocadoEn { get; set; }

    public Usuario Usuario { get; set; } = default!;
}

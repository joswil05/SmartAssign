namespace SmartAssign.Api.Notificaciones;

/// <summary>
/// El único punto de contacto con Firebase Cloud Messaging (D5). Envía
/// exclusivamente <see cref="PingFcm"/> — nunca contenido de negocio —
/// a UN dispositivo (identificado por su <c>push_token</c>, nunca por
/// identidad de persona, 04 §10).
/// </summary>
public interface IServicioNotificacionesPush
{
    /// <summary>
    /// Devuelve si el envío se completó DE VERDAD contra un canal real.
    /// Nunca debe devolver <c>true</c> sin haberlo intentado — de eso
    /// depende que <c>NotificacionDispatcher</c> marque
    /// <c>Notificacion.EntregadaEn</c> con honestidad (§12.4): "sí o sí"
    /// (D5) es verificable solo si el servidor nunca miente sobre si
    /// entregó o no.
    /// </summary>
    Task<bool> EnviarAsync(string pushToken, PingFcm ping, CancellationToken ct);
}

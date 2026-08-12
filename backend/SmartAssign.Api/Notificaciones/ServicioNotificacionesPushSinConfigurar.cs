namespace SmartAssign.Api.Notificaciones;

/// <summary>
/// Sin credenciales reales de Firebase Admin SDK todavía — mismo hueco
/// exacto que D6/AD (<c>ServicioAutenticacion</c>): ningún documento del
/// cliente trae el proyecto de Firebase ni su clave de servicio, y la
/// Wi-Fi de planta ni siquiera tiene confirmada salida a internet
/// (⚠ PENDIENTE-E5, 05 §2.5) — inventar esas credenciales violaría la
/// cláusula de no invención (07 §2, regla R2).
///
/// A diferencia del origen "ad" (que RECHAZA de forma explícita porque
/// bloquea una operación síncrona iniciada por un usuario), aquí no hay
/// nada que rechazar: <c>NotificacionDispatcher</c> corre en segundo
/// plano, sin nadie esperando una respuesta HTTP. La honestidad del dato
/// (§12.4) exige lo mismo igual — nunca fingir que el ping salió. Por
/// eso devuelve <c>false</c> siempre: la fila de <c>Notificacion</c>
/// queda pendiente (<c>EntregadaEn</c> sin tocar) y se reintenta en el
/// siguiente sondeo, en vez de marcar "entregada" algo que nunca llegó a
/// ningún teléfono. Queda lista para el adaptador real —
/// <c>FirebaseAdmin.Messaging</c>— apenas el cliente entregue el
/// proyecto de Firebase.
/// </summary>
public class ServicioNotificacionesPushSinConfigurar(ILogger<ServicioNotificacionesPushSinConfigurar> logger)
    : IServicioNotificacionesPush
{
    public Task<bool> EnviarAsync(string pushToken, PingFcm ping, CancellationToken ct)
    {
        logger.LogWarning(
            "FCM sin configurar todavía (sin credenciales de Firebase del cliente) — " +
            "el ping de la notificación {NotificacionId} no salió de verdad hacia ningún teléfono.",
            ping.E);
        return Task.FromResult(false);
    }
}

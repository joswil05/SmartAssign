using System.Collections.Concurrent;
using SmartAssign.Api.Notificaciones;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E12.4: reemplaza a <c>ServicioNotificacionesPushSinConfigurar</c>
/// SOLO en <see cref="SmartAssignApiFactory"/> — no hay credenciales
/// reales de Firebase en CI, así que esta suite necesita un doble que sí
/// "entregue" para poder observar el mecanismo completo de punta a
/// punta. A diferencia del stub real, siempre devuelve <c>true</c> y
/// registra cada envío para que las pruebas lo inspeccionen.
/// </summary>
public class ServicioNotificacionesPushDeCaptura : IServicioNotificacionesPush
{
    public ConcurrentBag<(string PushToken, PingFcm Ping)> Enviados { get; } = new();

    public Task<bool> EnviarAsync(string pushToken, PingFcm ping, CancellationToken ct)
    {
        Enviados.Add((pushToken, ping));
        return Task.FromResult(true);
    }
}

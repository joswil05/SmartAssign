using Microsoft.EntityFrameworkCore;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Notificaciones;

/// <summary>
/// UT-E12.4: el lado de ENVÍO de D5/05 §2.5 — sondea <c>Notificacion</c>
/// por filas sin <c>EntregadaEn</c> y le manda la campana vacía
/// (<see cref="PingFcm"/>) a cada <c>DispositivoPush</c> activo del
/// destinatario. Mismo patrón exacto que <c>EventoSalienteDispatcher</c>
/// (E12.3): vive fuera de cualquier transacción de negocio a propósito —
/// la garantía transaccional ("se encola si y solo si la transacción
/// confirmó") ya la dio <c>sp_EncolarNotificacion</c> al escribir; este
/// servicio solo drena lo que ya quedó confirmado.
///
/// <c>EntregadaEn</c> solo se marca si <see cref="IServicioNotificacionesPush.EnviarAsync"/>
/// devuelve <c>true</c> para AL MENOS un dispositivo activo — honestidad
/// del dato (§12.4): sin eso, la fila queda pendiente y se reintenta en
/// el siguiente sondeo en vez de inventar una entrega que no ocurrió. Un
/// usuario sin ningún <c>DispositivoPush</c> activo (nunca registró un
/// token, o lo revocó al cerrar sesión) se queda igual sin entregar —
/// hueco conocido que E12.6 resuelve por el lado de escalado, no por
/// inventar aquí un destino que no existe.
/// </summary>
public class NotificacionDispatcher(
    IServiceScopeFactory scopeFactory, IServicioNotificacionesPush push, ILogger<NotificacionDispatcher> logger)
    : BackgroundService
{
    private static readonly TimeSpan IntervaloDeSondeo = TimeSpan.FromSeconds(1);
    private const int LoteMaximo = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DespacharPendientesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un fallo de envío no debe tumbar el proceso ni dejar de
                // reintentar en el próximo sondeo — la fila sigue pendiente
                // (EntregadaEn sin tocar) hasta que se entregue de verdad.
                logger.LogError(ex, "Fallo despachando Notificacion");
            }

            try
            {
                await Task.Delay(IntervaloDeSondeo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Apagado normal del host.
            }
        }
    }

    internal async Task DespacharPendientesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var pendientes = await db.Notificaciones
            .Where(n => n.EntregadaEn == null)
            .OrderBy(n => n.Id)
            .Take(LoteMaximo)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return;

        var usuarioIds = pendientes.Select(n => n.UsuarioId).Distinct().ToList();
        var dispositivosPorUsuario = await db.DispositivosPush
            .Where(d => usuarioIds.Contains(d.UsuarioId) && d.RevocadoEn == null)
            .ToListAsync(ct);

        foreach (var notificacion in pendientes)
        {
            var dispositivos = dispositivosPorUsuario.Where(d => d.UsuarioId == notificacion.UsuarioId);
            var ping = new PingFcm(notificacion.Id.ToString());

            var entregadaAlMenosUna = false;
            foreach (var dispositivo in dispositivos)
                if (await push.EnviarAsync(dispositivo.PushToken, ping, ct))
                    entregadaAlMenosUna = true;

            if (entregadaAlMenosUna)
                notificacion.EntregadaEn = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}

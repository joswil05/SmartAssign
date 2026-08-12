using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Api.Hubs;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.TiempoReal;

/// <summary>
/// UT-E12.3: el lado de LECTURA de la bandeja de salida (05_TRD.md §4.1)
/// — sondea <c>EventoSaliente</c> por filas pendientes y las entrega a
/// <c>PlantaHub</c>. Vive fuera de cualquier transacción de negocio a
/// propósito: la garantía transaccional ("se emite si y solo si la
/// transacción confirmó") ya la dio <c>sp_EncolarEvento</c> AL ESCRIBIR;
/// este servicio solo drena lo que ya quedó confirmado, en su propio
/// tiempo, sin bloquear ninguna operación de negocio.
///
/// Sondeo cada segundo — dato de ingeniería, no de negocio (R2 no
/// aplica): 05 §? fija la propagación en vivo en menos de 2 s (ideal) /
/// 5 s (máximo); sondear cada 1 s deja margen real para el resto del
/// camino (deserializar, `SendAsync`, red) sin acercarse al límite.
///
/// Usa <see cref="IServiceScopeFactory"/> porque <see cref="SmartAssignDbContext"/>
/// es *scoped* y este servicio es *singleton* (mismo patrón que
/// cualquier `BackgroundService` con dependencias con alcance de
/// petición) — un scope nuevo por cada sondeo.
/// </summary>
public class EventoSalienteDispatcher(
    IServiceScopeFactory scopeFactory, IHubContext<PlantaHub> hub, ILogger<EventoSalienteDispatcher> logger)
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
                // Un fallo de entrega no debe tumbar el proceso ni dejar de
                // reintentar en el próximo sondeo — la fila sigue pendiente
                // (procesado_en sin tocar) hasta que se entregue de verdad.
                logger.LogError(ex, "Fallo despachando EventoSaliente");
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

        var pendientes = await db.EventosSalientes
            .Where(e => e.ProcesadoEn == null)
            .OrderBy(e => e.Id)
            .Take(LoteMaximo)
            .ToListAsync(ct);

        foreach (var evento in pendientes)
        {
            using var payload = JsonDocument.Parse(evento.PayloadJson);

            foreach (var grupo in evento.Grupos.Split(',', StringSplitOptions.RemoveEmptyEntries))
                await hub.Clients.Group(grupo).SendAsync(evento.TipoEvento, payload.RootElement.Clone(), ct);

            evento.ProcesadoEn = DateTime.UtcNow;
        }

        if (pendientes.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}

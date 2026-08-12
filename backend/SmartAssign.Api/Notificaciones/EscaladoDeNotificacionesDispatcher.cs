using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Notificaciones;

/// <summary>
/// UT-E12.6: dispara <c>sp_EscalarNotificacionesVencidas</c> por su
/// cuenta — mismo patrón de sondeo que <c>EventoSalienteDispatcher</c>/
/// <c>NotificacionDispatcher</c> (E12.3/E12.4), pero aquí el SP hace todo
/// el trabajo (barrido + <c>UPDATE</c> + <c>sp_EncolarEvento</c>, todo
/// atómico por fila); este servicio solo lo llama a intervalos.
///
/// Sondeo cada segundo, mismo criterio de ingeniería que los otros dos
/// dispatchers de E12 — barato de sobra frente a un timeout medido en
/// minutos (<c>notificacion_acuse_timeout_min</c>).
/// </summary>
public class EscaladoDeNotificacionesDispatcher(
    IServiceScopeFactory scopeFactory, ILogger<EscaladoDeNotificacionesDispatcher> logger)
    : BackgroundService
{
    private static readonly TimeSpan IntervaloDeSondeo = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EscalarVencidasAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Fallo escalando Notificacion vencidas");
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

    internal async Task<int> EscalarVencidasAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var pEscaladas = new SqlParameter("@escaladas", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_EscalarNotificacionesVencidas @escaladas = @escaladas OUTPUT", [pEscaladas], ct);

        return (int)pEscaladas.Value;
    }
}

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Seguridad;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.TiempoReal;

/// <summary>
/// Revisión de producción, hallazgo <b>P-03</b>. <c>sp_DetectarFatiga</c>
/// (E9.1, §9.4 paso 1) y <c>sp_CaducarTransitos</c> (E8.6, 00 §B11)
/// estaban construidos y probados, pero <b>solo los llamaban las
/// pruebas</b>: no había ningún servicio que los ejecutara. En planta eso
/// significa que la fatiga se acumula, cruza el umbral y no pasa nada —el
/// motor de relevos nunca arranca solo— y que una persona despachada y
/// nunca recibida se queda en tránsito para siempre.
///
/// <b>Los dos barridos van juntos pero fallan por separado.</b> Comparten
/// temporizador porque son la misma clase de trabajo (periódico, sin
/// nadie que lo pida), pero cada uno tiene su propio try: que la caducidad
/// de tránsitos falle no puede dejar la planta sin detección de fatiga.
///
/// <b>Cada 30 segundos, no cada segundo.</b> Los otros tres dispatchers de
/// E12 sondean cada segundo porque entregan eventos que el supervisor está
/// esperando en pantalla. Esto no: la fatiga se mide en minutos
/// (<c>horas_en_puesto</c> del dato real, <c>fatiga_*_default_min</c>) y la
/// caducidad de tránsito en 15 minutos por defecto. Medio minuto es
/// imperceptible contra esos umbrales y evita un sondeo constante contra
/// tablas que el barrido de asignación también usa.
///
/// <b>El contexto de sesión no es opcional aquí.</b> Los dos procedimientos
/// leen <c>Puesto</c> y <c>JornadaLinea</c>, que llevan RLS (04 §6.3). Un
/// scope de fondo no pasa por <c>ContextoSesionMiddleware</c>, así que sin
/// fijar el rol a mano el interceptor "cierra en falso", el filtro esconde
/// todas las filas y el barrido informaría de cero fatigados con la planta
/// entera al límite. Es la misma trampa que E14.4 documentó al medir
/// <c>sp_CalcularEficiencia</c>.
/// </summary>
public class BarridosDelMotorService(
    IServiceScopeFactory scopeFactory, ILogger<BarridosDelMotorService> logger, IConfiguration configuracion)
    : BackgroundService
{
    private static readonly TimeSpan IntervaloDeSondeo = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Las pruebas de integración lo apagan: un barrido que abre
    /// solicitudes de relevo por su cuenta cada 30 s competiría con los
    /// escenarios que construyen su propia fatiga a mano. Los barridos se
    /// prueban llamando a los métodos de abajo directamente, que es lo que
    /// de verdad hace el trabajo.
    /// </summary>
    private bool Habilitado => configuracion.GetValue("Barridos:Habilitado", true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Habilitado)
        {
            logger.LogInformation("Barridos del motor deshabilitados por configuración (Barridos:Habilitado=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var fatigados = await DetectarFatigaAsync(stoppingToken);
                if (fatigados > 0)
                    logger.LogInformation("Detección de fatiga: {Abiertas} solicitud(es) de relevo abierta(s).", fatigados);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Fallo detectando fatiga");
            }

            try
            {
                var caducados = await CaducarTransitosAsync(stoppingToken);
                if (caducados > 0)
                    logger.LogInformation("Caducidad de tránsitos: {Caducados} marcado(s).", caducados);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Fallo caducando tránsitos");
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

    /// <summary>§9.4 paso 1: abre una solicitud por cada puesto que cruzó su umbral.</summary>
    public async Task<int> DetectarFatigaAsync(CancellationToken ct) =>
        await EjecutarBarridoAsync("dbo.sp_DetectarFatiga", "@abiertas", ct);

    /// <summary>00 §B11: marca —nunca borra— los tránsitos que pasaron su duración máxima.</summary>
    public async Task<int> CaducarTransitosAsync(CancellationToken ct) =>
        await EjecutarBarridoAsync("dbo.sp_CaducarTransitos", "@caducados", ct);

    private async Task<int> EjecutarBarridoAsync(string procedimiento, string parametro, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        // Alcance sin restricción de línea: un barrido de planta mira las
        // 10 líneas. Tiene que fijarse ANTES de abrir la conexión, porque
        // es el interceptor quien lo traslada a SESSION_CONTEXT.
        scope.ServiceProvider.GetRequiredService<IContextoSesionActual>().Establecer("coordinador", null);

        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var salida = new SqlParameter(parametro, SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync($"EXEC {procedimiento} {parametro} = {parametro} OUTPUT", [salida], ct);

        return salida.Value as int? ?? 0;
    }
}

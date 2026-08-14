namespace SmartAssign.Application.CicloDeTurno;

/// <summary>Espejo de <c>sp_PlanificarLinea</c> (E5.3, §8.1).</summary>
public record ResultadoPlanificacion(int? JornadaLineaId, string? CodigoRechazo);

/// <summary>
/// Espejo de <c>sp_ConfirmarPlanificacion</c> (E5.4). <c>LineasSinSupervisor</c>
/// no es un error de programación: es el rechazo nominal que exige nombrar
/// qué líneas quedaron sin supervisor, en vez de un "no se pudo" genérico.
/// </summary>
public record ResultadoConfirmacion(string? CodigoRechazo, string? LineasSinSupervisor);

/// <summary>Espejo de <c>sp_ArrancarTurno</c> (E5.7, §8.3/§8.4).</summary>
public record ResultadoArranque(string? CodigoRechazo);

/// <summary>
/// Espejo de <c>sp_CerrarTurno</c> (E14.1/E14.2, 00 §C13). <c>Bloqueos</c>
/// llega como JSON con la lista exacta —lote abierto, tránsito entrante,
/// tránsito saliente sin recibir— porque C13 exige nombrarlos uno a uno y
/// nunca un rechazo genérico (§1.3, §12.4).
/// </summary>
public record ResultadoCierre(string? CodigoRechazo, string? Mensaje, string? BloqueosJson);

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>: el ciclo diario entero
/// —planificar, confirmar, arrancar, cerrar— estaba construido y probado
/// en SQL desde E5 y E14, pero <b>sin ninguna vía desde la app</b>. Un
/// turno real no se podía correr de punta a punta desde un teléfono; hacía
/// falta SQL directo contra la base de planta, que es justo lo que un
/// sistema de asignación no puede pedirle a un coordinador.
///
/// Fachada delgada, mismo patrón que <c>IServicioAsignacion</c> (E6.7/E6.8)
/// e <c>IServicioHistorico</c> (E14.3): la lógica de negocio sigue viviendo
/// entera en los procedimientos, esto solo la hace alcanzable.
/// </summary>
public interface IServicioCicloDeTurno
{
    Task<ResultadoPlanificacion> PlanificarLineaAsync(
        byte lineaId, byte turnoId, DateOnly diaOperacion, int? skuId, int? supervisorId,
        int usuarioId, CancellationToken ct = default);

    Task<ResultadoConfirmacion> ConfirmarAsync(
        byte turnoId, DateOnly diaOperacion, int usuarioId, CancellationToken ct = default);

    Task<ResultadoArranque> ArrancarAsync(
        byte turnoId, DateOnly diaOperacion, int usuarioId, CancellationToken ct = default);

    Task<ResultadoCierre> CerrarTurnoAsync(
        int jornadaLineaId, int usuarioId, short? justificacionMotivoId, string? justificacionTexto,
        CancellationToken ct = default);
}

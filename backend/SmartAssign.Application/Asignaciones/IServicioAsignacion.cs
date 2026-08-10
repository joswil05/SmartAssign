namespace SmartAssign.Application.Asignaciones;

/// <summary>Espejo de las salidas de <c>sp_SugerirPuesto</c> (E6.7, §8.5).</summary>
public record ResultadoSugerencia(int? PuestoId, byte? Nivel, string? CodigoRechazo, string? Mensaje);

/// <summary>Espejo de las salidas de <c>sp_AsignarPersona</c> (E6.8, 04 §7.3).</summary>
public record ResultadoAsignacion(long? AsignacionId, string? CodigoRechazo, string? Mensaje);

/// <summary>
/// Fachada delgada sobre los dos procedimientos almacenados de E6.7/E6.8
/// — la lógica de negocio vive en SQL (sp_SugerirPuesto, sp_AsignarPersona),
/// esta interfaz solo la hace invocable desde la Api.
/// </summary>
public interface IServicioAsignacion
{
    Task<ResultadoSugerencia> SugerirPuestoAsync(int personalId, byte lineaId, int usuarioId, CancellationToken ct = default);

    Task<ResultadoAsignacion> AsignarPersonaAsync(
        int personalId, int puestoId, int usuarioId, Guid idempotencyKey, bool cederPerfil, CancellationToken ct = default);
}

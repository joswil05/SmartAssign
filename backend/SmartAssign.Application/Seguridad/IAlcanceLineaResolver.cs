namespace SmartAssign.Application.Seguridad;

/// <summary>
/// Resuelve en vivo qué línea tiene asignada un supervisor, consultando
/// <c>Linea.SupervisorActualId</c> — nunca desde el token (§2.3, 04 §6.4:
/// "resuelve la línea EN VIVO desde Linea.supervisor_actual"). Nulo si el
/// usuario no tiene línea asignada (04 §6.1 nota final).
/// </summary>
public interface IAlcanceLineaResolver
{
    Task<byte?> LineaDeSupervisorAsync(int usuarioId, CancellationToken ct = default);
}

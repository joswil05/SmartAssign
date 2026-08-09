namespace SmartAssign.Application.Trazabilidad;

/// <summary>
/// Traza obligatoria de toda operación (§12.7, 04 §8): "quién la hizo,
/// cuándo, sobre quién y con qué resultado". Se llama tanto en éxito como
/// en rechazo — <b>los rechazos se auditan igual que los éxitos</b>, es lo
/// que permite demostrar que una restricción médica sí impidió lo que
/// debía impedir, no solo que existe.
/// </summary>
public interface IRegistradorAuditoria
{
    Task RegistrarAsync(
        int usuarioId,
        string rol,
        string accion,
        string entidad,
        long? entidadId,
        int? personalId,
        byte? lineaId,
        string resultado,
        string? codigoRechazo,
        string? deviceId,
        CancellationToken ct = default);
}

/// <summary>Valores válidos de <see cref="IRegistradorAuditoria"/>.Resultado (04 §8).</summary>
public static class ResultadoAuditoria
{
    public const string Ok = "OK";
    public const string Rechazo = "RECHAZO";
}

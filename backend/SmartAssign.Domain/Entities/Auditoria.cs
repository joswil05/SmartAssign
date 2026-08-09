namespace SmartAssign.Domain.Entities;

/// <summary>
/// Traza obligatoria de toda operación que mueva a una persona: quién la
/// hizo, cuándo, sobre quién y con qué resultado (§12.7, 04 §8). Los
/// rechazos se auditan igual que los éxitos — un sistema que solo registra
/// lo que sí ocurrió no puede demostrar que impidió lo que no debía
/// ocurrir. La aplicación no tiene permiso de UPDATE/DELETE sobre esta
/// tabla (04 §7.5): el único camino de escritura es sp_RegistrarAuditoria.
/// </summary>
public class Auditoria
{
    public long Id { get; set; }
    public int UsuarioId { get; set; }
    public string Rol { get; set; } = default!;
    public string Accion { get; set; } = default!;
    public string Entidad { get; set; } = default!;
    public long? EntidadId { get; set; }

    /// <summary>Sobre quién (Personal.Id) — nulo si la acción no recae sobre una persona.</summary>
    public int? PersonalId { get; set; }
    public byte? LineaId { get; set; }

    /// <summary>"OK" | "RECHAZO".</summary>
    public string Resultado { get; set; } = default!;
    public string? CodigoRechazo { get; set; }
    public string? DatosAntes { get; set; }
    public string? DatosDespues { get; set; }
    public long? JustificacionId { get; set; }
    public string? DeviceId { get; set; }
    public DateTime OcurridoEn { get; set; }
}

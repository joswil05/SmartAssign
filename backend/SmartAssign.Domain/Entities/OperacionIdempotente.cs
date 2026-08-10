namespace SmartAssign.Domain.Entities;

/// <summary>
/// 04 §7.3, §12.4: bloqueo contra doble toque. Cada operación de
/// escritura sensible a duplicados (empezando por <c>sp_AsignarPersona</c>,
/// E6.8) trae una clave de idempotencia generada por el cliente; un
/// reintento con la misma clave (red inestable, doble toque físico)
/// nunca reprocesa — devuelve el resultado ya ocurrido, sea éxito o
/// rechazo.
/// </summary>
public class OperacionIdempotente
{
    public Guid Clave { get; set; }
    public bool Exitosa { get; set; }
    public string? CodigoRechazo { get; set; }
    public string? Mensaje { get; set; }
    public long? AsignacionId { get; set; }
    public DateTime CreadoEn { get; set; }
}

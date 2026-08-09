namespace SmartAssign.Domain.Entities;

/// <summary>
/// Catálogo de motivos para el formulario de justificación obligatorio de
/// toda excepción del Coordinador (§2.1.9, A6). Ver docs/04_ESQUEMA_BACKEND.md §5.4.
/// </summary>
public class MotivoExcepcion
{
    public short Id { get; set; }
    public string Nombre { get; set; } = default!;
    public bool Activo { get; set; } = true;
}

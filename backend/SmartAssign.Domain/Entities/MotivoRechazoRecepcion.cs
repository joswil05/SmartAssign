namespace SmartAssign.Domain.Entities;

/// <summary>
/// Catálogo corto de motivos para el rechazo de recepción — es obligatorio
/// dar uno (C10): sin él, rechazar se vuelve un canal silencioso para
/// esquivar relevos. Ver docs/00_DECISIONES.md §C10.
/// </summary>
public class MotivoRechazoRecepcion
{
    public short Id { get; set; }
    public string Nombre { get; set; } = default!;
    public bool Activo { get; set; } = true;
}

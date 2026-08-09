namespace SmartAssign.Domain.Entities;

/// <summary>
/// Agrupa puestos que son "la misma tarea desgastante" aunque tengan
/// identificador distinto — p. ej. "Girar botellas 1/2/3" son tres puestos
/// físicos del mismo tipo de actividad. Es lo que hace posible evaluar la
/// regla de no repetición por ACTIVIDAD y no por puesto exacto (B6: "por
/// tipo de actividad", no por puesto específico).
///
/// Nota histórica: en A4 esta tabla llevaba una bandera
/// <c>aplica_no_repeticion_24h</c>. Los datos reales del cliente traen un
/// tiempo de recuperación propio por puesto (columna TiempoDeRecup), que
/// generaliza esa bandera a un valor numérico — ver docs/00_DECISIONES.md
/// §A12 y §A14. La bandera booleana ya no existe; el tiempo de recuperación
/// vive en <see cref="Puesto.HorasRecuperacion"/>.
/// </summary>
public class TipoActividad
{
    public short Id { get; set; }
    public string Nombre { get; set; } = default!;
    public bool Activo { get; set; } = true;
}

namespace SmartAssign.Application.Importacion;

/// <summary>
/// El importador del cliente (07 §4.3): Personal, Puestos Fijos y
/// Ausencias son las tres hojas "✅ Utilizable" de 07 §4.1 que ya tienen
/// tabla propia en esta etapa. Programa y Puestos SKU quedan para la
/// etapa E5, cuando existan Turno/JornadaLinea/SKU.
/// </summary>
public interface IImportadorDatosReales
{
    /// <summary>Hoja "Personal" → tabla Personal. Categoría mapeada por 00 §G1; línea habitual por 00 §G3.</summary>
    Task<ResultadoImportacion> ImportarPersonalAsync(Stream archivoExcel, CancellationToken ct = default);

    /// <summary>Hoja "Puestos Fijos" → tabla Puesto. Rechaza SexoPreferente contaminado (00 §A13).</summary>
    Task<ResultadoImportacion> ImportarPuestosFijosAsync(Stream archivoExcel, CancellationToken ct = default);

    /// <summary>Hoja "Personal ausente" → tabla AusenciaJustificada. Exige que la persona ya esté importada.</summary>
    Task<ResultadoImportacion> ImportarAusenciasAsync(Stream archivoExcel, int usuarioId, CancellationToken ct = default);
}

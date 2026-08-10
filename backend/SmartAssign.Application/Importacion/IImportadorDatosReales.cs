namespace SmartAssign.Application.Importacion;

/// <summary>
/// El importador del cliente (07 §4.3): Personal, Puestos Fijos y
/// Ausencias son las tres hojas "✅ Utilizable" de 07 §4.1. Programa y
/// Puestos SKU se incorporan en la etapa E5 (UT-E5.3), ya con
/// Turno/JornadaLinea/SKU construidos.
/// </summary>
public interface IImportadorDatosReales
{
    /// <summary>Hoja "Personal" → tabla Personal. Categoría mapeada por 00 §G1; línea habitual por 00 §G3.</summary>
    Task<ResultadoImportacion> ImportarPersonalAsync(Stream archivoExcel, CancellationToken ct = default);

    /// <summary>Hoja "Puestos Fijos" → tabla Puesto. Rechaza SexoPreferente contaminado (00 §A13).</summary>
    Task<ResultadoImportacion> ImportarPuestosFijosAsync(Stream archivoExcel, CancellationToken ct = default);

    /// <summary>Hoja "Personal ausente" → tabla AusenciaJustificada. Exige que la persona ya esté importada.</summary>
    Task<ResultadoImportacion> ImportarAusenciasAsync(Stream archivoExcel, int usuarioId, CancellationToken ct = default);

    /// <summary>Hoja "Programa" → catálogo SKU (código, descripción, ritmo teórico). Todo o nada: es una hoja completa (07 §4.1).</summary>
    Task<ResultadoImportacion> ImportarSkuAsync(Stream archivoExcel, CancellationToken ct = default);

    /// <summary>
    /// Hoja "Puestos SKU" → Puesto (rotativos nuevos, SKU-dependientes) +
    /// PuestoSKU. NO es todo o nada (00 §G4: la hoja está deliberadamente
    /// incompleta — 18 filas, la mayoría sin Item reconocible). Importa
    /// solo lo verificable; el resto se omite sin rechazar el lote.
    /// </summary>
    Task<ResultadoImportacion> ImportarPuestosSkuAsync(Stream archivoExcel, CancellationToken ct = default);
}

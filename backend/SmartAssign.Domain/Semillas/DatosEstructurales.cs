using SmartAssign.Domain.Entities;

namespace SmartAssign.Domain.Semillas;

/// <summary>
/// Datos "estructurales, inmutables" (04_ESQUEMA_BACKEND.md §11.3): las 10
/// líneas, la prioridad base y la tabla de proximidad completa. Van también
/// a producción — a diferencia de la semilla simulada (§4.4 del plan de
/// ejecución), esto no se purga nunca.
///
/// <c>SEED_TIMESTAMP</c> y <c>SISTEMA_PLACEHOLDER</c> son valores fijos de
/// siembra, no datos de negocio: EF Core `HasData` exige un valor explícito
/// para toda columna sin generación automática en la base, y las columnas
/// de auditoría (creado_en, cambiado_por) no tienen ese valor hasta que
/// exista la tabla Usuario en la etapa E2.
/// </summary>
public static class DatosEstructurales
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int SistemaPlaceholder = 0; // sustituido por un Usuario real en E2

    /// <summary>Códigos L1..L10, con Id = número de línea. La L8 es el único Bolsón (§3.2).</summary>
    public static Linea[] Lineas() =>
        Enumerable.Range(1, 10).Select(n => new Linea
        {
            Id = (byte)n,
            Codigo = $"L{n}",
            Nombre = $"Línea {n}",
            EsBolson = n == 8,
            ActivaHoy = false,
            Situacion = "inactiva",
            CreadoEn = SeedTimestamp
        }).ToArray();

    /// <summary>
    /// Orden base de §3.3: L4 &gt; L1 &gt; L2 &gt; L6 &gt; L7 &gt; L5 &gt; L3 &gt; L8 &gt; L9 &gt; L10.
    /// Gobierna SOLO la asignación inicial y la extracción inversa (A9) —
    /// el motor de relevos nunca la consulta.
    /// </summary>
    public static PrioridadLinea[] PrioridadBase()
    {
        int[] ordenPorLinea = { 4, 1, 2, 6, 7, 5, 3, 8, 9, 10 }; // línea en cada posición 1..10
        return ordenPorLinea.Select((lineaId, idx) => new PrioridadLinea
        {
            Id = idx + 1,
            LineaId = (byte)lineaId,
            Orden = (byte)(idx + 1),
            VigenteDesde = SeedTimestamp,
            VigenteHasta = null,
            CambiadoPor = SistemaPlaceholder
        }).ToArray();
    }

    /// <summary>
    /// Grafo dirigido de proximidad (§9.5), con la corrección de fuente A1
    /// ya aplicada. Construido desde esta tabla explícita — nunca de una
    /// fórmula, de coordenadas ni de una matriz simétrica (A3) — para que
    /// una discrepancia futura contra docs/00_DECISIONES.md §A1 se detecte
    /// comparando esta misma estructura, no reinterpretando el dato.
    /// </summary>
    private static readonly Dictionary<int, int[]> RecorridoProximidad = new()
    {
        [1] = [2, 4, 9, 10, 6, 3, 7, 5, 8],
        [2] = [4, 1, 7, 9, 10, 3, 6, 5, 8],
        [3] = [10, 9, 6, 7, 4, 2, 1, 5, 8],
        [4] = [2, 1, 7, 9, 10, 6, 3, 5, 8],
        [5] = [1, 2, 4, 7, 9, 10, 6, 3, 8],
        [6] = [3, 10, 9, 7, 4, 2, 1, 5, 8],
        [7] = [9, 10, 6, 3, 4, 2, 1, 5, 8],
        // L8: sin filas — nunca busca "la línea más cercana" (§9.5)
        [9] = [3, 10, 6, 7, 4, 2, 1, 5, 8],
        [10] = [9, 3, 6, 7, 4, 2, 1, 5, 8], // ← corregida (A1)
    };

    public static ProximidadLinea[] Proximidad() =>
        RecorridoProximidad.SelectMany(par => par.Value.Select((destino, idx) => new ProximidadLinea
        {
            LineaOrigenId = (byte)par.Key,
            LineaDestinoId = (byte)destino,
            Orden = (byte)(idx + 1)
        })).ToArray();
}

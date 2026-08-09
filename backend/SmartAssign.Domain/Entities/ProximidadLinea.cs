namespace SmartAssign.Domain.Entities;

/// <summary>
/// Grafo dirigido de proximidad física entre líneas (§9.5). Asimétrico a
/// propósito (A3): L5 puede ser la más cercana a L1 sin que lo inverso sea
/// cierto. Nunca se deriva de una fórmula, de coordenadas ni de una matriz
/// simétrica — se lee tal cual está sembrada.
///
/// La L8 nunca aparece como <see cref="LineaOrigenId"/>: no recorre esta
/// jerarquía, es siempre el destino de respaldo (§9.5).
///
/// Corrección de fuente aplicada: ver docs/00_DECISIONES.md §A1 — la fila
/// original de L10 traía "L2" repetido; la fila correcta es
/// L9, L3, L6, L7, L4, L2, L1, L5, L8.
/// </summary>
public class ProximidadLinea
{
    public byte LineaOrigenId { get; set; }
    public byte LineaDestinoId { get; set; }

    /// <summary>1 = la más cercana. Rango 1..9.</summary>
    public byte Orden { get; set; }

    public Linea LineaOrigen { get; set; } = default!;
    public Linea LineaDestino { get; set; } = default!;
}

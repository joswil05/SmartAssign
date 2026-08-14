namespace SmartAssign.Domain.Entities;

/// <summary>
/// El vocabulario compartido de la regla médica (§7.2). La persona tiene
/// capacidades prohibidas; el puesto declara capacidades que exige; el
/// sistema compara. Sin este vocabulario cerrado no hay comparación
/// posible — es la pieza que exige docs/07_PLAN_DE_EJECUCION.md §4.1 (H6)
/// para producción, y que se simula para poder construir y probar (G2).
/// Ver docs/04_ESQUEMA_BACKEND.md §2.7.
/// </summary>
public class CapacidadFisica
{
    public short Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public bool Activo { get; set; } = true;

    /// <summary>
    /// "real" | "simulado" (07 §4.4). <b>Por defecto 'simulado'</b>, al
    /// revés que Personal o Puesto: este vocabulario nace inventado y solo
    /// deja de serlo cuando llega el acordado con Enfermería (H6). Es lo
    /// que permite a <c>sp_VerificarSinDatosSimulados</c> negarse a
    /// declarar lista una base que todavía decidiría sobre restricciones
    /// médicas reales con palabras que nadie de Enfermería escribió.
    ///
    /// A diferencia de las filas simuladas, la purga NO lo borra: este
    /// catálogo se REEMPLAZA, no se quita — sin él la regla médica (§7.2)
    /// se queda sin vocabulario que comparar.
    /// </summary>
    public string OrigenDato { get; set; } = "simulado";
}

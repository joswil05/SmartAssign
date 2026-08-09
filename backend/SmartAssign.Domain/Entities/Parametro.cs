namespace SmartAssign.Domain.Entities;

/// <summary>
/// Valor de planta configurable en caliente (§12.6): "ninguno puede quedar
/// fijo en el código". Tabla clave-valor — ver docs/04_ESQUEMA_BACKEND.md §9.
///
/// La AUSENCIA de una fila es un estado válido y deliberado ("a definir",
/// 04 §9): un parámetro sin sembrar significa que la regla que depende de
/// él todavía no se calibra con datos reales, y el motor debe comportarse
/// como si esa regla no aplicara todavía — nunca inventar un valor por
/// defecto (docs/06_ROADMAP.md, P5.1: "NO inventes umbrales por defecto").
/// </summary>
public class Parametro
{
    public string Clave { get; set; } = default!;
    public string Valor { get; set; } = default!;
    public string Tipo { get; set; } = default!;
    public string Descripcion { get; set; } = default!;
    public int? ModificadoPor { get; set; }
    public DateTime ModificadoEn { get; set; }
}

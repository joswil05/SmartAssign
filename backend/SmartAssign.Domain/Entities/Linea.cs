namespace SmartAssign.Domain.Entities;

/// <summary>
/// Una de las 10 líneas de producción. La L8 es el Bolsón (§3.2) — se
/// distingue por <see cref="EsBolson"/>, no por su Id: no hay que asumir
/// nunca que "la línea 8" es el Bolsón por convención de nombre.
/// Ver docs/04_ESQUEMA_BACKEND.md §2.1.
/// </summary>
public class Linea
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;

    /// <summary>Solo una línea de las 10 puede tener este valor en true (§3.2).</summary>
    public bool EsBolson { get; set; }

    /// <summary>
    /// Piso de seguridad de esta línea (§9.6, B5). Nulo = usa el valor de
    /// planta por defecto. Nunca se infiere un número aquí en código.
    /// </summary>
    public short? MinimoOperarios { get; set; }

    public bool ActivaHoy { get; set; }

    /// <summary>inactiva | activa | en_arranque | en_produccion | en_paro | en_limpieza (§3.1)</summary>
    public string Situacion { get; set; } = "inactiva";

    /// <summary>
    /// FK a Usuario(Id) (§2.3). La línea NUNCA se guarda en la sesión del
    /// usuario ni en el dispositivo — este campo es la única fuente de
    /// verdad de qué supervisor tiene cada línea, resuelta en vivo en cada
    /// petición (04 §6.1, D6).
    /// </summary>
    public int? SupervisorActualId { get; set; }

    public DateTime CreadoEn { get; set; }
    public byte[] RowVersion { get; set; } = default!;
}

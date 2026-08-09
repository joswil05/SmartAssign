namespace SmartAssign.Domain.Entities;

/// <summary>
/// Una persona de planta. <see cref="Categoria"/> viene del padrón real
/// mapeado por decisión G1 (columna "Perfil" del Excel del cliente, que
/// pese al nombre es la categoría laboral, no una preferencia). Ver
/// docs/00_DECISIONES.md §G1.
/// </summary>
public class Personal
{
    public int Id { get; set; }

    /// <summary>Número de gafete/ficha (§12.2) — es lo que el QR codifica. Viene de CodEmpleado.</summary>
    public string Ficha { get; set; } = default!;

    public string NombreCompleto { get; set; } = default!;

    /// <summary>
    /// operario | operador_a | operador_b | operador_c | averiero |
    /// liderazgo (Parte IV, 00 §G1). B y C solo existen en la semilla de
    /// desarrollo (subconjunto de operario re-etiquetado, §G1) hasta que
    /// el cliente entregue la categorización real.
    /// </summary>
    public string Categoria { get; set; } = default!;

    /// <summary>
    /// masculino | femenino, nulo si no se registró. Es el otro lado de la
    /// comparación con <see cref="Puesto.SexoPreferente"/> — la regla
    /// blanda del §7.3 realizada como preferencia de sexo (00 §A13).
    /// NULL significa "no evaluar", nunca "indistinto" — ver 00 §A13/§7.3.
    /// </summary>
    public string? Sexo { get; set; }

    /// <summary>
    /// De dónde sale la línea física al arrancar, sin reloj checador
    /// (§8.2, 00 §C3). Nulo para personal de centros de coste que no son
    /// de las 10 líneas — MAQUILA/PET/1605/1606 (00 §G3).
    /// </summary>
    public byte? LineaHabitual { get; set; }

    /// <summary>Dónde está físicamente hoy (§8.2). Nulo hasta que alguien lo registre presente.</summary>
    public byte? LineaFisicaActual { get; set; }

    /// <summary>fuera_de_turno | presente_sin_asignar | asignado | en_transito | en_bolson | retirado_temporal | ausente_justificado (Parte VI).</summary>
    public string Situacion { get; set; } = "fuera_de_turno";

    /// <summary>Distintivo visual permanente (§11.5, 00 §B7) — nunca bloquea, solo informa.</summary>
    public bool DobleTurno { get; set; }

    /// <summary>Baja/reactivación (§2.1.6). No se borra a nadie del padrón.</summary>
    public bool Activo { get; set; } = true;

    public byte[] RowVersion { get; set; } = default!;

    public Linea? LineaHabitualNav { get; set; }
    public Linea? LineaFisicaActualNav { get; set; }
}

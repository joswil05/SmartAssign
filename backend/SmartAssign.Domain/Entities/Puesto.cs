namespace SmartAssign.Domain.Entities;

/// <summary>
/// Fijo o rotativo (Parte V). Fijo/rotativo se deriva de si tiene
/// <see cref="HorasEnPuesto"/>: en los datos reales del cliente, los 31
/// puestos sin ese valor son fijos y los 67 con valor son rotativos — no
/// hay un campo booleano separado que lo declare, es una consecuencia del
/// propio dato (docs/07_PLAN_DE_EJECUCION.md §4.2).
///
/// <see cref="TitularId"/> y <see cref="UmbralCriticoMin"/> se añaden en la
/// etapa E3, cuando exista la tabla Personal — ver PROGRESO.md E3.1. Esta
/// versión de la tabla (migración 001) es deliberadamente parcial.
/// </summary>
public class Puesto
{
    public int Id { get; set; }
    public byte LineaId { get; set; }
    public string Codigo { get; set; } = default!;
    public string NombrePuesto { get; set; } = default!;

    /// <summary>"fijo" | "rotativo" (Parte V).</summary>
    public string Tipo { get; set; } = default!;

    /// <summary>Agrupa puestos de la misma tarea desgastante (B6). Solo aplica a rotativos.</summary>
    public short? TipoActividadId { get; set; }

    /// <summary>Solo fijos: operador_a | operador_c | averiero (§4.2). Nulo en rotativos.</summary>
    public string? CategoriaTitular { get; set; }

    /// <summary>
    /// Sexo preferente del puesto — es el "perfil preferente" del §7.3,
    /// una regla blanda que cede en los niveles 2 y 4 de la escalera
    /// §8.5. Ver docs/00_DECISIONES.md §A13. Nulo = sin preferencia
    /// (indistinto) — nunca se infiere.
    /// </summary>
    public string? SexoPreferente { get; set; }

    /// <summary>
    /// Horas antes de que el puesto entre en fatiga "sugerida" (§9.1).
    /// Viene de TiempoEnPuesto en el archivo real del cliente. Solo
    /// puestos rotativos lo tienen — ver docs/00_DECISIONES.md §A14.
    /// </summary>
    public short? HorasEnPuesto { get; set; }

    /// <summary>
    /// Horas de recuperación antes de poder volver a un puesto de esta
    /// misma actividad (generaliza la regla de 24h — A12). Viene de
    /// TiempoDeRecup. Girar botellas trae 24; Limpieza trae 48.
    /// </summary>
    public short? HorasRecuperacion { get; set; }

    public bool Activo { get; set; } = true;
    public byte[] RowVersion { get; set; } = default!;

    public Linea Linea { get; set; } = default!;
    public TipoActividad? TipoActividad { get; set; }
}

namespace SmartAssign.Domain.Entities;

/// <summary>
/// Fijo o rotativo (Parte V). Fijo/rotativo se deriva de si tiene
/// <see cref="HorasEnPuesto"/>: en los datos reales del cliente, los 31
/// puestos sin ese valor son fijos y los 67 con valor son rotativos — no
/// hay un campo booleano separado que lo declare, es una consecuencia del
/// propio dato (docs/07_PLAN_DE_EJECUCION.md §4.2).
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
    /// La categoría que el puesto EXIGE de quien lo ocupa (§4.2) — regla
    /// dura, nunca cede (00 §B12). Viene de la columna real PerfilRequerido
    /// (Supervisor, Operador, Averiero, Estibador, Indistinto, Genérico,
    /// Operador de filtro — 00 §A13). Distinta de <see cref="CategoriaTitular"/>,
    /// que es la asignación técnica de QUIÉN es el titular de un fijo, no
    /// la categoría que el puesto exige. El motor que evalúa esta matriz
    /// se construye en la etapa E4 (fn_CategoriaCompatible) — aquí solo se
    /// almacena el dato para que el importador (E3.6) tenga dónde ponerlo.
    /// </summary>
    public string? PerfilRequerido { get; set; }

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
    /// Umbral de fatiga "crítica" (§9.1) — sigue nulable con default de
    /// planta, tal como A4 ya preveía: el Excel real no trae un segundo
    /// valor, y A14 decide explícitamente no inventarlo. En HORAS, no
    /// minutos, para poder compararse contra <see cref="HorasEnPuesto"/>
    /// sin conversión de unidades — decisión técnica de esta etapa, no de
    /// negocio (docs/00_DECISIONES.md §A14).
    /// </summary>
    public short? UmbralCriticoHoras { get; set; }

    /// <summary>
    /// Horas de recuperación antes de poder volver a un puesto de esta
    /// misma actividad (generaliza la regla de 24h — A12). Viene de
    /// TiempoDeRecup. Girar botellas trae 24; Limpieza trae 48.
    /// </summary>
    public short? HorasRecuperacion { get; set; }

    /// <summary>
    /// Solo fijos (C12): asignación técnica que dispara el barrido
    /// automático (§8.3). En rotativos sería mera preferencia (§8.5 N1) —
    /// pero eso llega en una etapa posterior con el motor de sugerencia;
    /// por ahora el campo existe para ambos tipos y el procedimiento de
    /// barrido es quien filtra por <see cref="Tipo"/> = 'fijo' (C12).
    /// </summary>
    public int? TitularId { get; set; }

    public bool Activo { get; set; } = true;
    public byte[] RowVersion { get; set; } = default!;

    public Linea Linea { get; set; } = default!;
    public TipoActividad? TipoActividad { get; set; }
    public Personal? Titular { get; set; }
}

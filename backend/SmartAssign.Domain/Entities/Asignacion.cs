namespace SmartAssign.Domain.Entities;

/// <summary>
/// El corazón del modelo (04 §5.1): quién ocupa qué puesto, ahora mismo.
/// La aplicación nunca escribe aquí directamente — el único camino es
/// <c>sp_ValidarAsignacion</c> + <c>sp_AsignarPersona</c> (04 §7.5, E4.7).
/// </summary>
public class Asignacion
{
    public long Id { get; set; }
    public int JornadaLineaId { get; set; }
    public int PuestoId { get; set; }
    public int PersonalId { get; set; }

    /// <summary>Quién era el titular original, si esto cubre una ausencia (§8.3) — para poder devolver el puesto (00 §C1).</summary>
    public int? TitularOriginalId { get; set; }

    /// <summary>
    /// barrido_automatico | manual_supervisor | relevo | reasignacion_relevado |
    /// extraccion_inversa | intervencion_coordinador | cobertura_vacante_critica (CK_Asig_origen).
    /// </summary>
    public string Origen { get; set; } = default!;

    public DateTime Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public string? MotivoFin { get; set; }
    public int AsignadoPor { get; set; }
    public long? JustificacionId { get; set; }

    /// <summary>Si esta asignación cedió el perfil preferente (§8.5 niveles 2 y 4, 00 §B12) — única regla que cede.</summary>
    public bool CedePerfil { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public JornadaLinea? JornadaLinea { get; set; }
    public Puesto? Puesto { get; set; }
    public Personal? Personal { get; set; }
}

namespace SmartAssign.Domain.Entities;

/// <summary>
/// El formulario obligatorio de toda excepción del Coordinador (00 §A6):
/// "toda excepción del Coordinador la exige. Sin formulario, la operación
/// no se ejecuta." Ver docs/04_ESQUEMA_BACKEND.md §5.4.
/// </summary>
public class JustificacionExcepcion
{
    public long Id { get; set; }

    /// <summary>
    /// movimiento_fuera_de_flujo | saltar_ventana_arranque | forzar_cierre_turno |
    /// extraccion_operador_b | forzar_bajo_piso_seguridad | cancelar_transito |
    /// asignacion_liderazgo (04 §5.4).
    /// </summary>
    public string TipoExcepcion { get; set; } = default!;

    public short MotivoId { get; set; }

    /// <summary>Texto libre obligatorio, mínimo 10 caracteres reales (CK_JE_texto) — un formulario que se llena con un espacio no es obligatorio.</summary>
    public string Texto { get; set; } = default!;

    public int UsuarioId { get; set; }
    public DateTime CreadaEn { get; set; }

    public MotivoExcepcion? Motivo { get; set; }
    public Usuario? Usuario { get; set; }
}

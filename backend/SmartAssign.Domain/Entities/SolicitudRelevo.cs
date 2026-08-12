namespace SmartAssign.Domain.Entities;

/// <summary>
/// La cola de relevos pendientes — Parte IX §9.4, 04 §5.3. Se abre cuando
/// un puesto rotativo alcanza fatiga "sugerido"/"crítico" o un
/// supervisor lo marca manualmente; §9.4 paso 1 es literal: "el puesto
/// no se libera todavía". Nada de esta fila mueve a nadie — solo hace
/// visible que hace falta un relevo.
/// </summary>
public class SolicitudRelevo
{
    public long Id { get; set; }
    public int PuestoId { get; set; }
    public int JornadaLineaId { get; set; }

    /// <summary>umbral_automatico | manual_supervisor | vacante_critica (CK_SR_origen).</summary>
    public string Origen { get; set; } = default!;

    /// <summary>sugerido | critico | maxima (CK_SR_nivel) — "maxima" es de vacante_critica (C11), fuera del alcance de E9.1.</summary>
    public string Nivel { get; set; } = default!;

    /// <summary>% sobre el umbral propio del puesto (A4, B3) — insumo del orden de cola (E9.2).</summary>
    public decimal? ExcesoRelativo { get; set; }

    public DateTime CreadaEn { get; set; }
    public DateTime? ResueltaEn { get; set; }

    /// <summary>cubierta | cancelada | cierre_turno (CK_SR_resultado) — NULL mientras sigue abierta.</summary>
    public string? Resultado { get; set; }

    public long? MovimientoId { get; set; }

    public Puesto? Puesto { get; set; }
    public JornadaLinea? JornadaLinea { get; set; }
    public Movimiento? Movimiento { get; set; }
}

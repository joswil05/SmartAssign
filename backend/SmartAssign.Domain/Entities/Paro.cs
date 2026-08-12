namespace SmartAssign.Domain.Entities;

/// <summary>
/// Paro técnico de una línea (§11.1) — clasificación en dos niveles
/// (<see cref="CategoriaParo"/> → <see cref="CausaParo"/>) con
/// descripción obligatoria. Ver docs/04_ESQUEMA_BACKEND.md §4.
/// </summary>
public class Paro
{
    public int Id { get; set; }
    public int JornadaLineaId { get; set; }

    public int? LoteId { get; set; }

    public short CategoriaId { get; set; }
    public short CausaId { get; set; }

    /// <summary>Obligatoria — CK_Paro_descripcion (§11.1: "el supervisor debe escribir qué observó").</summary>
    public string Descripcion { get; set; } = default!;

    public DateTime Inicio { get; set; }

    /// <summary>NULL mientras el paro sigue abierto — se cierra al "reanudar la producción" explícitamente.</summary>
    public DateTime? Fin { get; set; }

    public int RegistradoPor { get; set; }
    public int? ReanudadoPor { get; set; }

    public JornadaLinea JornadaLinea { get; set; } = default!;
    public Lote? Lote { get; set; }
    public CategoriaParo Categoria { get; set; } = default!;
    public CausaParo Causa { get; set; } = default!;
    public Usuario Registrante { get; set; } = default!;
    public Usuario? Reanudante { get; set; }
}

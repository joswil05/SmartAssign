using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

/// <summary>Completa desde E5 — ver docs/04_ESQUEMA_BACKEND.md §4.1 y la nota en <see cref="JornadaLinea"/>.</summary>
public class JornadaLineaConfig : IEntityTypeConfiguration<JornadaLinea>
{
    public void Configure(EntityTypeBuilder<JornadaLinea> b)
    {
        b.ToTable("JornadaLinea", t => t.HasCheckConstraint("CK_Jornada_estado",
            "estado IN ('planificada','confirmada','arrancada','cerrada')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.LineaId).HasColumnName("linea_id");
        b.Property(x => x.TurnoId).HasColumnName("turno_id");
        b.Property(x => x.DiaOperacion).HasColumnName("dia_operacion").HasColumnType("date");
        b.Property(x => x.SkuId).HasColumnName("sku_id");
        b.Property(x => x.SupervisorId).HasColumnName("supervisor_id");
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("planificada");
        b.Property(x => x.ArrancadoEn).HasColumnName("arrancado_en");
        b.Property(x => x.VentanaArranqueFin).HasColumnName("ventana_arranque_fin");
        b.Property(x => x.CerradoEn).HasColumnName("cerrado_en");
        b.Property(x => x.CerradoForzadoPor).HasColumnName("cerrado_forzado_por");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sku).WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CerradoPor).WithMany().HasForeignKey(x => x.CerradoForzadoPor).OnDelete(DeleteBehavior.Restrict);

        // Como máximo una jornada abierta por línea a la vez (E4).
        b.HasIndex(x => x.LineaId).IsUnique().HasFilter("[cerrado_en] IS NULL")
            .HasDatabaseName("UX_JornadaLinea_abierta");

        // Una línea no se planifica dos veces para el mismo turno/día (04 §4.1).
        b.HasIndex(x => new { x.LineaId, x.TurnoId, x.DiaOperacion }).IsUnique()
            .HasDatabaseName("UQ_Jornada");
    }
}

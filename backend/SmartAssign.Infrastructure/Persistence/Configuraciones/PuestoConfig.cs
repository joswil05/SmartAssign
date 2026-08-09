using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class PuestoConfig : IEntityTypeConfiguration<Puesto>
{
    public void Configure(EntityTypeBuilder<Puesto> b)
    {
        b.ToTable("Puesto", t =>
        {
            t.HasCheckConstraint("CK_Puesto_tipo", "tipo IN ('fijo','rotativo')");
            t.HasCheckConstraint("CK_Puesto_categoria",
                "(tipo = 'fijo' AND categoria_titular IS NOT NULL) OR " +
                "(tipo = 'rotativo' AND categoria_titular IS NULL)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.LineaId).HasColumnName("linea_id");
        b.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        b.Property(x => x.NombrePuesto).HasColumnName("nombre_puesto").HasMaxLength(120).IsRequired();
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(10).IsRequired();
        b.Property(x => x.TipoActividadId).HasColumnName("tipo_actividad_id");
        b.Property(x => x.CategoriaTitular).HasColumnName("categoria_titular").HasMaxLength(15);
        b.Property(x => x.SexoPreferente).HasColumnName("sexo_preferente").HasMaxLength(12);
        b.Property(x => x.HorasEnPuesto).HasColumnName("horas_en_puesto");
        b.Property(x => x.HorasRecuperacion).HasColumnName("horas_recuperacion");
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TipoActividad).WithMany().HasForeignKey(x => x.TipoActividadId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.LineaId, x.Codigo }).IsUnique();
    }
}

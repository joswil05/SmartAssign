using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

/// <summary>Se siembra vacío a propósito — ver docs/00_DECISIONES.md §C6 y la nota en <see cref="Turno"/>.</summary>
public class TurnoConfig : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> b)
    {
        b.ToTable("Turno");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(30).IsRequired();
        b.Property(x => x.HoraInicio).HasColumnName("hora_inicio").HasColumnType("time");
        b.Property(x => x.HoraFin).HasColumnName("hora_fin").HasColumnType("time");
        b.Property(x => x.CruzaMedianoche).HasColumnName("cruza_medianoche")
            .HasComputedColumnSql("CASE WHEN hora_fin <= hora_inicio THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END", stored: true)
            .ValueGeneratedOnAddOrUpdate();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);

        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

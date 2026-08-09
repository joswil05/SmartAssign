using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class TipoActividadConfig : IEntityTypeConfiguration<TipoActividad>
{
    public void Configure(EntityTypeBuilder<TipoActividad> b)
    {
        b.ToTable("TipoActividad");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(80).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

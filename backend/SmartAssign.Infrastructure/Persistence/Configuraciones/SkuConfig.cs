using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class SkuConfig : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> b)
    {
        b.ToTable("SKU", t => t.HasCheckConstraint("CK_SKU_ritmo", "ritmo_teorico_hora > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(30).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(150).IsRequired();
        b.Property(x => x.RitmoTeoricoHora).HasColumnName("ritmo_teorico_hora").HasColumnType("decimal(10,2)");
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.HasIndex(x => x.Codigo).IsUnique();
    }
}

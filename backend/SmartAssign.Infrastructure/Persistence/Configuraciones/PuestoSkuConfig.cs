using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class PuestoSkuConfig : IEntityTypeConfiguration<PuestoSku>
{
    public void Configure(EntityTypeBuilder<PuestoSku> b)
    {
        b.ToTable("PuestoSKU");
        b.HasKey(x => new { x.PuestoId, x.SkuId });
        b.Property(x => x.PuestoId).HasColumnName("puesto_id");
        b.Property(x => x.SkuId).HasColumnName("sku_id");

        b.HasOne(x => x.Puesto).WithMany().HasForeignKey(x => x.PuestoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Sku).WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Cascade);
    }
}

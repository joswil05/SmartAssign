using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class ProduccionAvanceConfig : IEntityTypeConfiguration<ProduccionAvance>
{
    public void Configure(EntityTypeBuilder<ProduccionAvance> b)
    {
        b.ToTable("ProduccionAvance", t =>
        {
            t.HasCheckConstraint("CK_Avance_cantidad", "cantidad >= 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.Cantidad).HasColumnName("cantidad").HasColumnType("decimal(12,2)");
        b.Property(x => x.RegistradoPor).HasColumnName("registrado_por");
        b.Property(x => x.RegistradoEn).HasColumnName("registrado_en").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Lote).WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Registrante).WithMany().HasForeignKey(x => x.RegistradoPor).OnDelete(DeleteBehavior.Restrict);
    }
}

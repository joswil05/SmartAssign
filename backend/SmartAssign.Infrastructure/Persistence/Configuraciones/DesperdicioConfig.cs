using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class DesperdicioConfig : IEntityTypeConfiguration<Desperdicio>
{
    public void Configure(EntityTypeBuilder<Desperdicio> b)
    {
        b.ToTable("Desperdicio", t =>
        {
            // §11.3: separado por causa — cada cantidad, por sí sola, nunca negativa.
            t.HasCheckConstraint("CK_Desp_valores", "dano_origen >= 0 AND dano_proceso >= 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.DanoOrigen).HasColumnName("dano_origen").HasColumnType("decimal(12,2)").HasDefaultValue(0m);
        b.Property(x => x.DanoProceso).HasColumnName("dano_proceso").HasColumnType("decimal(12,2)").HasDefaultValue(0m);
        b.Property(x => x.Justificacion).HasColumnName("justificacion").HasMaxLength(600);
        b.Property(x => x.RegistradoPor).HasColumnName("registrado_por");
        b.Property(x => x.RegistradoEn).HasColumnName("registrado_en").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Lote).WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Registrante).WithMany().HasForeignKey(x => x.RegistradoPor).OnDelete(DeleteBehavior.Restrict);
    }
}

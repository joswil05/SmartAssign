using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class LoteConfig : IEntityTypeConfiguration<Lote>
{
    public void Configure(EntityTypeBuilder<Lote> b)
    {
        b.ToTable("Lote");

        b.HasKey(x => x.Id);
        b.Property(x => x.JornadaLineaId).HasColumnName("jornada_linea_id");
        b.Property(x => x.SkuId).HasColumnName("sku_id");
        b.Property(x => x.Numero).HasColumnName("numero");
        b.Property(x => x.AbiertoEn).HasColumnName("abierto_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.CerradoEn).HasColumnName("cerrado_en");
        b.Property(x => x.ProduccionReal).HasColumnName("produccion_real").HasColumnType("decimal(12,2)");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasOne(x => x.JornadaLinea).WithMany().HasForeignKey(x => x.JornadaLineaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sku).WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.JornadaLineaId, x.Numero }).IsUnique().HasDatabaseName("UX_Lote");
        // 00 §C5: solo un lote abierto por jornada-línea a la vez.
        b.HasIndex(x => x.JornadaLineaId).IsUnique().HasFilter("[cerrado_en] IS NULL").HasDatabaseName("UX_Lote_abierto");
    }
}

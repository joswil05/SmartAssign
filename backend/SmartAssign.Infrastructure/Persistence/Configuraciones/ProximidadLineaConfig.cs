using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class ProximidadLineaConfig : IEntityTypeConfiguration<ProximidadLinea>
{
    public void Configure(EntityTypeBuilder<ProximidadLinea> b)
    {
        b.ToTable("ProximidadLinea", t =>
        {
            t.HasCheckConstraint("CK_Proximidad_distinta", "linea_origen <> linea_destino");
            t.HasCheckConstraint("CK_Proximidad_orden", "orden BETWEEN 1 AND 9");
        });

        // PK compuesta (linea_origen, orden): permite que L5→L1 tenga
        // orden 1 mientras L1→L5 tiene orden 8, sin que la base lo trate
        // como incoherencia (A3: la asimetría es intencional).
        b.HasKey(x => new { x.LineaOrigenId, x.Orden });

        b.Property(x => x.LineaOrigenId).HasColumnName("linea_origen");
        b.Property(x => x.LineaDestinoId).HasColumnName("linea_destino");
        b.Property(x => x.Orden).HasColumnName("orden");

        // Restrict en ambas FK hacia Linea: dos caminos de cascada desde la
        // misma tabla no son válidos en SQL Server si alguna fuera Cascade.
        b.HasOne(x => x.LineaOrigen).WithMany()
            .HasForeignKey(x => x.LineaOrigenId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LineaDestino).WithMany()
            .HasForeignKey(x => x.LineaDestinoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.LineaOrigenId, x.LineaDestinoId }).IsUnique()
            .HasDatabaseName("UQ_Proximidad");
    }
}

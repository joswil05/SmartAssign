using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class PrioridadLineaConfig : IEntityTypeConfiguration<PrioridadLinea>
{
    public void Configure(EntityTypeBuilder<PrioridadLinea> b)
    {
        b.ToTable("PrioridadLinea", t => t.HasCheckConstraint(
            "CK_Prioridad_orden", "orden BETWEEN 1 AND 10"));

        b.HasKey(x => x.Id);
        b.Property(x => x.LineaId).HasColumnName("linea_id");
        b.Property(x => x.Orden).HasColumnName("orden");
        b.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
        b.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
        b.Property(x => x.CambiadoPor).HasColumnName("cambiado_por");

        b.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);

        // Versionado: exactamente una fila vigente por línea, y un solo
        // orden vigente a la vez — es lo que hace demostrable que B8
        // ("solo hacia adelante") se cumple.
        b.HasIndex(x => x.LineaId).IsUnique().HasFilter("[vigente_hasta] IS NULL")
            .HasDatabaseName("UX_Prioridad_vigente");
        b.HasIndex(x => x.Orden).IsUnique().HasFilter("[vigente_hasta] IS NULL")
            .HasDatabaseName("UX_Prioridad_orden_vigente");
    }
}

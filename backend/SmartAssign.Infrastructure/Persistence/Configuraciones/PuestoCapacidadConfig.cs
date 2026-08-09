using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class PuestoCapacidadConfig : IEntityTypeConfiguration<PuestoCapacidad>
{
    public void Configure(EntityTypeBuilder<PuestoCapacidad> b)
    {
        b.ToTable("PuestoCapacidad");
        b.HasKey(x => new { x.PuestoId, x.CapacidadId });
        b.Property(x => x.PuestoId).HasColumnName("puesto_id");
        b.Property(x => x.CapacidadId).HasColumnName("capacidad_id");

        b.HasOne(x => x.Puesto).WithMany().HasForeignKey(x => x.PuestoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Capacidad).WithMany().HasForeignKey(x => x.CapacidadId).OnDelete(DeleteBehavior.Restrict);
    }
}

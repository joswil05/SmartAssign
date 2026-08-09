using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class RestriccionMedicaConfig : IEntityTypeConfiguration<RestriccionMedica>
{
    public void Configure(EntityTypeBuilder<RestriccionMedica> b)
    {
        b.ToTable("RestriccionMedica", t => t.HasCheckConstraint("CK_RM_vigencia", "fecha_fin IS NULL OR fecha_fin >= fecha_inicio"));

        b.HasKey(x => x.Id);
        b.Property(x => x.PersonalId).HasColumnName("personal_id");
        b.Property(x => x.CapacidadId).HasColumnName("capacidad_id");
        b.Property(x => x.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("date");
        b.Property(x => x.FechaFin).HasColumnName("fecha_fin").HasColumnType("date");
        b.Property(x => x.Fuente).HasColumnName("fuente").HasMaxLength(120).IsRequired();
        b.Property(x => x.FechaDictamen).HasColumnName("fecha_dictamen").HasColumnType("date");
        b.Property(x => x.Observacion).HasColumnName("observacion").HasMaxLength(400);
        b.Property(x => x.RegistradoPor).HasColumnName("registrado_por");
        b.Property(x => x.RegistradoEn).HasColumnName("registrado_en").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Personal).WithMany().HasForeignKey(x => x.PersonalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Capacidad).WithMany().HasForeignKey(x => x.CapacidadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.RegistradoPor).OnDelete(DeleteBehavior.Restrict);

        // 00 §C14: "§7.2 evalúa solo las activas hoy" — la consulta central de la regla médica.
        b.HasIndex(x => new { x.PersonalId, x.CapacidadId })
            .HasDatabaseName("IX_RM_vigentes")
            .IncludeProperties(x => new { x.FechaInicio, x.FechaFin });
    }
}

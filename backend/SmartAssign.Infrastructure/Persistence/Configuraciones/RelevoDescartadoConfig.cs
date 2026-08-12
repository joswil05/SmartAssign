using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class RelevoDescartadoConfig : IEntityTypeConfiguration<RelevoDescartado>
{
    public void Configure(EntityTypeBuilder<RelevoDescartado> b)
    {
        b.ToTable("RelevoDescartado");

        b.HasKey(x => x.Id);
        b.Property(x => x.PuestoId).HasColumnName("puesto_id");
        b.Property(x => x.PersonalId).HasColumnName("personal_id");
        b.Property(x => x.JornadaDia).HasColumnName("jornada_dia");
        b.Property(x => x.DescartadoPor).HasColumnName("descartado_por");
        b.Property(x => x.DescartadoEn).HasColumnName("descartado_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.LimpiadoEn).HasColumnName("limpiado_en");
        b.Property(x => x.LimpiadoPor).HasColumnName("limpiado_por");

        b.HasOne(x => x.Puesto).WithMany().HasForeignKey(x => x.PuestoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Personal).WithMany().HasForeignKey(x => x.PersonalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Descartante).WithMany().HasForeignKey(x => x.DescartadoPor).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Limpiador).WithMany().HasForeignKey(x => x.LimpiadoPor).OnDelete(DeleteBehavior.Restrict);

        // B10: el descarte es del par (puesto, persona) dentro de un mismo día de turno.
        b.HasIndex(x => new { x.PuestoId, x.PersonalId, x.JornadaDia }).IsUnique().HasDatabaseName("UQ_Descartado");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class UltimaTareaJornadaConfig : IEntityTypeConfiguration<UltimaTareaJornada>
{
    public void Configure(EntityTypeBuilder<UltimaTareaJornada> b)
    {
        b.ToTable("UltimaTareaJornada");
        b.HasKey(x => x.PersonalId);
        b.Property(x => x.PersonalId).HasColumnName("personal_id").ValueGeneratedNever();
        b.Property(x => x.TipoActividadId).HasColumnName("tipo_actividad_id");
        b.Property(x => x.PuestoId).HasColumnName("puesto_id");
        b.Property(x => x.DiaOperacion).HasColumnName("dia_operacion").HasColumnType("date");
        b.Property(x => x.RegistradoEn).HasColumnName("registrado_en").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Personal).WithMany().HasForeignKey(x => x.PersonalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TipoActividad).WithMany().HasForeignKey(x => x.TipoActividadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Puesto).WithMany().HasForeignKey(x => x.PuestoId).OnDelete(DeleteBehavior.Restrict);
    }
}

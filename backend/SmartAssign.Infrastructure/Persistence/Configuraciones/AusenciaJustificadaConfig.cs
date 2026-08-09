using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class AusenciaJustificadaConfig : IEntityTypeConfiguration<AusenciaJustificada>
{
    public void Configure(EntityTypeBuilder<AusenciaJustificada> b)
    {
        b.ToTable("AusenciaJustificada", t => t.HasCheckConstraint("CK_Ausencia_tipo",
            "tipo IN ('vacaciones','permiso','cita_medica','subsidio','accidente_laboral','otro')"));

        b.HasKey(x => x.Id);
        b.Property(x => x.PersonalId).HasColumnName("personal_id");
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(30).IsRequired();
        b.Property(x => x.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("date");
        b.Property(x => x.FechaFin).HasColumnName("fecha_fin").HasColumnType("date");
        b.Property(x => x.RegistradoPor).HasColumnName("registrado_por");

        b.HasOne(x => x.Personal).WithMany().HasForeignKey(x => x.PersonalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.RegistradoPor).OnDelete(DeleteBehavior.Restrict);
    }
}

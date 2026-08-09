using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class AsignacionConfig : IEntityTypeConfiguration<Asignacion>
{
    public void Configure(EntityTypeBuilder<Asignacion> b)
    {
        b.ToTable("Asignacion", t =>
        {
            t.HasCheckConstraint("CK_Asig_origen", "origen IN (" +
                "'barrido_automatico','manual_supervisor','relevo','reasignacion_relevado'," +
                "'extraccion_inversa','intervencion_coordinador','cobertura_vacante_critica')");
            t.HasCheckConstraint("CK_Asig_fin", "fin IS NULL OR fin >= inicio");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.JornadaLineaId).HasColumnName("jornada_linea_id");
        b.Property(x => x.PuestoId).HasColumnName("puesto_id");
        b.Property(x => x.PersonalId).HasColumnName("personal_id");
        b.Property(x => x.TitularOriginalId).HasColumnName("titular_original_id");
        b.Property(x => x.Origen).HasColumnName("origen").HasMaxLength(25).IsRequired();
        b.Property(x => x.Inicio).HasColumnName("inicio").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.Fin).HasColumnName("fin");
        b.Property(x => x.MotivoFin).HasColumnName("motivo_fin").HasMaxLength(30);
        b.Property(x => x.AsignadoPor).HasColumnName("asignado_por");
        b.Property(x => x.JustificacionId).HasColumnName("justificacion_id");
        b.Property(x => x.CedePerfil).HasColumnName("cede_perfil").HasDefaultValue(false);
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasOne(x => x.JornadaLinea).WithMany().HasForeignKey(x => x.JornadaLineaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Puesto).WithMany().HasForeignKey(x => x.PuestoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Personal).WithMany().HasForeignKey(x => x.PersonalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Personal>().WithMany().HasForeignKey(x => x.TitularOriginalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.AsignadoPor).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JustificacionExcepcion>().WithMany().HasForeignKey(x => x.JustificacionId).OnDelete(DeleteBehavior.Restrict);

        // §7.5, B1: un puesto no tiene dos asignaciones activas; una persona no está en dos sitios.
        b.HasIndex(x => x.PuestoId).IsUnique().HasFilter("[fin] IS NULL").HasDatabaseName("UX_Asig_puesto_activo");
        b.HasIndex(x => x.PersonalId).IsUnique().HasFilter("[fin] IS NULL").HasDatabaseName("UX_Asig_personal_activo");
    }
}

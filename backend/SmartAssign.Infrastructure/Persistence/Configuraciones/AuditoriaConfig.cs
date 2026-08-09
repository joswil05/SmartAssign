using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class AuditoriaConfig : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> b)
    {
        b.ToTable("Auditoria", t => t.HasCheckConstraint("CK_Aud_resultado", "resultado IN ('OK','RECHAZO')"));

        b.HasKey(x => x.Id);
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.Rol).HasColumnName("rol").HasMaxLength(15).IsRequired();
        b.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(40).IsRequired();
        b.Property(x => x.Entidad).HasColumnName("entidad").HasMaxLength(40).IsRequired();
        b.Property(x => x.EntidadId).HasColumnName("entidad_id");
        b.Property(x => x.PersonalId).HasColumnName("personal_id");
        b.Property(x => x.LineaId).HasColumnName("linea_id");
        b.Property(x => x.Resultado).HasColumnName("resultado").HasMaxLength(20).IsRequired();
        b.Property(x => x.CodigoRechazo).HasColumnName("codigo_rechazo").HasMaxLength(40);
        b.Property(x => x.DatosAntes).HasColumnName("datos_antes");
        b.Property(x => x.DatosDespues).HasColumnName("datos_despues");
        b.Property(x => x.JustificacionId).HasColumnName("justificacion_id");
        b.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
        b.Property(x => x.OcurridoEn).HasColumnName("ocurrido_en").HasDefaultValueSql("SYSUTCDATETIME()");

        // FK a Usuario real; a Linea y Personal se dejan sin FK formal
        // donde Personal aún no existe (E3) para no bloquear la migración.
        b.HasOne<SmartAssign.Domain.Entities.Usuario>().WithMany().HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Linea>().WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.PersonalId, x.OcurridoEn }).HasDatabaseName("IX_Aud_persona");
        b.HasIndex(x => new { x.LineaId, x.OcurridoEn }).HasDatabaseName("IX_Aud_linea");
        b.HasIndex(x => new { x.CodigoRechazo, x.OcurridoEn }).HasDatabaseName("IX_Aud_rechazo")
            .HasFilter("[resultado] = 'RECHAZO'");
    }
}

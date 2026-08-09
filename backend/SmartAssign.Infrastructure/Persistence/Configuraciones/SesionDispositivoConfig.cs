using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class SesionDispositivoConfig : IEntityTypeConfiguration<SesionDispositivo>
{
    public void Configure(EntityTypeBuilder<SesionDispositivo> b)
    {
        b.ToTable("SesionDispositivo");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("Id").HasDefaultValueSql("NEWID()");
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120).IsRequired();
        b.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(256).IsRequired();
        b.Property(x => x.EmitidoEn).HasColumnName("emitido_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ExpiraEn).HasColumnName("expira_en");
        b.Property(x => x.RevocadoEn).HasColumnName("revocado_en");
        b.Property(x => x.UltimaActividad).HasColumnName("ultima_actividad").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.UsuarioId, x.DeviceId }).HasDatabaseName("IX_Sesion_activa")
            .HasFilter("[revocado_en] IS NULL");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class DispositivoPushConfig : IEntityTypeConfiguration<DispositivoPush>
{
    public void Configure(EntityTypeBuilder<DispositivoPush> b)
    {
        b.ToTable("DispositivoPush");

        b.HasKey(x => x.Id);
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120).IsRequired();
        b.Property(x => x.PushToken).HasColumnName("push_token").HasMaxLength(400).IsRequired();
        b.Property(x => x.Plataforma).HasColumnName("plataforma").HasMaxLength(10).HasDefaultValue("android");
        b.Property(x => x.RegistradoEn).HasColumnName("registrado_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RevocadoEn).HasColumnName("revocado_en");

        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.DeviceId).IsUnique().HasDatabaseName("UQ_DispositivoPush");
        b.HasIndex(x => x.UsuarioId).HasDatabaseName("IX_Push_activo").HasFilter("[revocado_en] IS NULL");
    }
}

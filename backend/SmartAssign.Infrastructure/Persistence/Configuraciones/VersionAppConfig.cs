using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class VersionAppConfig : IEntityTypeConfiguration<VersionApp>
{
    public void Configure(EntityTypeBuilder<VersionApp> b)
    {
        b.ToTable("VersionApp");
        b.HasKey(x => x.Id);
        b.Property(x => x.VersionNombre).HasColumnName("version_nombre").HasMaxLength(20).IsRequired();
        b.Property(x => x.VersionCodigo).HasColumnName("version_codigo").IsRequired();
        b.Property(x => x.RutaApk).HasColumnName("ruta_apk").HasMaxLength(300).IsRequired();
        b.Property(x => x.VersionMinimaApi).HasColumnName("version_minima_api").IsRequired();
        b.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(600);
        b.Property(x => x.PublicadaEn).HasColumnName("publicada_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.Vigente).HasColumnName("vigente").HasDefaultValue(true);

        b.HasIndex(x => x.VersionCodigo).IsUnique().HasDatabaseName("UX_VersionApp_codigo");
        b.HasIndex(x => x.Vigente).IsUnique().HasFilter("[vigente] = 1").HasDatabaseName("UX_VersionApp_vigente");
    }
}

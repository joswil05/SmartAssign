using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class LineaConfig : IEntityTypeConfiguration<Linea>
{
    public void Configure(EntityTypeBuilder<Linea> b)
    {
        b.ToTable("Linea", t => t.HasCheckConstraint(
            "CK_Linea_situacion",
            "situacion IN ('inactiva','activa','en_arranque','en_produccion','en_paro','en_limpieza')"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("Id").ValueGeneratedNever();
        b.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(4).IsRequired();
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(60).IsRequired();
        b.Property(x => x.EsBolson).HasColumnName("es_bolson").HasDefaultValue(false);
        b.Property(x => x.MinimoOperarios).HasColumnName("minimo_operarios");
        b.Property(x => x.ActivaHoy).HasColumnName("activa_hoy").HasDefaultValue(false);
        b.Property(x => x.Situacion).HasColumnName("situacion").HasMaxLength(20).HasDefaultValue("inactiva");
        b.Property(x => x.SupervisorActualId).HasColumnName("supervisor_actual");
        b.Property(x => x.CreadoEn).HasColumnName("creado_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasIndex(x => x.Codigo).IsUnique();

        // Solo puede existir un Bolsón en toda la planta (§3.2)
        b.HasIndex(x => x.EsBolson).IsUnique().HasFilter("[es_bolson] = 1")
            .HasDatabaseName("UX_Linea_bolson");

        // Un supervisor no puede tener dos líneas (§2.3) — la FK real a
        // Usuario se añade en la etapa E2, cuando esa tabla exista.
        b.HasIndex(x => x.SupervisorActualId).IsUnique()
            .HasFilter("[supervisor_actual] IS NOT NULL")
            .HasDatabaseName("UX_Linea_supervisor");
    }
}

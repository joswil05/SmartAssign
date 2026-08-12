using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class ParoConfig : IEntityTypeConfiguration<Paro>
{
    public void Configure(EntityTypeBuilder<Paro> b)
    {
        b.ToTable("Paro", t =>
        {
            // §11.1, literal: "El supervisor debe escribir qué observó antes de confirmar".
            t.HasCheckConstraint("CK_Paro_descripcion", "LEN(LTRIM(RTRIM(descripcion))) > 0");
            t.HasCheckConstraint("CK_Paro_fin", "fin IS NULL OR fin >= inicio");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.JornadaLineaId).HasColumnName("jornada_linea_id");
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.CategoriaId).HasColumnName("categoria_id");
        b.Property(x => x.CausaId).HasColumnName("causa_id");
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(500).IsRequired();
        b.Property(x => x.Inicio).HasColumnName("inicio").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.Fin).HasColumnName("fin");
        b.Property(x => x.RegistradoPor).HasColumnName("registrado_por");
        b.Property(x => x.ReanudadoPor).HasColumnName("reanudado_por");

        b.HasOne(x => x.JornadaLinea).WithMany().HasForeignKey(x => x.JornadaLineaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Causa).WithMany().HasForeignKey(x => x.CausaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Registrante).WithMany().HasForeignKey(x => x.RegistradoPor).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reanudante).WithMany().HasForeignKey(x => x.ReanudadoPor).OnDelete(DeleteBehavior.Restrict);

        // §11.4: dos paros simultáneos en la misma línea harían incalculable el tiempo efectivo de marcha.
        b.HasIndex(x => x.JornadaLineaId).IsUnique().HasFilter("[fin] IS NULL").HasDatabaseName("UX_Paro_abierto");
    }
}

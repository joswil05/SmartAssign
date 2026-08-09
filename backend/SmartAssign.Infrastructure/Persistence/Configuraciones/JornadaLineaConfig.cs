using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

/// <summary>Versión mínima de la tabla — ver docs/04_ESQUEMA_BACKEND.md §4.1 y la nota en <see cref="JornadaLinea"/>.</summary>
public class JornadaLineaConfig : IEntityTypeConfiguration<JornadaLinea>
{
    public void Configure(EntityTypeBuilder<JornadaLinea> b)
    {
        b.ToTable("JornadaLinea");
        b.HasKey(x => x.Id);
        b.Property(x => x.LineaId).HasColumnName("linea_id");
        b.Property(x => x.ArrancadoEn).HasColumnName("arrancado_en");
        b.Property(x => x.VentanaArranqueFin).HasColumnName("ventana_arranque_fin");
        b.Property(x => x.CerradoEn).HasColumnName("cerrado_en");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);

        // Como máximo una jornada abierta por línea a la vez — anticipa
        // UQ_Jornada (04 §4.1), que se completa con turno_id+dia_operacion en E5.
        b.HasIndex(x => x.LineaId).IsUnique().HasFilter("[cerrado_en] IS NULL")
            .HasDatabaseName("UX_JornadaLinea_abierta");
    }
}

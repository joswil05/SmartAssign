using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class EventoSalienteConfig : IEntityTypeConfiguration<EventoSaliente>
{
    public void Configure(EntityTypeBuilder<EventoSaliente> b)
    {
        b.ToTable("EventoSaliente");

        b.HasKey(x => x.Id);
        b.Property(x => x.TipoEvento).HasColumnName("tipo_evento").HasMaxLength(60).IsRequired();
        b.Property(x => x.Grupos).HasColumnName("grupos").HasMaxLength(400).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnName("payload_json").IsRequired();
        b.Property(x => x.CreadoEn).HasColumnName("creado_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ProcesadoEn).HasColumnName("procesado_en");

        // Solo pendientes (procesado_en IS NULL) es lo que el dispatcher barre
        // en cada sondeo — filtrado en el índice, no en cada lectura completa.
        b.HasIndex(x => x.Id).HasFilter("[procesado_en] IS NULL").HasDatabaseName("IX_EventoSaliente_pendientes");
    }
}

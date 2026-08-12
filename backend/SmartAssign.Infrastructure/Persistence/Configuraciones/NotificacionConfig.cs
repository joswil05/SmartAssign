using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class NotificacionConfig : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificacion", t =>
        {
            t.HasCheckConstraint("CK_Notif_criticidad", "criticidad IN ('normal','critica')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(35).IsRequired();
        b.Property(x => x.Criticidad).HasColumnName("criticidad").HasMaxLength(10).HasDefaultValue("normal");
        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(120).IsRequired();
        b.Property(x => x.Cuerpo).HasColumnName("cuerpo").HasMaxLength(300).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnName("payload_json");
        b.Property(x => x.CreadaEn).HasColumnName("creada_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.EntregadaEn).HasColumnName("entregada_en");
        b.Property(x => x.AcusadaEn).HasColumnName("acusada_en");
        b.Property(x => x.EscaladaEn).HasColumnName("escalada_en");

        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Índice literal de 04 §10 — pensado para el barrido de acuse/
        // escalado (criticidad + antigüedad) que construye E12.6, no para
        // el sondeo de ESTA UT (NotificacionDispatcher filtra por
        // entregada_en, no por acusada_en). Se declara completo ahora,
        // igual que Lote/Desperdicio en E11.5/E11.6, porque 04 ya lo
        // especifica entero — no hay motivo para construir la tabla a
        // medias. Un índice propio para "sin entregar" queda como
        // optimización futura, no como hueco de esta UT: el volumen de
        // Notificacion pendiente en un momento dado es pequeño (cientos,
        // no miles) frente a EventoSaliente.
        b.HasIndex(x => new { x.Criticidad, x.CreadaEn }).HasFilter("[acusada_en] IS NULL").HasDatabaseName("IX_Notif_sin_acuse");
    }
}

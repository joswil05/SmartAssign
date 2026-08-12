using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class MovimientoConfig : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> b)
    {
        b.ToTable("Movimiento", t =>
        {
            t.HasCheckConstraint("CK_Mov_estado", "estado IN (" +
                "'en_transito','recibido','rechazado','cancelado')");
            t.HasCheckConstraint("CK_Mov_motivo", "motivo IN (" +
                "'relevo','reasignacion_relevado','liberacion_bolson','paro'," +
                "'cambio_sku','linea_inactiva','rechazo_recepcion'," +
                "'intervencion_coordinador','cobertura_vacante_critica')");
            // C10: todo rechazo de recepción lleva motivo — sin él, rechazar
            // se vuelve un canal silencioso para esquivar relevos.
            t.HasCheckConstraint("CK_Mov_rechazo", "estado <> 'rechazado' OR motivo_rechazo_id IS NOT NULL");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.PersonalId).HasColumnName("personal_id");
        b.Property(x => x.LineaOrigen).HasColumnName("linea_origen");
        b.Property(x => x.LineaDestino).HasColumnName("linea_destino");
        b.Property(x => x.PuestoDestinoId).HasColumnName("puesto_destino_id");
        b.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(30).IsRequired();
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("en_transito").IsRequired();
        b.Property(x => x.HoraSalida).HasColumnName("hora_salida").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.HoraLlegada).HasColumnName("hora_llegada");
        // §12.7: columna calculada persistida — la razón de ser de esta tabla.
        b.Property(x => x.DuracionSeg).HasColumnName("duracion_seg")
            .HasComputedColumnSql("DATEDIFF(SECOND, hora_salida, hora_llegada)", stored: true);
        b.Property(x => x.DespachadoPor).HasColumnName("despachado_por");
        b.Property(x => x.RecibidoPor).HasColumnName("recibido_por");
        b.Property(x => x.MotivoRechazoId).HasColumnName("motivo_rechazo_id");
        b.Property(x => x.NotaRechazo).HasColumnName("nota_rechazo").HasMaxLength(300);
        b.Property(x => x.CaducadoEn).HasColumnName("caducado_en");
        b.Property(x => x.CanceladoPor).HasColumnName("cancelado_por");
        b.Property(x => x.JustificacionId).HasColumnName("justificacion_id");

        b.HasOne(x => x.Personal).WithMany().HasForeignKey(x => x.PersonalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Origen).WithMany().HasForeignKey(x => x.LineaOrigen).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Destino).WithMany().HasForeignKey(x => x.LineaDestino).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PuestoDestino).WithMany().HasForeignKey(x => x.PuestoDestinoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Despachante).WithMany().HasForeignKey(x => x.DespachadoPor).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Receptor).WithMany().HasForeignKey(x => x.RecibidoPor).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MotivoRechazo).WithMany().HasForeignKey(x => x.MotivoRechazoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cancelante).WithMany().HasForeignKey(x => x.CanceladoPor).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Justificacion).WithMany().HasForeignKey(x => x.JustificacionId).OnDelete(DeleteBehavior.Restrict);

        // §6.1: una persona no puede estar en dos tránsitos a la vez — la inmunidad escrita en la base (E8.2).
        b.HasIndex(x => x.PersonalId).IsUnique().HasFilter("[estado] = 'en_transito'").HasDatabaseName("UX_Mov_transito");
        // B4: dos relevistas no pueden converger al mismo puesto (guarda anti-convergencia, E8.5).
        b.HasIndex(x => x.PuestoDestinoId).IsUnique()
            .HasFilter("[estado] = 'en_transito' AND [puesto_destino_id] IS NOT NULL")
            .HasDatabaseName("UX_Mov_reserva");
        // §12.7: la consulta analítica que motiva la columna calculada.
        b.HasIndex(x => new { x.LineaOrigen, x.LineaDestino }).IncludeProperties(x => x.DuracionSeg)
            .HasFilter("[estado] = 'recibido'").HasDatabaseName("IX_Mov_analitica");
    }
}

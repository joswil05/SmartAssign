using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class JustificacionExcepcionConfig : IEntityTypeConfiguration<JustificacionExcepcion>
{
    public void Configure(EntityTypeBuilder<JustificacionExcepcion> b)
    {
        b.ToTable("JustificacionExcepcion", t =>
        {
            t.HasCheckConstraint("CK_JE_texto", "LEN(LTRIM(RTRIM(texto))) >= 10");
            t.HasCheckConstraint("CK_JE_tipo", "tipo_excepcion IN (" +
                "'movimiento_fuera_de_flujo','saltar_ventana_arranque','forzar_cierre_turno'," +
                "'extraccion_operador_b','forzar_bajo_piso_seguridad','cancelar_transito','asignacion_liderazgo')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.TipoExcepcion).HasColumnName("tipo_excepcion").HasMaxLength(35).IsRequired();
        b.Property(x => x.MotivoId).HasColumnName("motivo_id");
        b.Property(x => x.Texto).HasColumnName("texto").HasMaxLength(600).IsRequired();
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.CreadaEn).HasColumnName("creada_en").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Motivo).WithMany().HasForeignKey(x => x.MotivoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

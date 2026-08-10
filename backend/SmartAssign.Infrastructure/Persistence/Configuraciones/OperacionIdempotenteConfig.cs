using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class OperacionIdempotenteConfig : IEntityTypeConfiguration<OperacionIdempotente>
{
    public void Configure(EntityTypeBuilder<OperacionIdempotente> b)
    {
        b.ToTable("OperacionIdempotente");

        b.HasKey(x => x.Clave);
        b.Property(x => x.Clave).HasColumnName("clave").ValueGeneratedNever();
        b.Property(x => x.Exitosa).HasColumnName("exitosa");
        b.Property(x => x.CodigoRechazo).HasColumnName("codigo_rechazo").HasMaxLength(40);
        b.Property(x => x.Mensaje).HasColumnName("mensaje").HasMaxLength(400);
        b.Property(x => x.AsignacionId).HasColumnName("asignacion_id");
        b.Property(x => x.CreadoEn).HasColumnName("creado_en").HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

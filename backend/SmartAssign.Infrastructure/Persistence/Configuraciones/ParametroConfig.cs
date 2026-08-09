using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class ParametroConfig : IEntityTypeConfiguration<Parametro>
{
    public void Configure(EntityTypeBuilder<Parametro> b)
    {
        b.ToTable("Parametro");
        b.HasKey(x => x.Clave);
        b.Property(x => x.Clave).HasColumnName("clave").HasMaxLength(60);
        b.Property(x => x.Valor).HasColumnName("valor").HasMaxLength(200).IsRequired();
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(15).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(300).IsRequired();
        b.Property(x => x.ModificadoPor).HasColumnName("modificado_por");
        b.Property(x => x.ModificadoEn).HasColumnName("modificado_en").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.ModificadoPor).OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

// Catálogos simples de la etapa E1.5 — mismo patrón, un archivo para no
// dispersar cinco clases casi idénticas.

public class CapacidadFisicaConfig : IEntityTypeConfiguration<CapacidadFisica>
{
    public void Configure(EntityTypeBuilder<CapacidadFisica> b)
    {
        b.ToTable("CapacidadFisica");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(40).IsRequired();
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.HasIndex(x => x.Codigo).IsUnique();
    }
}

public class MotivoExcepcionConfig : IEntityTypeConfiguration<MotivoExcepcion>
{
    public void Configure(EntityTypeBuilder<MotivoExcepcion> b)
    {
        b.ToTable("MotivoExcepcion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class MotivoRechazoRecepcionConfig : IEntityTypeConfiguration<MotivoRechazoRecepcion>
{
    public void Configure(EntityTypeBuilder<MotivoRechazoRecepcion> b)
    {
        b.ToTable("MotivoRechazoRecepcion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class CategoriaParoConfig : IEntityTypeConfiguration<CategoriaParo>
{
    public void Configure(EntityTypeBuilder<CategoriaParo> b)
    {
        b.ToTable("CategoriaParo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(60).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class CausaParoConfig : IEntityTypeConfiguration<CausaParo>
{
    public void Configure(EntityTypeBuilder<CausaParo> b)
    {
        b.ToTable("CausaParo");
        b.HasKey(x => x.Id);
        b.Property(x => x.CategoriaId).HasColumnName("categoria_id");
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);

        b.HasOne(x => x.Categoria).WithMany(c => c.Causas)
            .HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CategoriaId, x.Nombre }).IsUnique();
    }
}

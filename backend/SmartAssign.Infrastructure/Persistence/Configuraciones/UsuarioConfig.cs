using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuario", t =>
        {
            t.HasCheckConstraint("CK_Usuario_rol", "rol IN ('coordinador','supervisor')");
            t.HasCheckConstraint("CK_Usuario_origen", "origen_identidad IN ('ad','local')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasColumnName("username").HasMaxLength(80).IsRequired();
        b.Property(x => x.NombreCompleto).HasColumnName("nombre_completo").HasMaxLength(150).IsRequired();
        b.Property(x => x.Rol).HasColumnName("rol").HasMaxLength(15).IsRequired();
        b.Property(x => x.OrigenIdentidad).HasColumnName("origen_identidad").HasMaxLength(10).HasDefaultValue("local");
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
        b.Property(x => x.PasswordSalt).HasColumnName("password_salt").HasMaxLength(64);
        b.Property(x => x.PinHash).HasColumnName("pin_hash").HasMaxLength(256);
        b.Property(x => x.PinSalt).HasColumnName("pin_salt").HasMaxLength(64);
        b.Property(x => x.PersonalId).HasColumnName("personal_id");
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.Property(x => x.BloqueadoHasta).HasColumnName("bloqueado_hasta");
        b.Property(x => x.IntentosFallidos).HasColumnName("intentos_fallidos").HasDefaultValue((byte)0);

        b.HasIndex(x => x.Username).IsUnique();

        // FK a Personal — la tabla no existe hasta E3 (04 §3.1). Se deja
        // como columna simple hasta entonces, igual que Linea.SupervisorActualId.
    }
}

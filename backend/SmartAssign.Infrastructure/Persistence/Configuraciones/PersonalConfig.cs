using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class PersonalConfig : IEntityTypeConfiguration<Personal>
{
    public void Configure(EntityTypeBuilder<Personal> b)
    {
        b.ToTable("Personal", t =>
        {
            t.HasCheckConstraint("CK_Personal_categoria",
                "categoria IN ('operario','operador_a','operador_b','operador_c','averiero','liderazgo')");
            t.HasCheckConstraint("CK_Personal_situacion",
                "situacion IN ('fuera_de_turno','presente_sin_asignar','asignado','en_transito'," +
                "'en_bolson','retirado_temporal','ausente_justificado')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Ficha).HasColumnName("ficha").HasMaxLength(20).IsRequired();
        b.Property(x => x.NombreCompleto).HasColumnName("nombre_completo").HasMaxLength(150).IsRequired();
        b.Property(x => x.Categoria).HasColumnName("categoria").HasMaxLength(20).IsRequired();
        b.Property(x => x.Sexo).HasColumnName("sexo").HasMaxLength(12);
        b.Property(x => x.LineaHabitual).HasColumnName("linea_habitual");
        b.Property(x => x.LineaFisicaActual).HasColumnName("linea_fisica_actual");
        b.Property(x => x.Situacion).HasColumnName("situacion").HasMaxLength(25).HasDefaultValue("fuera_de_turno");
        b.Property(x => x.DobleTurno).HasColumnName("doble_turno").HasDefaultValue(false);
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasOne(x => x.LineaHabitualNav).WithMany().HasForeignKey(x => x.LineaHabitual).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LineaFisicaActualNav).WithMany().HasForeignKey(x => x.LineaFisicaActual).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Ficha).IsUnique();

        // §8.2, C3: quién está disponible hoy y dónde — la consulta más
        // frecuente del motor de asignación inicial.
        b.HasIndex(x => new { x.Situacion, x.LineaFisicaActual })
            .HasDatabaseName("IX_Personal_disponible")
            .IncludeProperties(x => new { x.Ficha, x.NombreCompleto, x.Categoria })
            .HasFilter("[activo] = 1");
    }
}

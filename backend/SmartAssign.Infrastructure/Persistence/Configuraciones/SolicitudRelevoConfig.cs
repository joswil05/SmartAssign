using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Persistence.Configuraciones;

public class SolicitudRelevoConfig : IEntityTypeConfiguration<SolicitudRelevo>
{
    public void Configure(EntityTypeBuilder<SolicitudRelevo> b)
    {
        b.ToTable("SolicitudRelevo", t =>
        {
            t.HasCheckConstraint("CK_SR_origen", "origen IN (" +
                "'umbral_automatico','manual_supervisor','vacante_critica')");
            t.HasCheckConstraint("CK_SR_nivel", "nivel IN ('sugerido','critico','maxima')");
            t.HasCheckConstraint("CK_SR_resultado", "resultado IS NULL OR resultado IN (" +
                "'cubierta','cancelada','cierre_turno')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.PuestoId).HasColumnName("puesto_id");
        b.Property(x => x.JornadaLineaId).HasColumnName("jornada_linea_id");
        b.Property(x => x.Origen).HasColumnName("origen").HasMaxLength(25).IsRequired();
        b.Property(x => x.Nivel).HasColumnName("nivel").HasMaxLength(12).IsRequired();
        b.Property(x => x.ExcesoRelativo).HasColumnName("exceso_relativo").HasColumnType("decimal(6,2)");
        b.Property(x => x.CreadaEn).HasColumnName("creada_en").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ResueltaEn).HasColumnName("resuelta_en");
        b.Property(x => x.Resultado).HasColumnName("resultado").HasMaxLength(20);
        b.Property(x => x.MovimientoId).HasColumnName("movimiento_id");

        b.HasOne(x => x.Puesto).WithMany().HasForeignKey(x => x.PuestoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.JornadaLinea).WithMany().HasForeignKey(x => x.JornadaLineaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Movimiento).WithMany().HasForeignKey(x => x.MovimientoId).OnDelete(DeleteBehavior.Restrict);

        // §9.4 p1: un puesto no puede tener dos solicitudes de relevo pendientes a la vez.
        b.HasIndex(x => x.PuestoId).IsUnique().HasFilter("[resuelta_en] IS NULL").HasDatabaseName("UX_SR_abierta");
    }
}

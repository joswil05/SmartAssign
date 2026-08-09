using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Domain.Semillas;

namespace SmartAssign.Infrastructure.Persistence;

/// <summary>
/// El contexto NO decide reglas de negocio (05_TRD.md §1.4): es un
/// mapeador y un motor de migraciones. Las reglas duras viven en
/// procedimientos almacenados con DENY de escritura directa sobre las
/// tablas críticas (04_ESQUEMA_BACKEND.md §7.5) — eso se cablea en la
/// etapa E4, no aquí.
/// </summary>
public class SmartAssignDbContext : DbContext
{
    public SmartAssignDbContext(DbContextOptions<SmartAssignDbContext> options) : base(options) { }

    public DbSet<Linea> Lineas => Set<Linea>();
    public DbSet<PrioridadLinea> PrioridadesLinea => Set<PrioridadLinea>();
    public DbSet<ProximidadLinea> ProximidadesLinea => Set<ProximidadLinea>();
    public DbSet<TipoActividad> TiposActividad => Set<TipoActividad>();
    public DbSet<Sku> Skus => Set<Sku>();
    public DbSet<PuestoSku> PuestosSku => Set<PuestoSku>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<CapacidadFisica> CapacidadesFisicas => Set<CapacidadFisica>();
    public DbSet<MotivoExcepcion> MotivosExcepcion => Set<MotivoExcepcion>();
    public DbSet<MotivoRechazoRecepcion> MotivosRechazoRecepcion => Set<MotivoRechazoRecepcion>();
    public DbSet<CategoriaParo> CategoriasParo => Set<CategoriaParo>();
    public DbSet<CausaParo> CausasParo => Set<CausaParo>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SesionDispositivo> SesionesDispositivo => Set<SesionDispositivo>();
    public DbSet<DispositivoPush> DispositivosPush => Set<DispositivoPush>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<Personal> Personas => Set<Personal>();
    public DbSet<PuestoCapacidad> PuestosCapacidad => Set<PuestoCapacidad>();
    public DbSet<RestriccionMedica> RestriccionesMedicas => Set<RestriccionMedica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartAssignDbContext).Assembly);

        // Estructural, inmutable — va también a producción (04 §11.3, A1).
        modelBuilder.Entity<Linea>().HasData(DatosEstructurales.Lineas());
        modelBuilder.Entity<PrioridadLinea>().HasData(DatosEstructurales.PrioridadBase());
        modelBuilder.Entity<ProximidadLinea>().HasData(DatosEstructurales.Proximidad());

        // Catálogo, editable — punto de partida; el Coordinador amplía
        // desde la aplicación (§2.1.10). Capacidades = placeholder (G2, H6).
        modelBuilder.Entity<CapacidadFisica>().HasData(DatosCatalogo.Capacidades());
        modelBuilder.Entity<MotivoExcepcion>().HasData(DatosCatalogo.MotivosExcepcion());
        modelBuilder.Entity<MotivoRechazoRecepcion>().HasData(DatosCatalogo.MotivosRechazoRecepcion());
        modelBuilder.Entity<CategoriaParo>().HasData(DatosCatalogo.CategoriasParo());
        modelBuilder.Entity<CausaParo>().HasData(DatosCatalogo.CausasParo());
    }
}

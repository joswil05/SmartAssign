using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// 04 §7.5 / §7.3, 00 §C14: "Nunca se borran. Solo se cierran con
    /// fecha de fin" — la garantía técnica de que ni un defecto de código
    /// ni una credencial de aplicación comprometida puede borrar historial
    /// médico. `rol_app` es un usuario de base de datos SIN LOGIN — existe
    /// solo para agrupar permisos y para poder impersonarlo con
    /// EXECUTE AS en las pruebas; qué login de servidor se asigna a este
    /// rol en cada entorno (dev/staging/producción) es una decisión de
    /// despliegue, no de esta migración.
    ///
    /// El resto del DENY de 04 §7.5 (Asignacion, Movimiento, Auditoria)
    /// se añade en la etapa E4 (UT-E4.7), cuando esas tablas ya tengan
    /// flujo de escritura real que proteger.
    /// </summary>
    public partial class DenyRestriccionMedica : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'rol_app')
                    CREATE USER rol_app WITHOUT LOGIN;
                """);

            migrationBuilder.Sql("DENY DELETE ON dbo.RestriccionMedica TO rol_app;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE DELETE ON dbo.RestriccionMedica TO rol_app;");
            migrationBuilder.Sql("DROP USER IF EXISTS rol_app;");
        }
    }
}

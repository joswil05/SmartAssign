using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// Capa 3 del aislamiento (04 §6.3): seguridad a nivel de fila, red de
    /// seguridad ante un fallo de la capa 2 (el filtro del repositorio).
    /// Actúa aunque el código de aplicación falle porque vive en el motor,
    /// no en el código — "un solo WHERE olvidado en un refactor filtraría
    /// el padrón médico de otra línea" (04 §6.3).
    ///
    /// Solo se añade el predicado sobre Puesto en esta migración.
    /// JornadaLinea (la otra tabla que 04 §6.3 lista) no existe todavía —
    /// se crea en la etapa E5 — y una SECURITY POLICY no puede apuntar a
    /// una tabla inexistente. El predicado sobre JornadaLinea se añade con
    /// ALTER SECURITY POLICY ... ADD FILTER PREDICATE en la migración que
    /// cree esa tabla (ver docs/PROGRESO.md, nota de la etapa E5).
    /// </summary>
    public partial class RlsAlcanceLinea : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_AlcanceLinea(@linea_id TINYINT)
                RETURNS TABLE WITH SCHEMABINDING AS
                RETURN SELECT 1 AS alcance_ok
                       WHERE SESSION_CONTEXT(N'rol') = CAST(N'coordinador' AS SQL_VARIANT)
                          OR CAST(SESSION_CONTEXT(N'linea_id') AS TINYINT) = @linea_id;
                """);

            migrationBuilder.Sql("""
                CREATE SECURITY POLICY dbo.PoliticaAlcanceLinea
                    ADD FILTER PREDICATE dbo.fn_AlcanceLinea(linea_id) ON dbo.Puesto
                    WITH (STATE = ON);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY IF EXISTS dbo.PoliticaAlcanceLinea;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_AlcanceLinea;");
        }
    }
}

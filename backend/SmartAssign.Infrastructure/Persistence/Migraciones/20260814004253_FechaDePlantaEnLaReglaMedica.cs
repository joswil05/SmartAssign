using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// Revisión de producción, hallazgo P-01 — <b>corrige un fallo de
    /// seguridad ocupacional</b>, no una preferencia de estilo.
    ///
    /// Seis procedimientos vivos derivaban el día de calendario con
    /// <c>CAST(SYSUTCDATETIME() AS DATE)</c>, y uno de ellos —
    /// <c>sp_ValidarAsignacion</c> — se lo pasaba a
    /// <c>fn_TieneRestriccionBloqueante</c>, la regla dura del §7.2 que
    /// "NUNCA cede". Con el servidor en UTC−6, la fecha UTC ya es la de
    /// mañana desde las 18:00: una restricción médica con
    /// <c>fecha_fin = hoy</c> dejaba de cumplir <c>fecha_fin &gt;= @fecha</c>
    /// y <b>dejaba de bloquear seis horas antes de tiempo</b>, todos los días.
    /// Comprobado en vivo antes de escribir esto.
    ///
    /// 00 §C6 ya tenía la respuesta: <i>"la hora es siempre la del
    /// servidor"</i>. El servidor está en la planta, así que el "hoy" de la
    /// planta es su fecha local. Los INSTANTES siguen en UTC en toda la
    /// base y así deben seguir — lo que estaba mal era convertir un
    /// instante UTC en un DÍA DE CALENDARIO.
    ///
    /// <b>Por qué el reemplazo es dinámico.</b> Reescribir a mano seis
    /// cuerpos de procedimiento (algunos de 200 líneas, con sus versiones
    /// más recientes repartidas entre varias migraciones) es la clase de
    /// tarea donde se pierde una cláusula sin que nadie lo note — ya pasó
    /// en E10.5 con <c>sp_DespacharPersona</c>. Leer la definición vigente
    /// de <c>sys.sql_modules</c>, sustituir la expresión y volver a
    /// ejecutarla no puede perder nada: opera sobre lo que de verdad hay
    /// desplegado. La migración se verifica a sí misma al final y falla si
    /// queda un solo módulo sin corregir.
    ///
    /// El patrón correcto ya existía en el código: E9.6 evitó este mismo
    /// <c>CAST</c> a propósito para <c>RelevoDescartado.jornada_dia</c>,
    /// con un comentario que lo dice. Simplemente no se aplicó al resto.
    /// </summary>
    public partial class FechaDePlantaEnLaReglaMedica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fuente única del "hoy" de la planta. Que sea una función y no
            // una expresión repetida es el punto: la próxima vez que alguien
            // necesite la fecha, no tiene que acordarse de cuál usar.
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_FechaPlanta()
                RETURNS DATE
                AS
                BEGIN
                    -- 00 §C6: la hora es la del servidor, y el servidor está
                    -- en la planta. SYSDATETIME() es hora local; para
                    -- INSTANTES se sigue usando SYSUTCDATETIME() en todas
                    -- partes — un instante es absoluto, un día no.
                    RETURN CAST(SYSDATETIME() AS DATE);
                END;
                """);

            migrationBuilder.Sql("""
                DECLARE @nombre SYSNAME, @definicion NVARCHAR(MAX), @corregidos INT = 0;

                DECLARE modulos CURSOR LOCAL FAST_FORWARD FOR
                    SELECT o.name, m.definition
                      FROM sys.sql_modules m
                      JOIN sys.objects   o ON o.object_id = m.object_id
                     WHERE m.definition LIKE '%CAST(SYSUTCDATETIME() AS DATE)%'
                       AND o.name <> 'fn_FechaPlanta';

                OPEN modulos;
                FETCH NEXT FROM modulos INTO @nombre, @definicion;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @definicion = REPLACE(@definicion,
                        'CAST(SYSUTCDATETIME() AS DATE)', 'dbo.fn_FechaPlanta()');

                    -- Las definiciones de este esquema ya vienen como
                    -- CREATE OR ALTER; esto solo cubre el caso de que alguna
                    -- llegara como CREATE a secas.
                    IF @definicion NOT LIKE '%CREATE OR ALTER%'
                        SET @definicion = STUFF(@definicion,
                            PATINDEX('%CREATE%', @definicion), 6, 'CREATE OR ALTER');

                    EXEC sp_executesql @definicion;
                    SET @corregidos = @corregidos + 1;

                    FETCH NEXT FROM modulos INTO @nombre, @definicion;
                END

                CLOSE modulos;
                DEALLOCATE modulos;

                PRINT CONCAT('fn_FechaPlanta: ', @corregidos, ' modulo(s) corregido(s).');

                -- Se verifica a sí misma. Si el reemplazo no cubrió algo, la
                -- migración falla aquí y no deja el esquema a medio corregir.
                IF EXISTS (
                    SELECT 1 FROM sys.sql_modules m
                      JOIN sys.objects o ON o.object_id = m.object_id
                     WHERE m.definition LIKE '%CAST(SYSUTCDATETIME() AS DATE)%'
                       AND o.name <> 'fn_FechaPlanta')
                    THROW 51000, 'Quedan modulos derivando la fecha de UTC tras la correccion de P-01.', 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberadamente NO se revierten los procedimientos: volver a
            // la fecha UTC reintroduciría el fallo de seguridad. Se retira
            // solo la función, y quien de verdad quiera el comportamiento
            // anterior tiene que revertir también la migración que creó
            // cada procedimiento.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_FechaPlanta;");
        }
    }
}

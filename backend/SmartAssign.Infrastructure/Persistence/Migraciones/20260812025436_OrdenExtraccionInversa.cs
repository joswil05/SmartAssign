using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E10.1 (docs/PROGRESO.md): arranca E10 (Extracción inversa y
    /// vacante crítica). <c>fn_OrdenExtraccionInversa</c> — §9.6, literal
    /// 00 §A5: "se busca personal en la línea ACTIVA de MENOR prioridad,
    /// recorriendo la jerarquía al revés". La derivación exacta de A5:
    /// "invertir la prioridad vigente, excluir la L8 y excluir la línea
    /// que solicita" — se implementa como derivación de
    /// <c>PrioridadLinea</c>, nunca como una lista escrita aparte, para
    /// que §12.6 (prioridad configurable en caliente) no exija mantener
    /// dos listas en sincronía: si el Coordinador cambia la prioridad
    /// con <c>sp_CambiarPrioridadLinea</c> (E5.2), esta función lee el
    /// cambio de inmediato.
    ///
    /// <c>A5b</c> (cerrada — cliente): "L4 SÍ puede ser donante" — la
    /// lista publicada del §9.6 (L10, L9, L3, L5, L7, L6, L2, L1) excluye
    /// L4 solo porque L4 era la línea solicitante en ESE ejemplo, no una
    /// exclusión permanente. Por eso la función excluye
    /// <c>@linea_solicitante</c> como parámetro — cualquier línea activa
    /// puede aparecer en el resultado salvo la que está pidiendo el
    /// relevo, nunca una lista fija de "líneas donantes".
    ///
    /// Función de tabla en línea (mismo patrón que <c>fn_AlcanceLinea</c>,
    /// E4.3) — expone <c>orden</c> para que quien la consuma haga su
    /// propio <c>ORDER BY ... DESC</c> (mayor <c>orden</c> = menor
    /// prioridad = primero en la extracción inversa); un <c>ORDER BY</c>
    /// dentro de la función no está garantizado sin <c>TOP</c>/<c>OFFSET</c>,
    /// mismo criterio que <c>fn_PrioridadRelevo</c> (E9.2).
    ///
    /// Deliberadamente **solo la derivación del orden** — el disparador
    /// ("solo con la L8 completamente vacía") es E10.2, y el piso de
    /// seguridad que hace inmune a una línea es E10.3; esta función no
    /// sabe nada de ninguno de los dos todavía.
    /// </summary>
    public partial class OrdenExtraccionInversa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_OrdenExtraccionInversa (@linea_solicitante TINYINT)
                RETURNS TABLE AS
                RETURN
                    SELECT pl.linea_id, pl.orden
                      FROM PrioridadLinea pl
                      JOIN Linea l ON l.Id = pl.linea_id
                     WHERE pl.vigente_hasta IS NULL
                       AND l.es_bolson = 0
                       AND l.activa_hoy = 1
                       AND pl.linea_id <> @linea_solicitante;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_OrdenExtraccionInversa;");
        }
    }
}

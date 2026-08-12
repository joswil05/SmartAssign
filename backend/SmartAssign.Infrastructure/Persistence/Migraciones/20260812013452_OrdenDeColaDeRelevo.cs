using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E9.2 (docs/PROGRESO.md): <c>fn_PrioridadRelevo</c> — el orden
    /// de la cola de relevos pendientes, literal 00 §B3:
    ///
    /// 1. Nivel **crítico** antes que **sugerido**.
    /// 2. Mayor **exceso relativo** sobre su propio umbral, en %.
    /// 3. **FIFO** por antigüedad de la solicitud.
    ///
    /// Sin cambios de esquema — solo una función escalar que traduce
    /// <c>nivel</c> a un rango ordenable, mismo patrón que
    /// <c>fn_NivelFatiga</c> (E7.3): una sola pieza pequeña y probada,
    /// que E9.4 (`vw_SolicitudRelevo_L8`) y E9.5 (`sp_ProponerRelevista`)
    /// componen en su propio <c>ORDER BY</c> — un <c>ORDER BY</c> dentro
    /// de un <c>CREATE VIEW</c> no está garantizado por el motor sin
    /// <c>TOP</c>/<c>OFFSET</c>, así que el orden real siempre lo aplica
    /// quien consume, no la vista.
    ///
    /// Incluye <c>nivel='maxima'</c> (rango 1, por delante de
    /// <c>critico</c>) aunque todavía nada lo produce — la excepción de
    /// máxima prioridad de B3 es explícita: "una solicitud generada por
    /// vacante crítica de puesto fijo (C15-N1) encabeza la cola por
    /// delante de cualquier fatiga". <c>CK_SR_nivel</c> (E9.1) ya admite
    /// el valor; escribir la función completa ahora evita tener que
    /// tocar el orden otra vez cuando C15 llegue.
    /// </summary>
    public partial class OrdenDeColaDeRelevo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_PrioridadRelevo (@nivel VARCHAR(12))
                RETURNS TINYINT AS
                BEGIN
                    RETURN CASE @nivel
                        WHEN 'maxima'   THEN 1  -- B3: vacante crítica (C15-N1), por delante de cualquier fatiga
                        WHEN 'critico'  THEN 2
                        WHEN 'sugerido' THEN 3
                        ELSE NULL
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_PrioridadRelevo;");
        }
    }
}

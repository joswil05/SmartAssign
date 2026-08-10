using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E7.2 (docs/PROGRESO.md): exceso relativo en % (00 §A4, §B3) —
    /// "todo ordenamiento por fatiga usa exceso relativo sobre el umbral
    /// propio, expresado en porcentaje", nunca minutos absolutos, porque
    /// con umbrales por puesto los minutos dejan de ser comparables entre
    /// puestos (70 min en un puesto de umbral 60 es peor que 70 min en
    /// uno de umbral 120 — 00 §A4).
    ///
    /// El umbral de referencia es siempre el **sugerido** (03 §2: "la
    /// barra se llena progresivamente desde el minuto cero", "porcentaje
    /// sobre el umbral propio del puesto") — es el primer punto de
    /// referencia que existe; el crítico se compara aparte, en minutos
    /// absolutos contra <c>fn_UmbralFatigaCriticoMinutos</c> (E7.1), en
    /// la UT que clasifique niveles (E7.3, todavía no esta). Tipo
    /// <c>DECIMAL(6,2)</c> — ya catalogado en 04 §9 como la forma
    /// de <c>exceso_relativo</c>.
    /// </summary>
    public partial class ExcesoRelativoFatiga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── fn_ExcesoRelativoFatiga (00 §A4, §B3) ──
            // NULL cuando fn_MinutosEnPuesto ya es NULL (fatiga no
            // aplica: fijo/inactivo/sin ocupante, E7.1) o cuando no hay
            // umbral sugerido que sirva de referencia (ni propio del
            // puesto ni de planta, R2) — nunca un porcentaje sin
            // denominador real. Guarda extra contra umbral = 0 (división
            // por cero): no debería ocurrir con datos reales, pero la
            // función no debe reventar si algún día aparece.
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_ExcesoRelativoFatiga (@puesto_id INT)
                RETURNS DECIMAL(6,2) AS
                BEGIN
                    DECLARE @minutos INT = dbo.fn_MinutosEnPuesto(@puesto_id);
                    DECLARE @umbral_sugerido INT = dbo.fn_UmbralFatigaSugeridoMinutos(@puesto_id);

                    IF @minutos IS NULL OR @umbral_sugerido IS NULL OR @umbral_sugerido = 0
                        RETURN NULL;

                    RETURN CAST(@minutos AS DECIMAL(10,4)) * 100.0 / @umbral_sugerido;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_ExcesoRelativoFatiga;");
        }
    }
}

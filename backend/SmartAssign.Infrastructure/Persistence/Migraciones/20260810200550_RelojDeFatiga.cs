using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E7.1 (docs/PROGRESO.md): el reloj de fatiga — cuánto lleva cada
    /// operario en su puesto rotativo actual (fuente §9.1: "La fatiga
    /// solo aplica a puestos rotativos") y cuál es el umbral propio de ese
    /// puesto (00 §A4: "cada puesto tiene su propio tiempo de fatiga").
    ///
    /// Deliberadamente NO clasifica en niveles (normal/sugerido/crítico,
    /// §9.1) ni calcula el exceso relativo en % — eso es E7.2/E7.3. Esta
    /// UT solo entrega el reloj y la resolución del umbral, para que las
    /// siguientes los comparen sin reimplementarlos.
    /// </summary>
    public partial class RelojDeFatiga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── fn_MinutosEnPuesto (§9.1) ──
            // Minutos desde Asignacion.inicio para el ocupante ACTUAL
            // (fin IS NULL — UX_Asig_puesto_activo garantiza que hay a lo
            // sumo una fila así). NULL cuando la fatiga simplemente no
            // aplica: puesto fijo (§9.1, "los operadores en puestos fijos
            // no entran en este cálculo"), inactivo, o sin nadie asignado
            // ahora mismo — nunca 0 disfrazando "no hay dato".
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_MinutosEnPuesto (@puesto_id INT)
                RETURNS INT AS
                BEGIN
                    DECLARE @tipo VARCHAR(10);
                    DECLARE @activo BIT;
                    DECLARE @inicio DATETIME2(0);

                    SELECT @tipo = tipo, @activo = activo FROM Puesto WHERE Id = @puesto_id;

                    IF @tipo <> 'rotativo' OR @activo = 0
                        RETURN NULL;

                    SELECT @inicio = inicio FROM Asignacion WHERE puesto_id = @puesto_id AND fin IS NULL;

                    IF @inicio IS NULL
                        RETURN NULL;

                    RETURN DATEDIFF(MINUTE, @inicio, SYSUTCDATETIME());
                END;
                """);

            // ── fn_UmbralFatigaSugeridoMinutos (00 §A4) ──
            // "Umbral propio": primero el del puesto (horas_en_puesto,
            // A14 — ya en horas, se convierte a minutos aquí una sola vez
            // para que el reloj y el umbral compartan unidad). Si el
            // puesto no lo trae todavía (dato real de calibración
            // pendiente, A4), cae al valor de planta en
            // Parametro['fatiga_sugerido_default_min'] — mismo nombre que
            // ya cataloga 04 §9. Sin ninguno de los dos, NULL: la regla
            // no aplica todavía, nunca un umbral inventado (R2,
            // docs/06_ROADMAP.md P5.1) — mismo criterio que
            // fn_VentanaArranqueBloquea (E4.5) con ventana_arranque_min.
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_UmbralFatigaSugeridoMinutos (@puesto_id INT)
                RETURNS INT AS
                BEGIN
                    DECLARE @tipo VARCHAR(10);
                    DECLARE @horas SMALLINT;

                    SELECT @tipo = tipo, @horas = horas_en_puesto FROM Puesto WHERE Id = @puesto_id;

                    IF @tipo <> 'rotativo'
                        RETURN NULL;

                    IF @horas IS NOT NULL
                        RETURN @horas * 60;

                    RETURN (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'fatiga_sugerido_default_min');
                END;
                """);

            // ── fn_UmbralFatigaCriticoMinutos (00 §A4) ── mismo criterio
            // que el sugerido, con umbral_critico_horas y
            // Parametro['fatiga_critico_default_min'].
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_UmbralFatigaCriticoMinutos (@puesto_id INT)
                RETURNS INT AS
                BEGIN
                    DECLARE @tipo VARCHAR(10);
                    DECLARE @horas SMALLINT;

                    SELECT @tipo = tipo, @horas = umbral_critico_horas FROM Puesto WHERE Id = @puesto_id;

                    IF @tipo <> 'rotativo'
                        RETURN NULL;

                    IF @horas IS NOT NULL
                        RETURN @horas * 60;

                    RETURN (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'fatiga_critico_default_min');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_UmbralFatigaCriticoMinutos;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_UmbralFatigaSugeridoMinutos;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_MinutosEnPuesto;");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E7.3 (docs/PROGRESO.md): la clasificación de fatiga en los tres
    /// niveles de §9.1 (normal / relevo sugerido / relevo crítico),
    /// cerrando E7: "los operadores en puestos fijos no entran en este
    /// cálculo" (§9.1, §5.1) y "la fatiga es propiedad del puesto
    /// ocupado, no de la categoría de la persona" (00 §A7) — ambas ya
    /// gratis por construcción, porque <c>fn_MinutosEnPuesto</c> (E7.1)
    /// nunca lee <c>Personal.categoria</c>, solo <c>Puesto</c> y la
    /// asignación activa.
    /// </summary>
    public partial class NivelDeFatiga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── fn_NivelFatiga (§9.1) ──
            // Reutiliza fn_MinutosEnPuesto/fn_UmbralFatigaSugeridoMinutos/
            // fn_UmbralFatigaCriticoMinutos (E7.1) sin reimplementar la
            // resolución de umbral propio-o-planta que ya hacen. Crítico
            // se evalúa ANTES que sugerido (un puesto por encima de
            // ambos umbrales es crítico, no sugerido). Si un umbral no
            // está calibrado (ni propio ni de planta, E7.1), esa
            // clasificación simplemente no se alcanza — nunca se infiere
            // un umbral que no existe (R2). 'normal' solo se afirma
            // cuando hay al menos un umbral real contra el que se pudo
            // comparar; sin ninguno de los dos, NULL: no hay nada
            // calibrado todavía con qué clasificar, mismo criterio que
            // fn_ExcesoRelativoFatiga (E7.2).
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_NivelFatiga (@puesto_id INT)
                RETURNS VARCHAR(10) AS
                BEGIN
                    DECLARE @minutos INT = dbo.fn_MinutosEnPuesto(@puesto_id);

                    IF @minutos IS NULL
                        RETURN NULL;

                    DECLARE @umbral_critico INT = dbo.fn_UmbralFatigaCriticoMinutos(@puesto_id);
                    DECLARE @umbral_sugerido INT = dbo.fn_UmbralFatigaSugeridoMinutos(@puesto_id);

                    IF @umbral_critico IS NOT NULL AND @minutos >= @umbral_critico
                        RETURN 'critico';

                    IF @umbral_sugerido IS NOT NULL AND @minutos >= @umbral_sugerido
                        RETURN 'sugerido';

                    IF @umbral_critico IS NOT NULL OR @umbral_sugerido IS NOT NULL
                        RETURN 'normal';

                    RETURN NULL;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_NivelFatiga;");
        }
    }
}

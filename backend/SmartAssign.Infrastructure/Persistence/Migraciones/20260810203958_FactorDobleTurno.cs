using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E7.4 (docs/PROGRESO.md): factor de doble turno (00 §B7) — cierra
    /// E7 (4/4). "Subir el factor acelera la aparición de sugerido y
    /// crítico para esa persona, sin tocar código": un multiplicador
    /// configurable sobre el reloj de fatiga, no un umbral distinto.
    ///
    /// `fn_MinutosEnPuestoEfectivos` es una función NUEVA, no un
    /// `CREATE OR ALTER` sobre `fn_MinutosEnPuesto` (E7.1): 01_PRD.md
    /// HU-D2 muestra el aviso a otros supervisores con el minuto LITERAL
    /// ("L4 · Puesto 3 — relevo sugerido · 62 min") — ese número tiene
    /// que seguir siendo el reloj real, sin ajustar, o el supervisor que
    /// lo lee vería un tiempo que no corresponde a nada físico. El
    /// ajuste de B7 solo debe alcanzar la CLASIFICACIÓN (nivel) y el
    /// ORDENAMIENTO (exceso relativo) — por eso `fn_ExcesoRelativoFatiga`
    /// y `fn_NivelFatiga` sí se alteran (`CREATE OR ALTER`, mismo nombre,
    /// mismo contrato de salida) para leer de aquí en vez del reloj
    /// crudo; ningún llamador externo necesita cambiar.
    ///
    /// `factor_doble_turno` con **valor por defecto 1.0 — es el único
    /// caso de todo el motor donde 00 §B7 SÍ especifica el número por
    /// defecto** (no es "a definir" como `ventana_arranque_min` o los
    /// `fatiga_*_default_min` de E7.1): con el parámetro sin sembrar, el
    /// factor es 1.0 igual, comportamiento puramente informativo hasta
    /// que el Coordinador lo suba con datos reales (00 §B7, §12.6).
    /// </summary>
    public partial class FactorDobleTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── fn_MinutosEnPuestoEfectivos (00 §B7) ──
            // NULL si fn_MinutosEnPuesto ya es NULL (fatiga no aplica,
            // E7.1). Sin ocupante en doble turno, el efectivo es el
            // crudo. Con ocupante en doble turno, se multiplica por
            // factor_doble_turno (1.0 si no está sembrado — único
            // default real de todo el motor, ver comentario de arriba).
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_MinutosEnPuestoEfectivos (@puesto_id INT)
                RETURNS INT AS
                BEGIN
                    DECLARE @minutos INT = dbo.fn_MinutosEnPuesto(@puesto_id);

                    IF @minutos IS NULL
                        RETURN NULL;

                    DECLARE @doble_turno BIT = (
                        SELECT p.doble_turno
                          FROM Asignacion a JOIN Personal p ON p.Id = a.personal_id
                         WHERE a.puesto_id = @puesto_id AND a.fin IS NULL
                    );

                    IF ISNULL(@doble_turno, 0) = 0
                        RETURN @minutos;

                    DECLARE @factor DECIMAL(5,2) = ISNULL(
                        (SELECT TRY_CAST(valor AS DECIMAL(5,2)) FROM Parametro WHERE clave = 'factor_doble_turno'),
                        1.0
                    );

                    RETURN CAST(ROUND(@minutos * @factor, 0) AS INT);
                END;
                """);

            // ── fn_ExcesoRelativoFatiga (E7.2) — alterada para leer el
            // reloj EFECTIVO (B7), mismo contrato de salida (% DECIMAL(6,2)).
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_ExcesoRelativoFatiga (@puesto_id INT)
                RETURNS DECIMAL(6,2) AS
                BEGIN
                    DECLARE @minutos INT = dbo.fn_MinutosEnPuestoEfectivos(@puesto_id);
                    DECLARE @umbral_sugerido INT = dbo.fn_UmbralFatigaSugeridoMinutos(@puesto_id);

                    IF @minutos IS NULL OR @umbral_sugerido IS NULL OR @umbral_sugerido = 0
                        RETURN NULL;

                    RETURN CAST(@minutos AS DECIMAL(10,4)) * 100.0 / @umbral_sugerido;
                END;
                """);

            // ── fn_NivelFatiga (E7.3) — alterada para leer el reloj
            // EFECTIVO (B7), mismo contrato de salida (normal/sugerido/critico).
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_NivelFatiga (@puesto_id INT)
                RETURNS VARCHAR(10) AS
                BEGIN
                    DECLARE @minutos INT = dbo.fn_MinutosEnPuestoEfectivos(@puesto_id);

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
            // Revierte fn_ExcesoRelativoFatiga/fn_NivelFatiga a su forma de
            // E7.2/E7.3 (reloj crudo) y quita la función nueva.
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

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_MinutosEnPuestoEfectivos;");
        }
    }
}

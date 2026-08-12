using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E11.8 (docs/PROGRESO.md), cierra E11 (8/8): "todo registro empuja
    /// a los dos paneles" — 00 §C4. Sin cambios de esquema.
    ///
    /// **Decisión de alcance confirmada con el cliente (R2), antes de tocar
    /// código.** 06_ROADMAP.md (F9) dice explícitamente *"No incluye:
    /// Difusión en vivo (es F10)"* — el SignalR + la bandeja de salida
    /// transaccional que de verdad "empujan" (C4 punto 1: "emite un evento
    /// por el canal de tiempo real") son E12.1/E12.3, que no existen
    /// todavía. F9 se verifica con: *"el tiempo acumulado aparece al
    /// instante en los dos paneles, con el mismo número"* — "al instante"
    /// es sobre una CONSULTA fresca, no sobre difusión sin pedirla; la
    /// garantía real de esta etapa es la de C4 punto 3: **"el cálculo vive
    /// en el servidor, nunca en el dispositivo... dos paneles calculando
    /// por su cuenta acaban mostrando cifras distintas"** — nunca hay caché
    /// en este backend, así que esa garantía ya es estructuralmente cierta;
    /// esta UT la deja demostrada con pruebas de integración explícitas,
    /// y cierra el único hueco real y ya resoluble de los cinco
    /// indicadores de C4.
    ///
    /// De los cinco indicadores de C4 ("eficiencia de su línea · tiempo de
    /// paro acumulado del turno · desperdicio del lote por causa ·
    /// cobertura de la línea · puestos en fatiga"):
    /// - **Eficiencia** ya la resuelve `sp_CalcularEficiencia` (E11.7),
    ///   igual para supervisor (su línea) y Coordinador (cada una de las
    ///   10 + agregado de planta) — es la misma llamada, nunca dos
    ///   cálculos distintos.
    /// - **Desperdicio del lote por causa**: ya son columnas directas de
    ///   `Desperdicio` (E11.6) — consultable tal cual, sin agregador nuevo.
    /// - **Puestos en fatiga**: `fn_NivelFatiga` (E7.3) ya existe por
    ///   puesto — un conteo por línea es una consulta directa sobre esa
    ///   función, no un procedimiento nuevo.
    /// - **Tiempo de paro acumulado del turno**: `sp_CalcularEficiencia`
    ///   ya lo CALCULA internamente (`@paros_min`, para restarlo del
    ///   tiempo efectivo) pero nunca lo devolvía — hueco real y acotado,
    ///   se cierra aquí con un output nuevo, `@paros_acumulados_min INT =
    ///   0 OUTPUT` (con default, ninguna llamada existente se rompe).
    /// - **Cobertura de la línea**: sin definición en ningún documento
    ///   (ni fuentes, ni 01-05) — no se inventa una fórmula (R2); queda
    ///   como hueco de la fuente, documentado aquí, no de esta UT.
    /// </summary>
    public partial class TodoRegistroEmpujaALosDosPaneles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── sp_CalcularEficiencia (E11.7) extendido — agrega el tiempo
            // de paro acumulado del turno como salida propia (00 §C4).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CalcularEficiencia
                    @jornada_linea_id INT,
                    @eficiencia_pct DECIMAL(9,4) OUTPUT,
                    @tramo VARCHAR(10) OUTPUT,
                    @produccion_real DECIMAL(14,2) OUTPUT,
                    @tiempo_efectivo_marcha_min INT OUTPUT,
                    @ritmo_teorico_hora DECIMAL(10,2) OUTPUT,
                    @ultima_actualizacion_produccion DATETIME2 OUTPUT,
                    @paros_acumulados_min INT = 0 OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @eficiencia_pct = NULL; SET @tramo = NULL; SET @produccion_real = NULL;
                    SET @tiempo_efectivo_marcha_min = NULL; SET @ritmo_teorico_hora = NULL;
                    SET @ultima_actualizacion_produccion = NULL; SET @paros_acumulados_min = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF NOT EXISTS (SELECT 1 FROM JornadaLinea WHERE Id = @jornada_linea_id)
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_INEXISTENTE';
                        SET @mensaje = 'Esa jornada-línea no existe.';
                        RETURN;
                    END

                    DECLARE @arrancado_en DATETIME2, @cerrado_en DATETIME2, @sku_id INT;
                    SELECT @arrancado_en = arrancado_en, @cerrado_en = cerrado_en, @sku_id = sku_id
                      FROM JornadaLinea WHERE Id = @jornada_linea_id;

                    IF @arrancado_en IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_NO_ARRANCADA';
                        SET @mensaje = 'Esta línea todavía no arrancó turno — no hay tiempo transcurrido que medir.';
                        RETURN;
                    END

                    SET @ritmo_teorico_hora = (SELECT ritmo_teorico_hora FROM Sku WHERE Id = @sku_id);

                    -- §11.4: tiempo de turno transcurrido − suma de paros. "Hasta ahora"
                    -- si la jornada sigue abierta, "hasta el cierre" si ya cerró —
                    -- para que la eficiencia de un turno cerrado no siga creciendo.
                    DECLARE @hasta DATETIME2(0) = ISNULL(@cerrado_en, SYSUTCDATETIME());
                    DECLARE @transcurrido_min INT = DATEDIFF(MINUTE, @arrancado_en, @hasta);
                    DECLARE @paros_min INT = ISNULL((
                        SELECT SUM(DATEDIFF(MINUTE, inicio, ISNULL(fin, @hasta)))
                          FROM Paro WHERE jornada_linea_id = @jornada_linea_id
                    ), 0);
                    SET @paros_acumulados_min = @paros_min; -- 00 §C4: indicador propio del panel.
                    SET @tiempo_efectivo_marcha_min = @transcurrido_min - @paros_min;
                    IF @tiempo_efectivo_marcha_min < 0 SET @tiempo_efectivo_marcha_min = 0;

                    -- Producción real: lotes ya cerrados + avances del lote que sigue
                    -- abierto (nunca las dos fuentes del mismo lote a la vez).
                    DECLARE @produccion_cerrados DECIMAL(14,2) = ISNULL((
                        SELECT SUM(produccion_real) FROM Lote
                         WHERE jornada_linea_id = @jornada_linea_id AND cerrado_en IS NOT NULL
                    ), 0);
                    DECLARE @lote_abierto_id INT = (
                        SELECT Id FROM Lote WHERE jornada_linea_id = @jornada_linea_id AND cerrado_en IS NULL);
                    DECLARE @produccion_avance DECIMAL(14,2) = ISNULL((
                        SELECT SUM(cantidad) FROM ProduccionAvance WHERE lote_id = @lote_abierto_id
                    ), 0);
                    SET @produccion_real = @produccion_cerrados + @produccion_avance;

                    SELECT @ultima_actualizacion_produccion = MAX(momento) FROM (
                        SELECT MAX(cerrado_en) AS momento FROM Lote
                         WHERE jornada_linea_id = @jornada_linea_id AND cerrado_en IS NOT NULL
                        UNION ALL
                        SELECT MAX(registrado_en) FROM ProduccionAvance WHERE lote_id = @lote_abierto_id
                    ) AS ultimos(momento);

                    -- Eficiencia = producción real / (tiempo efectivo en horas × ritmo teórico).
                    -- Denominador cero (nada transcurrido todavía) → NULL, nunca 0 % inventado.
                    DECLARE @denominador DECIMAL(18,4) = (@tiempo_efectivo_marcha_min / 60.0) * @ritmo_teorico_hora;
                    IF @denominador > 0
                        SET @eficiencia_pct = @produccion_real * 100.0 / @denominador;

                    IF @eficiencia_pct IS NOT NULL
                    BEGIN
                        -- "a definir" en 04 §9 — mismo criterio que fn_NivelFatiga (E7.3):
                        -- sin ningún umbral sembrado, no se clasifica (R2).
                        DECLARE @umbral_aceptable DECIMAL(5,2) = (
                            SELECT TRY_CAST(valor AS DECIMAL(5,2)) FROM Parametro WHERE clave = 'eficiencia_umbral_aceptable_pct');
                        DECLARE @umbral_optimo DECIMAL(5,2) = (
                            SELECT TRY_CAST(valor AS DECIMAL(5,2)) FROM Parametro WHERE clave = 'eficiencia_umbral_optimo_pct');

                        IF @umbral_aceptable IS NOT NULL AND @eficiencia_pct < @umbral_aceptable
                            SET @tramo = 'critico';
                        ELSE IF @umbral_optimo IS NOT NULL AND @eficiencia_pct >= @umbral_optimo
                            SET @tramo = 'optimo';
                        ELSE IF @umbral_aceptable IS NOT NULL OR @umbral_optimo IS NOT NULL
                            SET @tramo = 'aceptable';
                    END
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte sp_CalcularEficiencia a su forma de E11.7, sin @paros_acumulados_min.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CalcularEficiencia
                    @jornada_linea_id INT,
                    @eficiencia_pct DECIMAL(9,4) OUTPUT,
                    @tramo VARCHAR(10) OUTPUT,
                    @produccion_real DECIMAL(14,2) OUTPUT,
                    @tiempo_efectivo_marcha_min INT OUTPUT,
                    @ritmo_teorico_hora DECIMAL(10,2) OUTPUT,
                    @ultima_actualizacion_produccion DATETIME2 OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @eficiencia_pct = NULL; SET @tramo = NULL; SET @produccion_real = NULL;
                    SET @tiempo_efectivo_marcha_min = NULL; SET @ritmo_teorico_hora = NULL;
                    SET @ultima_actualizacion_produccion = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF NOT EXISTS (SELECT 1 FROM JornadaLinea WHERE Id = @jornada_linea_id)
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_INEXISTENTE';
                        SET @mensaje = 'Esa jornada-línea no existe.';
                        RETURN;
                    END

                    DECLARE @arrancado_en DATETIME2, @cerrado_en DATETIME2, @sku_id INT;
                    SELECT @arrancado_en = arrancado_en, @cerrado_en = cerrado_en, @sku_id = sku_id
                      FROM JornadaLinea WHERE Id = @jornada_linea_id;

                    IF @arrancado_en IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_NO_ARRANCADA';
                        SET @mensaje = 'Esta línea todavía no arrancó turno — no hay tiempo transcurrido que medir.';
                        RETURN;
                    END

                    SET @ritmo_teorico_hora = (SELECT ritmo_teorico_hora FROM Sku WHERE Id = @sku_id);

                    DECLARE @hasta DATETIME2(0) = ISNULL(@cerrado_en, SYSUTCDATETIME());
                    DECLARE @transcurrido_min INT = DATEDIFF(MINUTE, @arrancado_en, @hasta);
                    DECLARE @paros_min INT = ISNULL((
                        SELECT SUM(DATEDIFF(MINUTE, inicio, ISNULL(fin, @hasta)))
                          FROM Paro WHERE jornada_linea_id = @jornada_linea_id
                    ), 0);
                    SET @tiempo_efectivo_marcha_min = @transcurrido_min - @paros_min;
                    IF @tiempo_efectivo_marcha_min < 0 SET @tiempo_efectivo_marcha_min = 0;

                    DECLARE @produccion_cerrados DECIMAL(14,2) = ISNULL((
                        SELECT SUM(produccion_real) FROM Lote
                         WHERE jornada_linea_id = @jornada_linea_id AND cerrado_en IS NOT NULL
                    ), 0);
                    DECLARE @lote_abierto_id INT = (
                        SELECT Id FROM Lote WHERE jornada_linea_id = @jornada_linea_id AND cerrado_en IS NULL);
                    DECLARE @produccion_avance DECIMAL(14,2) = ISNULL((
                        SELECT SUM(cantidad) FROM ProduccionAvance WHERE lote_id = @lote_abierto_id
                    ), 0);
                    SET @produccion_real = @produccion_cerrados + @produccion_avance;

                    SELECT @ultima_actualizacion_produccion = MAX(momento) FROM (
                        SELECT MAX(cerrado_en) AS momento FROM Lote
                         WHERE jornada_linea_id = @jornada_linea_id AND cerrado_en IS NOT NULL
                        UNION ALL
                        SELECT MAX(registrado_en) FROM ProduccionAvance WHERE lote_id = @lote_abierto_id
                    ) AS ultimos(momento);

                    DECLARE @denominador DECIMAL(18,4) = (@tiempo_efectivo_marcha_min / 60.0) * @ritmo_teorico_hora;
                    IF @denominador > 0
                        SET @eficiencia_pct = @produccion_real * 100.0 / @denominador;

                    IF @eficiencia_pct IS NOT NULL
                    BEGIN
                        DECLARE @umbral_aceptable DECIMAL(5,2) = (
                            SELECT TRY_CAST(valor AS DECIMAL(5,2)) FROM Parametro WHERE clave = 'eficiencia_umbral_aceptable_pct');
                        DECLARE @umbral_optimo DECIMAL(5,2) = (
                            SELECT TRY_CAST(valor AS DECIMAL(5,2)) FROM Parametro WHERE clave = 'eficiencia_umbral_optimo_pct');

                        IF @umbral_aceptable IS NOT NULL AND @eficiencia_pct < @umbral_aceptable
                            SET @tramo = 'critico';
                        ELSE IF @umbral_optimo IS NOT NULL AND @eficiencia_pct >= @umbral_optimo
                            SET @tramo = 'optimo';
                        ELSE IF @umbral_aceptable IS NOT NULL OR @umbral_optimo IS NOT NULL
                            SET @tramo = 'aceptable';
                    END
                END;
                """);
        }
    }
}

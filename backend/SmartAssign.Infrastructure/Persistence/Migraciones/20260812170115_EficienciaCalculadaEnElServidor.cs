using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E11.7 (docs/PROGRESO.md): eficiencia calculada en el servidor
    /// (§11.4, 00 §C4, HU-F3). 04 §4.3 ya traía <c>ProduccionAvance</c>
    /// especificada — tabla nueva de una sola vez, mismo criterio que
    /// <c>Desperdicio</c> en E11.6. Sin SP propio de escritura: el
    /// catálogo de 04 §7.4 no lista ninguno para ella ("un contador
    /// simple", literal de C4) — se inserta directo, igual que <c>Lote</c>
    /// se sembraba directo en las pruebas de E11.5/E11.6 antes de tener
    /// un SP que lo tocara.
    ///
    /// <c>sp_CalcularEficiencia</c> (nuevo, catálogo 04 §7.4) implementa
    /// la fórmula literal de §11.4:
    ///
    /// <c>Eficiencia = Producción real / (Tiempo efectivo de marcha × Ritmo teórico del SKU)</c>
    ///
    /// - **Producción real** = SUM(<c>Lote.produccion_real</c>) de los lotes
    ///   YA CERRADOS de la jornada-línea + SUM(<c>ProduccionAvance.cantidad</c>)
    ///   del lote que sigue abierto (nunca ambas fuentes del mismo lote —
    ///   evita contar dos veces la misma producción).
    /// - **Tiempo efectivo de marcha** = tiempo de turno transcurrido
    ///   (hasta <c>cerrado_en</c> si la jornada ya cerró, si no hasta
    ///   ahora) − suma de paros de esa jornada-línea (el paro abierto
    ///   cuenta hasta ahora también). Nunca negativo.
    /// - **Ritmo teórico del SKU** = el SKU **vigente** de la jornada-línea
    ///   (<c>JornadaLinea.sku_id</c>) — la fórmula habla de "el SKU" en
    ///   singular, no de un promedio ponderado entre los SKU que pudo
    ///   haber tenido la línea hoy; no se inventa esa ponderación (R2).
    ///
    /// Umbrales de tramo (<c>eficiencia_umbral_optimo_pct</c>/
    /// <c>eficiencia_umbral_aceptable_pct</c>, 04 §9) siguen "a definir":
    /// sin sembrar, <c>@tramo</c> resuelve NULL — mismo criterio exacto
    /// que <c>fn_NivelFatiga</c> (E7.3): con un solo umbral configurado
    /// ya hay algo contra qué clasificar (cae a 'aceptable' si no toca
    /// ninguno de los dos extremos); sin ninguno, no se clasifica.
    ///
    /// **Honestidad del dato (HU-F3):** "nunca un número inventado" — con
    /// tiempo efectivo cero (jornada recién arrancada, nada transcurrido
    /// todavía) la eficiencia es NULL, no 0 %: dividir entre cero no es
    /// un dato, es ausencia de dato. <c>@ultima_actualizacion_produccion</c>
    /// devuelve el momento del último registro real (avance o cierre de
    /// lote) para que quien presente el dato arme el "estimada desde hace
    /// N min" — ese texto y el sello de frescura genérico (D4) no son de
    /// esta UT, ya existen como mecanismo aparte.
    /// </summary>
    public partial class EficienciaCalculadaEnElServidor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProduccionAvance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lote_id = table.Column<int>(type: "int", nullable: false),
                    cantidad = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    registrado_por = table.Column<int>(type: "int", nullable: false),
                    registrado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProduccionAvance", x => x.Id);
                    table.CheckConstraint("CK_Avance_cantidad", "cantidad >= 0");
                    table.ForeignKey(
                        name: "FK_ProduccionAvance_Lote_lote_id",
                        column: x => x.lote_id,
                        principalTable: "Lote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProduccionAvance_Usuario_registrado_por",
                        column: x => x.registrado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProduccionAvance_lote_id",
                table: "ProduccionAvance",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProduccionAvance_registrado_por",
                table: "ProduccionAvance",
                column: "registrado_por");

            // ── sp_CalcularEficiencia (nuevo, 04 §7.4) — §11.4, 00 §C4 ──
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

                    -- §11.4: tiempo de turno transcurrido − suma de paros. "Hasta ahora"
                    -- si la jornada sigue abierta, "hasta el cierre" si ya cerró —
                    -- para que la eficiencia de un turno cerrado no siga creciendo.
                    DECLARE @hasta DATETIME2(0) = ISNULL(@cerrado_en, SYSUTCDATETIME());
                    DECLARE @transcurrido_min INT = DATEDIFF(MINUTE, @arrancado_en, @hasta);
                    DECLARE @paros_min INT = ISNULL((
                        SELECT SUM(DATEDIFF(MINUTE, inicio, ISNULL(fin, @hasta)))
                          FROM Paro WHERE jornada_linea_id = @jornada_linea_id
                    ), 0);
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
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CalcularEficiencia;");

            migrationBuilder.DropTable(
                name: "ProduccionAvance");
        }
    }
}

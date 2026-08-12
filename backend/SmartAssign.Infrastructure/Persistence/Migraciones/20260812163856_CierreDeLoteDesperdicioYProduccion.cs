using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E11.6 (docs/PROGRESO.md): desperdicio + producción + justificación
    /// sobre umbral, al cierre de lote (§11.3, 00 §C4, HU-F2). 04 §4.3 ya
    /// traía el esquema completo de <c>Desperdicio</c> — tabla nueva de una
    /// sola vez, mismo criterio que <c>Asignacion</c> en E4.1.
    ///
    /// <c>sp_CerrarLote</c> (nuevo, catálogo de 04 §9): valida que el lote
    /// exista y siga abierto, rechaza cantidades negativas, y — la regla
    /// que le da nombre a la UT — exige justificación escrita cuando el
    /// daño de proceso supera <c>Parametro['umbral_desperdicio_justificacion_pct']</c>
    /// (04 §9, "a definir"). Si el parámetro no está sembrado todavía,
    /// <c>@umbral_pct</c> resuelve a NULL y la comparación nunca dispara —
    /// mismo criterio que <c>fn_ExcesoRelativoFatiga</c> (E7.2): un umbral
    /// sin configurar no bloquea nada, no se inventa un valor "razonable" (R2).
    ///
    /// **Decisión de alcance confirmada con el cliente (R2):** ni §11.3 ni
    /// 00 §C4 ni 04 §4.3/§9 definen qué compone el "volumen total" del que
    /// habla la fuente ("el daño de proceso supera un umbral... del volumen
    /// total") — no había fórmula en ningún documento declarado en el LEE
    /// ni en ningún otro. Se preguntó explícitamente; la respuesta fue
    /// **% del desperdicio total** (daño de proceso ÷ (daño de origen + daño
    /// de proceso)), la lectura más directa del propio párrafo — no se
    /// adivinó ni se usó "producción real" en el denominador, que el texto
    /// nunca menciona en ese contexto.
    ///
    /// Fuera de alcance a propósito: los "avances parciales" de C4 (contador
    /// simple durante el lote, para la lectura en vivo) son una captura
    /// distinta a la de cierre — ninguna UT del plan los nombra todavía;
    /// no se construye <c>ProduccionAvance</c> aquí. Tampoco se construye
    /// ningún mecanismo de "empuje" a los paneles (04 §9: "dispara
    /// recálculo") — ese es literalmente el objetivo declarado de E11.8
    /// ("Todo registro empuja a los dos paneles"), y el canal de tiempo
    /// real ni siquiera existe todavía (E12). Esta UT deja el dato real
    /// escrito; quien lo empuje es responsabilidad de otra UT.
    /// </summary>
    public partial class CierreDeLoteDesperdicioYProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Desperdicio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lote_id = table.Column<int>(type: "int", nullable: false),
                    dano_origen = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                    dano_proceso = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                    justificacion = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    registrado_por = table.Column<int>(type: "int", nullable: false),
                    registrado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Desperdicio", x => x.Id);
                    table.CheckConstraint("CK_Desp_valores", "dano_origen >= 0 AND dano_proceso >= 0");
                    table.ForeignKey(
                        name: "FK_Desperdicio_Lote_lote_id",
                        column: x => x.lote_id,
                        principalTable: "Lote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desperdicio_Usuario_registrado_por",
                        column: x => x.registrado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Desperdicio_lote_id",
                table: "Desperdicio",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "IX_Desperdicio_registrado_por",
                table: "Desperdicio",
                column: "registrado_por");

            // ── sp_CerrarLote (nuevo, 04 §9) — §11.3, 00 §C4 ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CerrarLote
                    @lote_id INT, @produccion_real DECIMAL(12,2), @dano_origen DECIMAL(12,2), @dano_proceso DECIMAL(12,2),
                    @justificacion NVARCHAR(600) = NULL, @usuario_id INT,
                    @desperdicio_id INT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET @desperdicio_id = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF NOT EXISTS (SELECT 1 FROM Lote WHERE Id = @lote_id)
                    BEGIN
                        SET @codigo_rechazo = 'LOTE_INEXISTENTE';
                        SET @mensaje = 'Ese lote no existe.';
                        RETURN;
                    END

                    IF EXISTS (SELECT 1 FROM Lote WHERE Id = @lote_id AND cerrado_en IS NOT NULL)
                    BEGIN
                        SET @codigo_rechazo = 'LOTE_YA_CERRADO';
                        SET @mensaje = 'Este lote ya está cerrado.';
                        RETURN;
                    END

                    IF @produccion_real < 0 OR @dano_origen < 0 OR @dano_proceso < 0
                    BEGIN
                        SET @codigo_rechazo = 'VALORES_NEGATIVOS';
                        SET @mensaje = 'La producción y el desperdicio no pueden ser negativos.';
                        RETURN;
                    END

                    -- §11.3: % del daño de proceso sobre el desperdicio TOTAL
                    -- (origen + proceso) — decisión confirmada con el cliente,
                    -- ver docstring de la migración (R2, no estaba en el LEE).
                    DECLARE @desperdicio_total DECIMAL(13,2) = @dano_origen + @dano_proceso;
                    DECLARE @pct_proceso DECIMAL(9,4) = CASE WHEN @desperdicio_total = 0 THEN 0
                        ELSE @dano_proceso * 100.0 / @desperdicio_total END;

                    -- "a definir" en 04 §9 — sin sembrar, @umbral_pct queda NULL y
                    -- la comparación de abajo nunca es verdadera (mismo criterio
                    -- que fn_ExcesoRelativoFatiga, E7.2: sin umbral configurado no
                    -- se bloquea nada, no se inventa un valor "razonable").
                    DECLARE @umbral_pct DECIMAL(5,2) = (
                        SELECT TRY_CAST(valor AS DECIMAL(5,2)) FROM Parametro WHERE clave = 'umbral_desperdicio_justificacion_pct');

                    IF @umbral_pct IS NOT NULL AND @pct_proceso > @umbral_pct
                       AND (@justificacion IS NULL OR LEN(LTRIM(RTRIM(@justificacion))) = 0)
                    BEGIN
                        SET @codigo_rechazo = 'JUSTIFICACION_REQUERIDA';
                        SET @mensaje = 'El daño de proceso supera el umbral configurado. Escribe una justificación antes de confirmar.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        -- Bloqueo determinista sobre la fila que se va a cerrar —
                        -- mismo patrón que sp_RegistrarParo/sp_CambiarSKU (E11.2/E11.5).
                        SELECT 1 FROM Lote WITH (UPDLOCK, HOLDLOCK) WHERE Id = @lote_id;

                        UPDATE Lote SET cerrado_en = SYSUTCDATETIME(), produccion_real = @produccion_real
                         WHERE Id = @lote_id;

                        INSERT INTO Desperdicio (lote_id, dano_origen, dano_proceso, justificacion, registrado_por)
                        VALUES (@lote_id, @dano_origen, @dano_proceso, NULLIF(LTRIM(RTRIM(@justificacion)), ''), @usuario_id);
                        SET @desperdicio_id = SCOPE_IDENTITY();
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CerrarLote;");

            migrationBuilder.DropTable(
                name: "Desperdicio");
        }
    }
}

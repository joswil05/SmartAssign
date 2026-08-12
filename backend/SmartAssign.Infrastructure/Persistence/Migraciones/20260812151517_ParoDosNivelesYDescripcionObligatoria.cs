using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E11.1 (docs/PROGRESO.md): arranca E11 (Contingencias y
    /// estadística). <c>Paro</c> — §11.1, literal: "Clasificación en dos
    /// niveles [...] Descripción obligatoria [...] Cada paro queda
    /// registrado con su duración". <c>CategoriaParo</c>/<c>CausaParo</c>
    /// ya existían completas con su catálogo sembrado desde E0/E1; esta
    /// UT crea solo la tabla que faltaba.
    ///
    /// <c>lote_id</c> es un <c>INT</c> simple, sin FK todavía — 04 §4 lo
    /// declara <c>NULL REFERENCES Lote(Id)</c>, pero <c>Lote</c> no
    /// existe hasta E11.5 (00 §C5). Se añade la referencia real ahí,
    /// mismo criterio de extensión incremental que <c>CK_Mov_motivo</c>
    /// (E10.3): construir con lo que existe, extender cuando la
    /// dependencia llegue.
    ///
    /// <c>sp_RegistrarParo</c> valida la jerarquía de dos niveles
    /// (<c>causa.categoria_id = @categoria_id</c> — nunca una causa de
    /// otra categoría) y la descripción no vacía con un código de
    /// rechazo claro, aunque <c>CK_Paro_descripcion</c> ya la protegería
    /// a nivel de base — mismo criterio que el resto del proyecto:
    /// nunca dejar que una violación de CHECK cruda llegue al llamador
    /// cuando el propio procedimiento puede anticiparla. Bloqueo
    /// determinista sobre <c>JornadaLinea</c> antes de comprobar
    /// <c>UX_Paro_abierto</c> — mismo patrón que
    /// <c>sp_MarcarRelevoSolicitado</c> (E9.1): se bloquea la fila padre
    /// para serializar dos intentos concurrentes de abrir un paro en la
    /// misma línea, el índice único filtrado es la red final.
    ///
    /// <c>sp_ReanudarProduccion</c> — §11.1, literal: "Solo se detiene
    /// [el cronómetro] cuando reanuda la producción explícitamente".
    /// Cierra el paro (<c>fin</c>, <c>reanudado_por</c>); sin esta UT no
    /// habría forma de cerrar lo que <c>sp_RegistrarParo</c> abre, así
    /// que ambos verbos van juntos aquí — el plan no reserva un UT
    /// separado para "reanudar".
    ///
    /// **Deliberadamente fuera de alcance** (E11.2, próxima UT): qué le
    /// pasa al personal durante el paro (fijos ocupados, rotativos
    /// liberados con tránsito a la L8) — ninguna de las dos SP de esta
    /// UT toca <c>Asignacion</c>, <c>Movimiento</c> ni
    /// <c>Personal.situacion</c>. Sin auditoría explícita: a diferencia
    /// de C1/C2 (E10.6), §11.1 no dice "queda auditado" — no se añade
    /// ceremonia que la fuente no pide (R2).
    /// </summary>
    public partial class ParoDosNivelesYDescripcionObligatoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Paro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    jornada_linea_id = table.Column<int>(type: "int", nullable: false),
                    lote_id = table.Column<int>(type: "int", nullable: true),
                    categoria_id = table.Column<short>(type: "smallint", nullable: false),
                    causa_id = table.Column<short>(type: "smallint", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    inicio = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    fin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    registrado_por = table.Column<int>(type: "int", nullable: false),
                    reanudado_por = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paro", x => x.Id);
                    table.CheckConstraint("CK_Paro_descripcion", "LEN(LTRIM(RTRIM(descripcion))) > 0");
                    table.CheckConstraint("CK_Paro_fin", "fin IS NULL OR fin >= inicio");
                    table.ForeignKey(
                        name: "FK_Paro_CategoriaParo_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "CategoriaParo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Paro_CausaParo_causa_id",
                        column: x => x.causa_id,
                        principalTable: "CausaParo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Paro_JornadaLinea_jornada_linea_id",
                        column: x => x.jornada_linea_id,
                        principalTable: "JornadaLinea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Paro_Usuario_reanudado_por",
                        column: x => x.reanudado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Paro_Usuario_registrado_por",
                        column: x => x.registrado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Paro_categoria_id",
                table: "Paro",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_Paro_causa_id",
                table: "Paro",
                column: "causa_id");

            migrationBuilder.CreateIndex(
                name: "IX_Paro_reanudado_por",
                table: "Paro",
                column: "reanudado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Paro_registrado_por",
                table: "Paro",
                column: "registrado_por");

            migrationBuilder.CreateIndex(
                name: "UX_Paro_abierto",
                table: "Paro",
                column: "jornada_linea_id",
                unique: true,
                filter: "[fin] IS NULL");

            // ── sp_RegistrarParo (§11.1) ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RegistrarParo
                    @jornada_linea_id INT, @categoria_id SMALLINT, @causa_id SMALLINT,
                    @descripcion NVARCHAR(500), @usuario_id INT,
                    @paro_id INT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @paro_id = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF LEN(LTRIM(RTRIM(ISNULL(@descripcion, '')))) = 0
                    BEGIN
                        SET @codigo_rechazo = 'DESCRIPCION_OBLIGATORIA';
                        SET @mensaje = 'Debes describir qué observaste antes de confirmar el paro.';
                        RETURN;
                    END

                    -- Dos niveles (§11.1): la causa tiene que pertenecer a la categoría elegida.
                    IF NOT EXISTS (SELECT 1 FROM CausaParo WHERE Id = @causa_id AND categoria_id = @categoria_id)
                    BEGIN
                        SET @codigo_rechazo = 'CAUSA_NO_PERTENECE_A_LA_CATEGORIA';
                        SET @mensaje = 'Esa causa no pertenece a la categoría seleccionada.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        -- Bloqueo determinista sobre la fila padre (JornadaLinea) — mismo
                        -- patrón que sp_MarcarRelevoSolicitado (E9.1) para serializar dos
                        -- intentos concurrentes de abrir un paro en la misma línea.
                        SELECT 1 FROM JornadaLinea WITH (UPDLOCK, HOLDLOCK) WHERE Id = @jornada_linea_id;

                        IF EXISTS (SELECT 1 FROM Paro WHERE jornada_linea_id = @jornada_linea_id AND fin IS NULL)
                        BEGIN
                            SET @codigo_rechazo = 'PARO_YA_ABIERTO';
                            SET @mensaje = 'Esta línea ya tiene un paro abierto.';
                            COMMIT;
                            RETURN;
                        END

                        INSERT INTO Paro (jornada_linea_id, categoria_id, causa_id, descripcion, registrado_por)
                        VALUES (@jornada_linea_id, @categoria_id, @causa_id, @descripcion, @usuario_id);
                        SET @paro_id = SCOPE_IDENTITY();
                    COMMIT;
                END;
                """);

            // ── sp_ReanudarProduccion (§11.1, "reanuda la producción explícitamente") ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ReanudarProduccion
                    @paro_id INT, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    BEGIN TRAN;
                        DECLARE @existe BIT = 0, @fin_actual DATETIME2(0);
                        SELECT @existe = 1, @fin_actual = fin
                          FROM Paro WITH (UPDLOCK, HOLDLOCK) WHERE Id = @paro_id;

                        IF @existe = 0
                        BEGIN
                            SET @codigo_rechazo = 'PARO_INEXISTENTE';
                            SET @mensaje = 'Este paro no existe.';
                            COMMIT;
                            RETURN;
                        END

                        IF @fin_actual IS NOT NULL
                        BEGIN
                            SET @codigo_rechazo = 'PARO_YA_RESUELTO';
                            SET @mensaje = 'Este paro ya fue resuelto.';
                            COMMIT;
                            RETURN;
                        END

                        UPDATE Paro SET fin = SYSUTCDATETIME(), reanudado_por = @usuario_id
                         WHERE Id = @paro_id;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ReanudarProduccion;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_RegistrarParo;");

            migrationBuilder.DropTable(
                name: "Paro");
        }
    }
}

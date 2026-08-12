using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E8.1 (docs/PROGRESO.md): arranca E8 (Movimiento entre líneas).
    /// <c>Movimiento</c> creada completa de una vez — 04 §5.2 ya estaba
    /// totalmente especificada, mismo criterio que <c>Asignacion</c> en
    /// E4.1 — aunque esta UT solo implementa el paso 1 del proceso de
    /// tres pasos de la Parte X (despacho). El paso 2 (tránsito inmune)
    /// es E8.2, el paso 3 (recepción) es E8.3; <c>sp_DespacharPersona</c>
    /// ya deja al despachado en <c>estado='en_transito'</c> con
    /// <c>hora_salida</c> real, pero la inmunidad que ese estado debe
    /// otorgar en otros motores todavía no existe en ningún lado —
    /// llega con E8.2.
    ///
    /// El DENY de <c>Movimiento</c> (04 §7.5) también se cierra aquí: la
    /// nota de ingeniería de E4.7 ya lo anticipaba "para la etapa del
    /// motor de movimiento entre líneas (E8)" — es esta, la única UT que
    /// toca el DDL fundacional de la tabla.
    /// </summary>
    public partial class DespachoDeMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Movimiento",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    personal_id = table.Column<int>(type: "int", nullable: false),
                    linea_origen = table.Column<byte>(type: "tinyint", nullable: false),
                    linea_destino = table.Column<byte>(type: "tinyint", nullable: false),
                    puesto_destino_id = table.Column<int>(type: "int", nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "en_transito"),
                    hora_salida = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    hora_llegada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    duracion_seg = table.Column<int>(type: "int", nullable: true, computedColumnSql: "DATEDIFF(SECOND, hora_salida, hora_llegada)", stored: true),
                    despachado_por = table.Column<int>(type: "int", nullable: false),
                    recibido_por = table.Column<int>(type: "int", nullable: true),
                    motivo_rechazo_id = table.Column<short>(type: "smallint", nullable: true),
                    nota_rechazo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    caducado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cancelado_por = table.Column<int>(type: "int", nullable: true),
                    justificacion_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimiento", x => x.Id);
                    table.CheckConstraint("CK_Mov_estado", "estado IN ('en_transito','recibido','rechazado','cancelado')");
                    table.CheckConstraint("CK_Mov_motivo", "motivo IN ('relevo','reasignacion_relevado','liberacion_bolson','paro','cambio_sku','linea_inactiva','rechazo_recepcion','intervencion_coordinador','cobertura_vacante_critica')");
                    table.CheckConstraint("CK_Mov_rechazo", "estado <> 'rechazado' OR motivo_rechazo_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Movimiento_JustificacionExcepcion_justificacion_id",
                        column: x => x.justificacion_id,
                        principalTable: "JustificacionExcepcion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Linea_linea_destino",
                        column: x => x.linea_destino,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Linea_linea_origen",
                        column: x => x.linea_origen,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_MotivoRechazoRecepcion_motivo_rechazo_id",
                        column: x => x.motivo_rechazo_id,
                        principalTable: "MotivoRechazoRecepcion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Personal_personal_id",
                        column: x => x.personal_id,
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Puesto_puesto_destino_id",
                        column: x => x.puesto_destino_id,
                        principalTable: "Puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Usuario_cancelado_por",
                        column: x => x.cancelado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Usuario_despachado_por",
                        column: x => x.despachado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Usuario_recibido_por",
                        column: x => x.recibido_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mov_analitica",
                table: "Movimiento",
                columns: new[] { "linea_origen", "linea_destino" },
                filter: "[estado] = 'recibido'")
                .Annotation("SqlServer:Include", new[] { "duracion_seg" });

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_cancelado_por",
                table: "Movimiento",
                column: "cancelado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_despachado_por",
                table: "Movimiento",
                column: "despachado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_justificacion_id",
                table: "Movimiento",
                column: "justificacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_linea_destino",
                table: "Movimiento",
                column: "linea_destino");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_motivo_rechazo_id",
                table: "Movimiento",
                column: "motivo_rechazo_id");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_recibido_por",
                table: "Movimiento",
                column: "recibido_por");

            migrationBuilder.CreateIndex(
                name: "UX_Mov_reserva",
                table: "Movimiento",
                column: "puesto_destino_id",
                unique: true,
                filter: "[estado] = 'en_transito' AND [puesto_destino_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Mov_transito",
                table: "Movimiento",
                column: "personal_id",
                unique: true,
                filter: "[estado] = 'en_transito'");

            // ── DENY sobre Movimiento (04 §7.5) — cierra la nota de
            // ingeniería de E4.7. Mismo mecanismo de impersonación
            // (EXECUTE AS) que ya prueba el resto de tablas críticas.
            migrationBuilder.Sql("DENY INSERT, UPDATE, DELETE ON dbo.Movimiento TO rol_app;");

            // ── sp_DespacharPersona (Parte X paso 1, §12.7) ──
            // Solo puede despachar a quien esté físicamente en la línea
            // de origen (linea_origen se RESUELVE del propio padrón,
            // nunca se confía en un valor que mande quien llama) y
            // disponible: en Bolsón o presente sin asignar (Parte X,
            // literal). El resto del proceso — inmunidad real durante
            // el tránsito, recepción, rechazo, reserva de puesto,
            // caducidad — llega en E8.2-E8.6; aquí solo se abre la fila
            // y se refleja el cambio de situación de la persona.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_DespacharPersona
                    @personal_id INT, @linea_destino TINYINT, @motivo VARCHAR(30), @usuario_id INT,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @movimiento_id = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @linea_origen TINYINT;
                    DECLARE @situacion VARCHAR(25);

                    SELECT @linea_origen = linea_fisica_actual, @situacion = situacion
                      FROM Personal WHERE Id = @personal_id;

                    IF @linea_origen IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'SIN_LINEA_FISICA';
                        SET @mensaje = 'Esta persona no tiene registrada una ubicación física actual.';
                        RETURN;
                    END

                    IF @situacion NOT IN ('en_bolson', 'presente_sin_asignar')
                    BEGIN
                        SET @codigo_rechazo = 'NO_DISPONIBLE_PARA_DESPACHO';
                        SET @mensaje = 'Solo se puede despachar a alguien en Bolsón o presente sin asignar.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        -- UPDLOCK: dos supervisores no pueden despachar a
                        -- la misma persona a la vez — mismo criterio de
                        -- bloqueo determinista que sp_AsignarPersona (B1).
                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        IF EXISTS (SELECT 1 FROM Movimiento WHERE personal_id = @personal_id AND estado = 'en_transito')
                        BEGIN
                            SET @codigo_rechazo = 'YA_EN_TRANSITO';
                            SET @mensaje = 'Esta persona ya está en tránsito hacia otra línea.';
                            COMMIT;
                            RETURN;
                        END

                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
                        VALUES (@personal_id, @linea_origen, @linea_destino, @motivo, @usuario_id);
                        SET @movimiento_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_DespacharPersona;");

            migrationBuilder.DropTable(
                name: "Movimiento");
        }
    }
}

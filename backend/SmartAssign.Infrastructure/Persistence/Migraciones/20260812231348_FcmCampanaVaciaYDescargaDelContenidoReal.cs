using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E12.4 (docs/PROGRESO.md): "FCM campana vacía" (D5, 05 §2.5).
    ///
    /// <c>Notificacion</c> creada completa — 04 §10 ya la traía
    /// especificada entera, incluido su índice literal
    /// <c>IX_Notif_sin_acuse</c> (pensado para el barrido de acuse/
    /// escalado de E12.6, no para esta UT) — mismo criterio que
    /// <c>Lote</c>/<c>Desperdicio</c> en E11.5/E11.6: cuando el esquema ya
    /// especifica la tabla entera, se construye entera, no a medias.
    ///
    /// <c>sp_EncolarNotificacion</c> (nuevo) es un <c>INSERT</c> plano sin
    /// <c>BEGIN TRAN</c> propio — mismo patrón exacto que
    /// <c>sp_EncolarEvento</c> (E12.3)/<c>sp_RegistrarAuditoria</c> (E4):
    /// al llamarse desde una transacción ya abierta, se une a ella sola.
    /// Si el destinatario no existe todavía no hay a quién notificar —
    /// no se inventa un valor "razonable" (honestidad del dato, §12.4):
    /// el llamador simplemente no invoca el procedimiento cuando no hay
    /// destinatario, tal como <c>sp_DespacharPersona</c> hace abajo.
    ///
    /// **Decisión de alcance (R2, mismo criterio que E12.2/E12.3):** se
    /// construye el MECANISMO completo (tabla + <c>sp_EncolarNotificacion</c>
    /// + <c>NotificacionDispatcher</c>/<c>IServicioNotificacionesPush</c> en
    /// Api, que de verdad recorren <c>DispositivoPush</c> y arman el ping)
    /// y se demuestra con UN productor real de punta a punta:
    /// <c>sp_DespacharPersona</c> (E8.1, con las extensiones de
    /// E8.5/E10.5), extendido para notificar al supervisor de la línea
    /// DESTINO — "TransitoEntrante", el único evento de la tabla de 05
    /// §2.4 que ya lleva identidad a propósito (00 §D1: "el destino sí lo
    /// ve") y el ejemplo LITERAL del propio diagrama de 05 §2.5 ("Viene
    /// María López a relevar el Puesto 3"). No se retrofita en los otros
    /// productores — mismo argumento de escala que E12.3 (nada tiene
    /// endpoint REST todavía).
    ///
    /// Deliberadamente SIN encolar también <c>TransitoEntranteEvento</c>
    /// por la bandeja de SignalR (E12.3): ese catálogo (E12.2) sigue sin
    /// cablear para este productor, y cablearlo aquí sería reabrir el
    /// alcance ya cerrado de E12.3, no el de esta UT — quedan como dos
    /// decisiones independientes, cada una en su propia UT.
    ///
    /// Sin puesto en el mensaje: al DESPACHAR todavía no hay
    /// <c>puesto_destino_id</c> resuelto en todos los casos (es opcional,
    /// E8.5) — el mismo motivo por el que <c>TransitoEntranteEvento</c>
    /// (E12.2) tampoco lo lleva. Se avisa la línea, no un puesto que
    /// puede no existir todavía.
    /// </summary>
    public partial class FcmCampanaVaciaYDescargaDelContenidoReal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notificacion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    criticidad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "normal"),
                    titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    cuerpo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    creada_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    entregada_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    acusada_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    escalada_en = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacion", x => x.Id);
                    table.CheckConstraint("CK_Notif_criticidad", "criticidad IN ('normal','critica')");
                    table.ForeignKey(
                        name: "FK_Notificacion_Usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notif_sin_acuse",
                table: "Notificacion",
                columns: new[] { "criticidad", "creada_en" },
                filter: "[acusada_en] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_usuario_id",
                table: "Notificacion",
                column: "usuario_id");

            // ── sp_EncolarNotificacion (nuevo) — mismo patrón que sp_EncolarEvento ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_EncolarNotificacion
                    @usuario_id INT, @tipo NVARCHAR(35), @titulo NVARCHAR(120), @cuerpo NVARCHAR(300),
                    @criticidad VARCHAR(10) = 'normal',
                    @payload_json NVARCHAR(MAX) = NULL,
                    @notificacion_id BIGINT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    INSERT INTO Notificacion (usuario_id, tipo, criticidad, titulo, cuerpo, payload_json)
                    VALUES (@usuario_id, @tipo, @criticidad, @titulo, @cuerpo, @payload_json);
                    SET @notificacion_id = SCOPE_IDENTITY();
                END
                """);

            // ── sp_DespacharPersona (E8.1, extendido en E8.5/E10.5) —
            // notifica al supervisor de la línea DESTINO dentro de la
            // MISMA transacción que abre el Movimiento (D5/05 §2.5, C4).
            // Preserva íntegra la reserva de puesto (E8.5) y la
            // justificación de excepción (E10.5) — CREATE OR ALTER
            // reemplaza el cuerpo completo, así que esta extensión parte
            // de la forma vigente, no de la original de E8.1.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_DespacharPersona
                    @personal_id INT, @linea_destino TINYINT, @motivo VARCHAR(30), @usuario_id INT,
                    @puesto_destino_id INT = NULL,
                    @justificacion_motivo_id SMALLINT = NULL,
                    @justificacion_texto NVARCHAR(600) = NULL,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @movimiento_id = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF @motivo = 'intervencion_coordinador' AND (@justificacion_motivo_id IS NULL OR @justificacion_texto IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'JUSTIFICACION_REQUERIDA';
                        SET @mensaje = 'Esta operación es una excepción del Coordinador — requiere un formulario de justificación.';
                        RETURN;
                    END

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

                    IF @puesto_destino_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Puesto WHERE Id = @puesto_destino_id)
                    BEGIN
                        SET @codigo_rechazo = 'PUESTO_DESTINO_INEXISTENTE';
                        SET @mensaje = 'El puesto de destino no existe.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        -- Puesto antes que persona (mismo orden que
                        -- sp_AsignarPersona, 04 §7.3) — solo cuando hay
                        -- puesto destino que contender.
                        IF @puesto_destino_id IS NOT NULL
                            SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_destino_id;

                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        IF EXISTS (SELECT 1 FROM Movimiento WHERE personal_id = @personal_id AND estado = 'en_transito')
                        BEGIN
                            SET @codigo_rechazo = 'YA_EN_TRANSITO';
                            SET @mensaje = 'Esta persona ya está en tránsito hacia otra línea.';
                            COMMIT;
                            RETURN;
                        END

                        IF @puesto_destino_id IS NOT NULL AND EXISTS (
                            SELECT 1 FROM Movimiento WHERE puesto_destino_id = @puesto_destino_id AND estado = 'en_transito'
                        )
                        BEGIN
                            -- B4: "el puesto destino no puede estar ya
                            -- reservado por otro relevista en tránsito".
                            SET @codigo_rechazo = 'PUESTO_YA_RESERVADO';
                            SET @mensaje = 'Ese puesto ya tiene otro relevista en camino.';
                            COMMIT;
                            RETURN;
                        END

                        DECLARE @justificacion_id BIGINT = NULL;
                        IF @motivo = 'intervencion_coordinador'
                        BEGIN
                            INSERT INTO JustificacionExcepcion (tipo_excepcion, motivo_id, texto, usuario_id)
                            VALUES ('movimiento_fuera_de_flujo', @justificacion_motivo_id, @justificacion_texto, @usuario_id);
                            SET @justificacion_id = SCOPE_IDENTITY();
                        END

                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por, justificacion_id)
                        VALUES (@personal_id, @linea_origen, @linea_destino, @puesto_destino_id, @motivo, @usuario_id, @justificacion_id);
                        SET @movimiento_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;

                        -- E12.4/D5: avisa al supervisor de la línea DESTINO
                        -- — "TransitoEntrante" sí lleva identidad a
                        -- propósito (00 §D1). Sin destinatario (línea sin
                        -- supervisor asignado ahora mismo) no se notifica a
                        -- nadie — no se inventa uno (honestidad del dato).
                        DECLARE @supervisor_destino INT = (SELECT supervisor_actual FROM Linea WHERE Id = @linea_destino);
                        IF @supervisor_destino IS NOT NULL
                        BEGIN
                            DECLARE @nombre_completo NVARCHAR(150) = (SELECT nombre_completo FROM Personal WHERE Id = @personal_id);
                            DECLARE @codigo_origen NVARCHAR(4) = (SELECT codigo FROM Linea WHERE Id = @linea_origen);
                            DECLARE @cuerpo_notif NVARCHAR(300) = @nombre_completo + N' viene en tránsito desde ' + @codigo_origen + N' hacia tu línea.';
                            DECLARE @payload_notif NVARCHAR(MAX) = JSON_OBJECT(
                                'MovimientoId': @movimiento_id, 'PersonalId': @personal_id, 'NombreCompleto': @nombre_completo,
                                'LineaOrigen': @linea_origen, 'LineaDestino': @linea_destino);
                            DECLARE @notificacion_id BIGINT;
                            -- Mismo motivo que sp_RegistrarParo (E12.3): EXEC
                            -- con parámetro nombrado exige una constante o
                            -- variable, nunca una expresión de función directa.
                            EXEC dbo.sp_EncolarNotificacion
                                @usuario_id = @supervisor_destino,
                                @tipo = 'TransitoEntrante',
                                @titulo = N'Relevista en camino',
                                @cuerpo = @cuerpo_notif,
                                @payload_json = @payload_notif,
                                @notificacion_id = @notificacion_id OUTPUT;
                        END
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte sp_DespacharPersona a su forma de E10.5, sin aviso
            // de tránsito entrante, antes de dropear lo que usa.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_DespacharPersona
                    @personal_id INT, @linea_destino TINYINT, @motivo VARCHAR(30), @usuario_id INT,
                    @puesto_destino_id INT = NULL,
                    @justificacion_motivo_id SMALLINT = NULL,
                    @justificacion_texto NVARCHAR(600) = NULL,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @movimiento_id = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF @motivo = 'intervencion_coordinador' AND (@justificacion_motivo_id IS NULL OR @justificacion_texto IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'JUSTIFICACION_REQUERIDA';
                        SET @mensaje = 'Esta operación es una excepción del Coordinador — requiere un formulario de justificación.';
                        RETURN;
                    END

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

                    IF @puesto_destino_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Puesto WHERE Id = @puesto_destino_id)
                    BEGIN
                        SET @codigo_rechazo = 'PUESTO_DESTINO_INEXISTENTE';
                        SET @mensaje = 'El puesto de destino no existe.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        IF @puesto_destino_id IS NOT NULL
                            SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_destino_id;

                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        IF EXISTS (SELECT 1 FROM Movimiento WHERE personal_id = @personal_id AND estado = 'en_transito')
                        BEGIN
                            SET @codigo_rechazo = 'YA_EN_TRANSITO';
                            SET @mensaje = 'Esta persona ya está en tránsito hacia otra línea.';
                            COMMIT;
                            RETURN;
                        END

                        IF @puesto_destino_id IS NOT NULL AND EXISTS (
                            SELECT 1 FROM Movimiento WHERE puesto_destino_id = @puesto_destino_id AND estado = 'en_transito'
                        )
                        BEGIN
                            SET @codigo_rechazo = 'PUESTO_YA_RESERVADO';
                            SET @mensaje = 'Ese puesto ya tiene otro relevista en camino.';
                            COMMIT;
                            RETURN;
                        END

                        DECLARE @justificacion_id BIGINT = NULL;
                        IF @motivo = 'intervencion_coordinador'
                        BEGIN
                            INSERT INTO JustificacionExcepcion (tipo_excepcion, motivo_id, texto, usuario_id)
                            VALUES ('movimiento_fuera_de_flujo', @justificacion_motivo_id, @justificacion_texto, @usuario_id);
                            SET @justificacion_id = SCOPE_IDENTITY();
                        END

                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por, justificacion_id)
                        VALUES (@personal_id, @linea_origen, @linea_destino, @puesto_destino_id, @motivo, @usuario_id, @justificacion_id);
                        SET @movimiento_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;
                    COMMIT;
                END;
                """);

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_EncolarNotificacion;");

            migrationBuilder.DropTable(
                name: "Notificacion");
        }
    }
}

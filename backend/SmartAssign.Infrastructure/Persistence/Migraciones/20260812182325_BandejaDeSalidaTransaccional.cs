using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E12.3 (docs/PROGRESO.md): bandeja de salida transaccional
    /// (05 §4.1) — "garantiza que el evento se emite si y solo si la
    /// transacción confirmó".
    ///
    /// <c>EventoSaliente</c> nueva (sin especificación previa en 04 —
    /// el patrón está decidido en 05 §4.1/§9, no el esquema; diseño de
    /// esta UT, no una regla de negocio inventada, R2 no aplica a cómo
    /// se modela un patrón de arquitectura ya elegido). <c>sp_EncolarEvento</c>
    /// (nuevo) es un <c>INSERT</c> plano sin `BEGIN TRAN` propio — mismo
    /// patrón exacto que <c>sp_RegistrarAuditoria</c> (E4): al llamarse
    /// desde dentro de una transacción ya abierta, se une a ella sola.
    /// <c>payload_json</c> se construye con <c>JSON_OBJECT()</c> (SQL
    /// Server 2022+, disponible en la instancia real de este proyecto)
    /// en vez de concatenar texto a mano — evita bugs de escapado.
    ///
    /// **Decisión de alcance (R2, mismo criterio que toda la sesión):**
    /// esta UT construye el MECANISMO completo (tabla + `sp_EncolarEvento`
    /// + el <c>EventoSalienteDispatcher</c> en Api que de verdad entrega a
    /// `PlantaHub`) y lo demuestra con UN productor real de punta a punta
    /// — `sp_RegistrarParo`/`sp_ReanudarProduccion` (E11.1/E11.2), porque
    /// §11.1 ya tiene su evento completamente catalogado desde E12.2. No
    /// se retrofita en los otros ~8 SP productores del catálogo de E12.2:
    /// ninguno de ellos tiene todavía un endpoint REST que un cliente
    /// pueda llamar (esta sesión entera dejó esa capa fuera, E9-E12), y
    /// ni siquiera `sp_RegistrarAuditoria` — la "traza obligatoria de
    /// toda operación" de §12.7, siete etapas más vieja — está cableada
    /// en más de 4 de las decenas de SP de escritura de este proyecto.
    /// Wire-in universal es trabajo de una escala muy distinta a "lo que
    /// cabe en una sesión" (07_PLAN §2), y ninguna UT del plan lo pide
    /// explícitamente todavía.
    /// </summary>
    public partial class BandejaDeSalidaTransaccional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventoSaliente",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipo_evento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    grupos = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    procesado_en = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoSaliente", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventoSaliente_pendientes",
                table: "EventoSaliente",
                column: "Id",
                filter: "[procesado_en] IS NULL");

            // ── sp_EncolarEvento (nuevo) — mismo patrón que sp_RegistrarAuditoria ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_EncolarEvento
                    @tipo_evento NVARCHAR(60), @grupos NVARCHAR(400), @payload_json NVARCHAR(MAX)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    INSERT INTO EventoSaliente (tipo_evento, grupos, payload_json)
                    VALUES (@tipo_evento, @grupos, @payload_json);
                END
                """);

            // ── sp_RegistrarParo (E11.1/E11.2) extendido — encola ParoIniciadoEvento
            // dentro de la MISMA transacción que abre el Paro (05 §4.1, C4).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RegistrarParo
                    @jornada_linea_id INT, @categoria_id SMALLINT, @causa_id SMALLINT,
                    @descripcion NVARCHAR(500), @usuario_id INT,
                    @paro_id INT OUTPUT,
                    @rotativos_liberados INT = 0 OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @paro_id = NULL; SET @rotativos_liberados = 0;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

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

                    DECLARE @linea_id TINYINT = (SELECT linea_id FROM JornadaLinea WHERE Id = @jornada_linea_id);
                    DECLARE @linea_bolson TINYINT = (SELECT Id FROM Linea WHERE es_bolson = 1);

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

                        -- E12.2/E12.3: ParoIniciadoEvento → linea:{id}, planta — misma
                        -- transacción, nunca después del COMMIT.
                        DECLARE @categoria_nombre NVARCHAR(60) = (SELECT nombre FROM CategoriaParo WHERE Id = @categoria_id);
                        DECLARE @causa_nombre NVARCHAR(100) = (SELECT nombre FROM CausaParo WHERE Id = @causa_id);
                        DECLARE @payload_paro NVARCHAR(MAX) = JSON_OBJECT(
                            'ParoId': @paro_id, 'LineaId': @linea_id,
                            'Categoria': @categoria_nombre, 'Causa': @causa_nombre, 'Descripcion': @descripcion);
                        -- EXEC con parámetro nombrado exige una constante o variable, nunca
                        -- una expresión de función directa ("Incorrect syntax" si no se
                        -- precomputa) — de ahí la variable intermedia.
                        DECLARE @grupos_paro NVARCHAR(400) = CONCAT('linea:', @linea_id, ',planta');
                        EXEC dbo.sp_EncolarEvento
                            @tipo_evento = 'ParoIniciadoEvento',
                            @grupos = @grupos_paro,
                            @payload_json = @payload_paro;

                        -- §11.1: fijos permanecen ocupados (ni siquiera entran al
                        -- recorrido); rotativos se liberan con tránsito INDIVIDUAL
                        -- hacia la L8 (00 §C8).
                        DECLARE @personal_id INT;
                        DECLARE cur_rotativos CURSOR LOCAL FAST_FORWARD FOR
                            SELECT a.personal_id
                              FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_id AND p.tipo = 'rotativo' AND a.fin IS NULL;

                        OPEN cur_rotativos;
                        FETCH NEXT FROM cur_rotativos INTO @personal_id;
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            UPDATE Asignacion SET fin = SYSUTCDATETIME(), motivo_fin = 'paro'
                             WHERE personal_id = @personal_id AND fin IS NULL;

                            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
                            VALUES (@personal_id, @linea_id, @linea_bolson, 'paro', @usuario_id);

                            UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;

                            SET @rotativos_liberados += 1;
                            FETCH NEXT FROM cur_rotativos INTO @personal_id;
                        END
                        CLOSE cur_rotativos; DEALLOCATE cur_rotativos;
                    COMMIT;
                END;
                """);

            // ── sp_ReanudarProduccion (E11.1) extendido — encola ParoReanudadoEvento
            // dentro de la misma transacción que cierra el Paro.
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
                        DECLARE @existe BIT = 0, @fin_actual DATETIME2(0), @jornada_linea_id INT;
                        SELECT @existe = 1, @fin_actual = fin, @jornada_linea_id = jornada_linea_id
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

                        DECLARE @linea_id TINYINT = (SELECT linea_id FROM JornadaLinea WHERE Id = @jornada_linea_id);
                        DECLARE @payload_reanudado NVARCHAR(MAX) = JSON_OBJECT('ParoId': @paro_id, 'LineaId': @linea_id);
                        DECLARE @grupos_reanudado NVARCHAR(400) = CONCAT('linea:', @linea_id, ',planta');
                        EXEC dbo.sp_EncolarEvento
                            @tipo_evento = 'ParoReanudadoEvento',
                            @grupos = @grupos_reanudado,
                            @payload_json = @payload_reanudado;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte sp_RegistrarParo/sp_ReanudarProduccion a su forma de
            // E11.1/E11.2 (sin encolar eventos) antes de dropear lo que usan.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RegistrarParo
                    @jornada_linea_id INT, @categoria_id SMALLINT, @causa_id SMALLINT,
                    @descripcion NVARCHAR(500), @usuario_id INT,
                    @paro_id INT OUTPUT,
                    @rotativos_liberados INT = 0 OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @paro_id = NULL; SET @rotativos_liberados = 0;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF LEN(LTRIM(RTRIM(ISNULL(@descripcion, '')))) = 0
                    BEGIN
                        SET @codigo_rechazo = 'DESCRIPCION_OBLIGATORIA';
                        SET @mensaje = 'Debes describir qué observaste antes de confirmar el paro.';
                        RETURN;
                    END

                    IF NOT EXISTS (SELECT 1 FROM CausaParo WHERE Id = @causa_id AND categoria_id = @categoria_id)
                    BEGIN
                        SET @codigo_rechazo = 'CAUSA_NO_PERTENECE_A_LA_CATEGORIA';
                        SET @mensaje = 'Esa causa no pertenece a la categoría seleccionada.';
                        RETURN;
                    END

                    DECLARE @linea_id TINYINT = (SELECT linea_id FROM JornadaLinea WHERE Id = @jornada_linea_id);
                    DECLARE @linea_bolson TINYINT = (SELECT Id FROM Linea WHERE es_bolson = 1);

                    BEGIN TRAN;
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

                        DECLARE @personal_id INT;
                        DECLARE cur_rotativos CURSOR LOCAL FAST_FORWARD FOR
                            SELECT a.personal_id
                              FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_id AND p.tipo = 'rotativo' AND a.fin IS NULL;

                        OPEN cur_rotativos;
                        FETCH NEXT FROM cur_rotativos INTO @personal_id;
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            UPDATE Asignacion SET fin = SYSUTCDATETIME(), motivo_fin = 'paro'
                             WHERE personal_id = @personal_id AND fin IS NULL;

                            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
                            VALUES (@personal_id, @linea_id, @linea_bolson, 'paro', @usuario_id);

                            UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;

                            SET @rotativos_liberados += 1;
                            FETCH NEXT FROM cur_rotativos INTO @personal_id;
                        END
                        CLOSE cur_rotativos; DEALLOCATE cur_rotativos;
                    COMMIT;
                END;
                """);

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

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_EncolarEvento;");

            migrationBuilder.DropTable(
                name: "EventoSaliente");
        }
    }
}

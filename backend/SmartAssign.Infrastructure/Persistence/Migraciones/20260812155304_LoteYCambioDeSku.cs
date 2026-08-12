using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E11.5 (docs/PROGRESO.md): <c>Lote</c> (00 §C5) + cambio de SKU
    /// (§11.2). 04 §4.2 ya traía el esquema completo — tabla nueva de
    /// una sola vez, mismo criterio que <c>Asignacion</c> en E4.1.
    /// Cierra el FK diferido de <c>Paro.lote_id</c> que E11.1/E11.2
    /// dejaron dicho pendiente ("se añade la referencia real en E11.5").
    ///
    /// <c>sp_ArrancarTurno</c> (E5.7) extendido — C5, literal: "Abre
    /// [el lote] cuando la línea empieza a producir ese SKU: al
    /// arrancar el turno, o tras un cambio de SKU". Un <c>Lote</c>
    /// <c>numero=1</c> por cada jornada-línea que arranca, dentro del
    /// mismo cursor que ya recorre las líneas por prioridad — nunca un
    /// segundo recorrido.
    ///
    /// <c>sp_CambiarSKU</c> (nuevo) — C5 + §11.2:
    /// - **Cierra el lote abierto, abre uno nuevo** con el SKU
    ///   entrante y el siguiente <c>numero</c> de la jornada-línea.
    /// - **Pasa la línea por "En limpieza"** (C5 literal, 00 §3.1) —
    ///   ya era un valor válido de <c>CK_Linea_situacion</c> desde E0/E1,
    ///   nunca usado hasta ahora. Vuelve a <c>'activa'</c> al terminar
    ///   — el único valor de "línea operando" que el proyecto usa hasta
    ///   hoy (<c>sp_ConfirmarPlanificacion</c>, E5.2); <c>'en_produccion'</c>
    ///   también es válido en el CHECK pero **ningún procedimiento del
    ///   proyecto lo usa todavía** — extenderlo aquí sería inventar una
    ///   transición que ninguna UT ha establecido nunca (R2); queda como
    ///   hueco conocido, no de esta UT.
    /// - **Puestos que se activan/desactivan** (§11.2, literal): se
    ///   toma una foto de <c>fn_PuestoFueraDeOperacion</c> (E5.3) por
    ///   puesto ANTES del cambio de <c>JornadaLinea.sku_id</c>, y se
    ///   recalcula la misma función DESPUÉS — la función ya deriva en
    ///   vivo de <c>PuestoSKU</c>, nunca se reescribe su lógica aquí.
    ///   El sistema informa los conteos exactos que pide §11.2 ("cuántos
    ///   puestos se activaron y cuántos se desactivaron").
    /// - **Ocupante de un puesto que se desactiva → L8, tránsito
    ///   individual** (§11.2, literal: "si tenían ocupante, esa persona
    ///   va a la L8") — mismo patrón exacto que <c>sp_RegistrarParo</c>
    ///   (E11.2): cierra la <c>Asignacion</c>, inserta un
    ///   <c>Movimiento</c> por persona (nunca en bloque, mismo criterio
    ///   de 00 §C8 aunque esta UT no lo cite en su LEE — es la misma
    ///   arquitectura de tránsito de toda la Parte X). <c>motivo='cambio_sku'</c>
    ///   ya era válido en <c>CK_Mov_motivo</c> desde el original de E8.1.
    /// - **Puestos que se activan** (pasan de fuera de operación a
    ///   libres): no requieren ninguna escritura — <c>fn_PuestoFueraDeOperacion</c>
    ///   ya los computa en vivo desde <c>PuestoSKU</c>/<c>JornadaLinea.sku_id</c>;
    ///   "pasan a libres" es una consecuencia automática del cambio de
    ///   SKU, no una acción separada.
    /// </summary>
    public partial class LoteYCambioDeSku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    jornada_linea_id = table.Column<int>(type: "int", nullable: false),
                    sku_id = table.Column<int>(type: "int", nullable: false),
                    numero = table.Column<short>(type: "smallint", nullable: false),
                    abierto_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    cerrado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    produccion_real = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lote_JornadaLinea_jornada_linea_id",
                        column: x => x.jornada_linea_id,
                        principalTable: "JornadaLinea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lote_SKU_sku_id",
                        column: x => x.sku_id,
                        principalTable: "SKU",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Paro_lote_id",
                table: "Paro",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "IX_Lote_sku_id",
                table: "Lote",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "UX_Lote",
                table: "Lote",
                columns: new[] { "jornada_linea_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Lote_abierto",
                table: "Lote",
                column: "jornada_linea_id",
                unique: true,
                filter: "[cerrado_en] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Paro_Lote_lote_id",
                table: "Paro",
                column: "lote_id",
                principalTable: "Lote",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── sp_ArrancarTurno (E5.7) extendido — abre Lote numero=1
            // por cada jornada-línea, dentro del mismo cursor que ya
            // recorre las líneas por prioridad (C5).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ArrancarTurno
                    @turno_id TINYINT, @dia_operacion DATE, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @codigo_rechazo = NULL;

                    IF NOT EXISTS (
                        SELECT 1 FROM JornadaLinea WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion AND sku_id IS NOT NULL
                    )
                    BEGIN
                        SET @codigo_rechazo = 'SIN_LINEAS_ACTIVAS';
                        RETURN;
                    END

                    IF EXISTS (
                        SELECT 1 FROM JornadaLinea
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                           AND sku_id IS NOT NULL AND estado = 'planificada'
                    )
                    BEGIN
                        SET @codigo_rechazo = 'PLANIFICACION_NO_CONFIRMADA';
                        RETURN;
                    END

                    IF EXISTS (
                        SELECT 1 FROM JornadaLinea
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                           AND sku_id IS NOT NULL AND estado IN ('arrancada', 'cerrada')
                    )
                    BEGIN
                        SET @codigo_rechazo = 'TURNO_YA_ARRANCADO';
                        RETURN;
                    END

                    DECLARE @ahora DATETIME2(0) = SYSUTCDATETIME();

                    UPDATE JornadaLinea SET estado = 'arrancada', arrancado_en = @ahora
                     WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                       AND sku_id IS NOT NULL AND estado = 'confirmada';

                    UPDATE l SET situacion = 'en_arranque'
                      FROM Linea l
                      JOIN JornadaLinea jl ON jl.linea_id = l.Id
                     WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion AND jl.sku_id IS NOT NULL;

                    -- Barrido por prioridad vigente — el único motor que usa A9 (§8.3).
                    DECLARE @jornada_id INT, @sku_id INT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT jl.Id, jl.sku_id
                          FROM JornadaLinea jl
                          JOIN PrioridadLinea pl ON pl.linea_id = jl.linea_id AND pl.vigente_hasta IS NULL
                         WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion AND jl.sku_id IS NOT NULL
                         ORDER BY pl.orden ASC;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @jornada_id, @sku_id;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        EXEC dbo.sp_BarridoPuestosFijos @jornada_linea_id = @jornada_id, @usuario_id = @usuario_id;

                        -- 00 §C5: "Abre [el lote] al arrancar el turno" — numero=1, primero de la jornada.
                        INSERT INTO Lote (jornada_linea_id, sku_id, numero)
                        VALUES (@jornada_id, @sku_id, 1);

                        FETCH NEXT FROM cur_lineas INTO @jornada_id, @sku_id;
                    END
                    CLOSE cur_lineas;
                    DEALLOCATE cur_lineas;

                    -- Ahora sí arranca la ventana (§8.4) — mismo instante
                    -- @ahora para las 10 líneas, tras terminar el barrido.
                    DECLARE @ventana_min INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'ventana_arranque_min');

                    IF @ventana_min IS NOT NULL
                        UPDATE JornadaLinea SET ventana_arranque_fin = DATEADD(MINUTE, @ventana_min, @ahora)
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                           AND sku_id IS NOT NULL AND estado = 'arrancada';
                END;
                """);

            // ── sp_CambiarSKU (00 §C5, §11.2) ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CambiarSKU
                    @jornada_linea_id INT, @sku_nuevo_id INT, @usuario_id INT,
                    @lote_nuevo_id INT OUTPUT,
                    @puestos_activados INT OUTPUT,
                    @puestos_desactivados INT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @lote_nuevo_id = NULL; SET @puestos_activados = 0; SET @puestos_desactivados = 0;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF NOT EXISTS (SELECT 1 FROM Sku WHERE Id = @sku_nuevo_id AND activo = 1)
                    BEGIN
                        SET @codigo_rechazo = 'SKU_INEXISTENTE';
                        SET @mensaje = 'Ese SKU no existe o no está activo.';
                        RETURN;
                    END

                    IF NOT EXISTS (SELECT 1 FROM JornadaLinea WHERE Id = @jornada_linea_id AND cerrado_en IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_NO_ABIERTA';
                        SET @mensaje = 'Esta jornada-línea no está abierta.';
                        RETURN;
                    END

                    DECLARE @linea_id TINYINT = (SELECT linea_id FROM JornadaLinea WHERE Id = @jornada_linea_id);
                    DECLARE @linea_bolson TINYINT = (SELECT Id FROM Linea WHERE es_bolson = 1);

                    BEGIN TRAN;
                        -- Bloqueo determinista sobre la fila padre (JornadaLinea) —
                        -- mismo patrón que sp_RegistrarParo (E11.2).
                        SELECT 1 FROM JornadaLinea WITH (UPDLOCK, HOLDLOCK) WHERE Id = @jornada_linea_id;

                        -- 00 §C5: cierra el lote abierto, si lo hay.
                        UPDATE Lote SET cerrado_en = SYSUTCDATETIME()
                         WHERE jornada_linea_id = @jornada_linea_id AND cerrado_en IS NULL;

                        -- C5, literal: "pasando la línea por En limpieza" (00 §3.1).
                        UPDATE Linea SET situacion = 'en_limpieza' WHERE Id = @linea_id;

                        -- §11.2: foto de "fuera de operación" ANTES del cambio de SKU,
                        -- para poder contar activados/desactivados después.
                        DECLARE @estados TABLE (puesto_id INT PRIMARY KEY, fuera_antes BIT NOT NULL);
                        INSERT INTO @estados (puesto_id, fuera_antes)
                        SELECT p.Id, dbo.fn_PuestoFueraDeOperacion(p.Id)
                          FROM Puesto p WHERE p.linea_id = @linea_id AND p.activo = 1;

                        UPDATE JornadaLinea SET sku_id = @sku_nuevo_id WHERE Id = @jornada_linea_id;

                        SET @puestos_activados = (
                            SELECT COUNT(*) FROM @estados e
                             WHERE e.fuera_antes = 1 AND dbo.fn_PuestoFueraDeOperacion(e.puesto_id) = 0);
                        SET @puestos_desactivados = (
                            SELECT COUNT(*) FROM @estados e
                             WHERE e.fuera_antes = 0 AND dbo.fn_PuestoFueraDeOperacion(e.puesto_id) = 1);

                        -- §11.2, literal: "si tenían ocupante, esa persona va a la L8" —
                        -- tránsito individual, mismo patrón que sp_RegistrarParo (E11.2).
                        DECLARE @personal_id INT;
                        DECLARE cur_desactivados CURSOR LOCAL FAST_FORWARD FOR
                            SELECT a.personal_id
                              FROM @estados e
                              JOIN Asignacion a ON a.puesto_id = e.puesto_id AND a.fin IS NULL
                             WHERE e.fuera_antes = 0 AND dbo.fn_PuestoFueraDeOperacion(e.puesto_id) = 1;

                        OPEN cur_desactivados;
                        FETCH NEXT FROM cur_desactivados INTO @personal_id;
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            UPDATE Asignacion SET fin = SYSUTCDATETIME(), motivo_fin = 'cambio_sku'
                             WHERE personal_id = @personal_id AND fin IS NULL;

                            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
                            VALUES (@personal_id, @linea_id, @linea_bolson, 'cambio_sku', @usuario_id);

                            UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;

                            FETCH NEXT FROM cur_desactivados INTO @personal_id;
                        END
                        CLOSE cur_desactivados; DEALLOCATE cur_desactivados;

                        -- 00 §C5: abre el lote nuevo, siguiente número de la jornada-línea.
                        DECLARE @siguiente_numero SMALLINT = ISNULL(
                            (SELECT MAX(numero) FROM Lote WHERE jornada_linea_id = @jornada_linea_id), 0) + 1;

                        INSERT INTO Lote (jornada_linea_id, sku_id, numero)
                        VALUES (@jornada_linea_id, @sku_nuevo_id, @siguiente_numero);
                        SET @lote_nuevo_id = SCOPE_IDENTITY();

                        -- Termina la limpieza — la línea vuelve a operar.
                        UPDATE Linea SET situacion = 'activa' WHERE Id = @linea_id;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CambiarSKU;");

            // Revierte sp_ArrancarTurno a su forma de E5.7, sin abrir Lote — antes
            // de dropear la tabla, que este cuerpo todavía referencia.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ArrancarTurno
                    @turno_id TINYINT, @dia_operacion DATE, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @codigo_rechazo = NULL;

                    IF NOT EXISTS (
                        SELECT 1 FROM JornadaLinea WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion AND sku_id IS NOT NULL
                    )
                    BEGIN
                        SET @codigo_rechazo = 'SIN_LINEAS_ACTIVAS';
                        RETURN;
                    END

                    IF EXISTS (
                        SELECT 1 FROM JornadaLinea
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                           AND sku_id IS NOT NULL AND estado = 'planificada'
                    )
                    BEGIN
                        SET @codigo_rechazo = 'PLANIFICACION_NO_CONFIRMADA';
                        RETURN;
                    END

                    IF EXISTS (
                        SELECT 1 FROM JornadaLinea
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                           AND sku_id IS NOT NULL AND estado IN ('arrancada', 'cerrada')
                    )
                    BEGIN
                        SET @codigo_rechazo = 'TURNO_YA_ARRANCADO';
                        RETURN;
                    END

                    DECLARE @ahora DATETIME2(0) = SYSUTCDATETIME();

                    UPDATE JornadaLinea SET estado = 'arrancada', arrancado_en = @ahora
                     WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                       AND sku_id IS NOT NULL AND estado = 'confirmada';

                    UPDATE l SET situacion = 'en_arranque'
                      FROM Linea l
                      JOIN JornadaLinea jl ON jl.linea_id = l.Id
                     WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion AND jl.sku_id IS NOT NULL;

                    DECLARE @jornada_id INT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT jl.Id
                          FROM JornadaLinea jl
                          JOIN PrioridadLinea pl ON pl.linea_id = jl.linea_id AND pl.vigente_hasta IS NULL
                         WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion AND jl.sku_id IS NOT NULL
                         ORDER BY pl.orden ASC;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @jornada_id;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        EXEC dbo.sp_BarridoPuestosFijos @jornada_linea_id = @jornada_id, @usuario_id = @usuario_id;
                        FETCH NEXT FROM cur_lineas INTO @jornada_id;
                    END
                    CLOSE cur_lineas;
                    DEALLOCATE cur_lineas;

                    DECLARE @ventana_min INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'ventana_arranque_min');

                    IF @ventana_min IS NOT NULL
                        UPDATE JornadaLinea SET ventana_arranque_fin = DATEADD(MINUTE, @ventana_min, @ahora)
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                           AND sku_id IS NOT NULL AND estado = 'arrancada';
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Paro_Lote_lote_id",
                table: "Paro");

            migrationBuilder.DropTable(
                name: "Lote");

            migrationBuilder.DropIndex(
                name: "IX_Paro_lote_id",
                table: "Paro");
        }
    }
}

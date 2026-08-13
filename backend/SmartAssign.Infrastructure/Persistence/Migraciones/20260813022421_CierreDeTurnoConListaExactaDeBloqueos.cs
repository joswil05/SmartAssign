using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E14.1 (docs/PROGRESO.md): "Cierre de turno con lista exacta de
    /// bloqueos" (00 §C13, 02 §4.10) — arranca E14.
    ///
    /// Sin cambios de esquema: <c>JornadaLinea.CerradoEn</c>/<c>Estado</c>
    /// y <c>UltimaTareaJornada</c> ya existían completos desde E4/E5 con
    /// un comentario propio que citaba esta UT por nombre ("se escribe al
    /// cierre de turno, etapa E14"). <c>SolicitudRelevo.Resultado</c> ya
    /// tenía <c>'cierre_turno'</c> como valor válido en su CHECK desde
    /// E9.1. El esquema entero anticipaba esta UT — solo faltaba el
    /// procedimiento.
    ///
    /// <c>sp_CerrarTurno</c> (nuevo) — alcance de **una sola línea**
    /// (<c>@jornada_linea_id</c>), no de la planta entera: C13 está
    /// escrito enteramente en primera persona de línea ("su línea",
    /// "personas suyas"). "El cierre de planta es acción suya [del
    /// Coordinador]" no es un mecanismo distinto — es que solo el
    /// Coordinador tiene alcance sin restricción (E2) para llamar esto en
    /// varias líneas seguidas; un supervisor solo puede cerrar la suya.
    /// No se inventa aquí un "cerrar toda la planta de una vez" que
    /// ningún documento describe (R2).
    ///
    /// **Los tres bloqueos, en una lista estructurada, nunca un rechazo
    /// genérico** (§1.3, §12.4, literal de C13): lote abierto, tránsito
    /// entrante, tránsito saliente sin recibir — vía <c>FOR JSON PATH</c>
    /// sobre un <c>UNION ALL</c>, disponible desde SQL Server 2016 (no se
    /// arriesga a `JSON_ARRAYAGG`, cuya disponibilidad en esta instancia
    /// no está confirmada). El SP devuelve HECHOS estructurados (ids,
    /// nombres, códigos de línea) — la prosa exacta del diagrama de 02
    /// §4.10 ("Lote 3 sigue abierto", "llama al supervisor de L2") es
    /// composición de presentación, no dato del servidor; ninguna fuente
    /// dice lo contrario para esta UT (a diferencia de
    /// `fn_TextoAvisoFatiga`, donde sí se pide texto compuesto). Sin
    /// pantalla Android que consuma esto todavía — mismo patrón que
    /// media docena de SP de E8-E13 sin UI propia.
    ///
    /// **Al ejecutar** (00 §C13, los cinco puntos, todos en una sola
    /// transacción): toda <c>Asignacion</c> abierta de la jornada —fija o
    /// rotativa, la distinción no importa aquí— cierra con
    /// <c>motivo_fin='cierre_turno'</c> y su ocupante pasa a
    /// <c>fuera_de_turno</c>; eso ES "liberar los puestos fijos", no una
    /// operación aparte. <c>UltimaTareaJornada</c> (00 §B6) se
    /// **actualiza** (upsert manual — la clave primaria real es solo
    /// <c>personal_id</c>, confirmado en `UltimaTareaJornadaConfig`: es
    /// "el último puesto", singular, nunca un historial que crezca sin
    /// límite) para cada persona cuyo puesto SÍ tiene
    /// <c>tipo_actividad_id</c>; `fn_ViolaNoRepeticion24h` (E4) ya filtra
    /// la relevancia por `Puesto.horas_recuperacion` al LEER, así que
    /// escribir sin filtrar aquí es seguro e inocuo para el resto. Los
    /// <c>SolicitudRelevo</c> abiertas de la línea cierran con
    /// <c>resultado='cierre_turno'</c> (valor ya válido desde E9.1). Los
    /// <c>RelevoDescartado</c> de los puestos de la línea, del mismo
    /// <c>dia_operacion</c>, caducan (00 §B10, literal: "caduca
    /// automáticamente al cierre de turno").
    ///
    /// Deliberadamente **fuera** de esta UT (R2): el cierre FORZADO con
    /// justificación (A6) — es literalmente el título de **E14.2**, que
    /// extiende este mismo SP con `CREATE OR ALTER`, mismo patrón que
    /// E10.5 extendió `sp_ExtraccionInversa`/`sp_CubrirVacanteCritica`
    /// con la vía de forzar. `JornadaLinea.CerradoForzadoPor` existe
    /// desde E4 pero esta UT no lo toca — su propio comentario en el
    /// modelo ya decía "llega en E14" (E14.2, no E14.1).
    /// </summary>
    public partial class CierreDeTurnoConListaExactaDeBloqueos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CerrarTurno
                    @jornada_linea_id INT, @usuario_id INT,
                    @bloqueos NVARCHAR(MAX) OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @bloqueos = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @linea_id TINYINT, @estado VARCHAR(20), @dia_operacion DATE;
                    SELECT @linea_id = linea_id, @estado = estado, @dia_operacion = dia_operacion
                      FROM JornadaLinea WHERE Id = @jornada_linea_id;

                    IF @linea_id IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_INEXISTENTE';
                        SET @mensaje = 'Esta jornada no existe.';
                        RETURN;
                    END

                    IF @estado = 'cerrada'
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_YA_CERRADA';
                        SET @mensaje = 'Esta jornada ya está cerrada.';
                        RETURN;
                    END

                    IF @estado <> 'arrancada'
                    BEGIN
                        SET @codigo_rechazo = 'JORNADA_NO_ARRANCADA';
                        SET @mensaje = 'Esta jornada todavía no ha arrancado.';
                        RETURN;
                    END

                    -- 00 §C13 / 02 §4.10: lista EXACTA de bloqueos, nunca un
                    -- rechazo genérico. Estructurada (hechos, no prosa) — la
                    -- composición del texto final es de presentación.
                    SET @bloqueos = (
                        SELECT tipo, loteId, numero, movimientoId, personalId, nombreCompleto,
                               lineaOrigenId, lineaOrigenCodigo, lineaDestinoId, lineaDestinoCodigo
                          FROM (
                            SELECT 'lote_abierto' AS tipo, l.Id AS loteId, l.numero AS numero,
                                   CAST(NULL AS BIGINT) AS movimientoId, CAST(NULL AS INT) AS personalId, CAST(NULL AS NVARCHAR(150)) AS nombreCompleto,
                                   CAST(NULL AS TINYINT) AS lineaOrigenId, CAST(NULL AS NVARCHAR(4)) AS lineaOrigenCodigo,
                                   CAST(NULL AS TINYINT) AS lineaDestinoId, CAST(NULL AS NVARCHAR(4)) AS lineaDestinoCodigo
                              FROM Lote l
                             WHERE l.jornada_linea_id = @jornada_linea_id AND l.cerrado_en IS NULL

                            UNION ALL

                            SELECT 'transito_entrante', NULL, NULL,
                                   m.Id, m.personal_id, per.nombre_completo,
                                   m.linea_origen, lo.codigo, NULL, NULL
                              FROM Movimiento m
                              JOIN Personal per ON per.Id = m.personal_id
                              JOIN Linea lo ON lo.Id = m.linea_origen
                             WHERE m.linea_destino = @linea_id AND m.estado = 'en_transito'

                            UNION ALL

                            SELECT 'transito_saliente_sin_recibir', NULL, NULL,
                                   m.Id, m.personal_id, per.nombre_completo,
                                   NULL, NULL, m.linea_destino, ld.codigo
                              FROM Movimiento m
                              JOIN Personal per ON per.Id = m.personal_id
                              JOIN Linea ld ON ld.Id = m.linea_destino
                             WHERE m.linea_origen = @linea_id AND m.estado = 'en_transito'
                          ) bloqueo
                        FOR JSON PATH
                    );

                    IF @bloqueos IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'CIERRE_BLOQUEADO';
                        SET @mensaje = 'Hay bloqueos pendientes para cerrar el turno.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        -- DATETIME2 completo (no DATETIME2(0)) a propósito:
                        -- @ahora se compara contra Asignacion.inicio, que
                        -- guarda precisión completa (SYSUTCDATETIME(), sin
                        -- escala, en su default de columna). Truncar @ahora
                        -- a segundos enteros puede REDONDEAR HACIA ABAJO por
                        -- debajo de un `inicio` con fracción de segundo más
                        -- alta creado el mismo segundo — CK_Asig_fin
                        -- ("fin >= inicio") entonces falla de verdad, no por
                        -- azar de pruebas: se reprodujo, se aisló y esta es
                        -- la causa raíz confirmada, no una suposición.
                        DECLARE @ahora DATETIME2 = SYSUTCDATETIME();

                        -- Toda Asignacion abierta de la jornada: fija o
                        -- rotativa, ambas cierran igual — "liberar los
                        -- puestos fijos" es consecuencia de esto, no un
                        -- paso aparte.
                        DECLARE @asignacion_id BIGINT, @personal_id INT, @puesto_id INT, @tipo_actividad_id SMALLINT;
                        DECLARE cur_asignaciones CURSOR LOCAL FAST_FORWARD FOR
                            SELECT a.Id, a.personal_id, a.puesto_id, p.tipo_actividad_id
                              FROM Asignacion a
                              JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE a.jornada_linea_id = @jornada_linea_id AND a.fin IS NULL;

                        OPEN cur_asignaciones;
                        FETCH NEXT FROM cur_asignaciones INTO @asignacion_id, @personal_id, @puesto_id, @tipo_actividad_id;
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            UPDATE Asignacion SET fin = @ahora, motivo_fin = 'cierre_turno' WHERE Id = @asignacion_id;
                            UPDATE Personal SET situacion = 'fuera_de_turno' WHERE Id = @personal_id;

                            -- 00 §B6: "el último puesto ocupado por persona"
                            -- — singular, upsert manual (la PK real de
                            -- UltimaTareaJornada es solo personal_id). Sin
                            -- tipo_actividad_id en el puesto, no hay nada
                            -- que la regla de 24 h necesite comparar.
                            IF @tipo_actividad_id IS NOT NULL
                            BEGIN
                                IF EXISTS (SELECT 1 FROM UltimaTareaJornada WHERE personal_id = @personal_id)
                                    UPDATE UltimaTareaJornada
                                       SET tipo_actividad_id = @tipo_actividad_id, puesto_id = @puesto_id,
                                           dia_operacion = @dia_operacion, registrado_en = @ahora
                                     WHERE personal_id = @personal_id;
                                ELSE
                                    INSERT INTO UltimaTareaJornada (personal_id, tipo_actividad_id, puesto_id, dia_operacion, registrado_en)
                                    VALUES (@personal_id, @tipo_actividad_id, @puesto_id, @dia_operacion, @ahora);
                            END

                            FETCH NEXT FROM cur_asignaciones INTO @asignacion_id, @personal_id, @puesto_id, @tipo_actividad_id;
                        END
                        CLOSE cur_asignaciones; DEALLOCATE cur_asignaciones;

                        -- Cancelar relevos pendientes de la línea (valor ya
                        -- válido en CK_SR_resultado desde E9.1).
                        UPDATE SolicitudRelevo
                           SET resultado = 'cierre_turno', resuelta_en = @ahora
                         WHERE jornada_linea_id = @jornada_linea_id AND resultado IS NULL;

                        -- 00 §B10: caducan los descartados de los puestos de
                        -- ESTA línea (el par puesto/persona, no la persona
                        -- en general) para el día de turno que cierra.
                        UPDATE rd
                           SET limpiado_en = @ahora, limpiado_por = @usuario_id
                          FROM RelevoDescartado rd
                          JOIN Puesto p ON p.Id = rd.puesto_id
                         WHERE p.linea_id = @linea_id
                           AND rd.jornada_dia = @dia_operacion
                           AND rd.limpiado_en IS NULL;

                        UPDATE JornadaLinea SET estado = 'cerrada', cerrado_en = @ahora WHERE Id = @jornada_linea_id;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CerrarTurno;");
        }
    }
}

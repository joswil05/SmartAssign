using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E14.2 (docs/PROGRESO.md): "<c>UltimaTareaJornada</c> + cierre
    /// forzado con justificación" (00 §B6, 00 §A6).
    ///
    /// Sin cambios de esquema — <c>JornadaLinea.CerradoForzadoPor</c>
    /// (int? FK a <c>Usuario</c>) ya existía completo desde E4, con un
    /// comentario propio que decía literalmente "llega en E14" (E14.2,
    /// no E14.1: E14.1 dejó dicho explícitamente que el cierre forzado
    /// era justo esta UT). <c>JustificacionExcepcion</c> ya acepta
    /// <c>tipo_excepcion = 'forzar_cierre_turno'</c> desde E4.1
    /// (<c>CK_JE_tipo</c>) y <c>MotivoExcepcion.Id = 3</c>
    /// ("Forzar cierre de turno") ya está sembrado desde E0. El upsert de
    /// <c>UltimaTareaJornada</c> (00 §B6) ya se construyó completo en
    /// E14.1 — corre igual en el camino normal y en el forzado porque es
    /// el mismo cuerpo de cursor, sin bifurcación; esta UT no repite ese
    /// trabajo, solo confirma con pruebas que el camino forzado también
    /// lo ejecuta.
    ///
    /// <c>sp_CerrarTurno</c> (extiende E14.1 con <c>CREATE OR ALTER</c>,
    /// mismo patrón que E10.5 usó con <c>sp_ExtraccionInversa</c>/
    /// <c>sp_CubrirVacanteCritica</c>) gana
    /// <c>@justificacion_motivo_id SMALLINT = NULL,
    /// @justificacion_texto NVARCHAR(600) = NULL</c>. A diferencia de
    /// <c>sp_AsignarPersona</c> (donde una bandera explícita como
    /// <c>@es_liderazgo_manual</c> señala la intención de excepción
    /// independientemente de si llega justificación, y por eso puede
    /// rechazar con <c>JUSTIFICACION_REQUERIDA</c>), aquí no existe una
    /// bandera de "quiero forzar" separada — C13 no la pide. El propio
    /// <c>@bloqueos</c> (lista exacta, ya devuelta desde E14.1) ES la
    /// señal: el Coordinador la ve, decide forzar, y repite la misma
    /// llamada adjuntando justificación. Mismo patrón que
    /// <c>sp_ExtraccionInversa</c>: <c>@forzando</c> = ambos parámetros
    /// presentes a la vez.
    ///
    /// Si hay bloqueos y NO se forzó: rechazo <c>CIERRE_BLOQUEADO</c>
    /// (sin cambios de E14.1). Si hay bloqueos y SÍ se forzó: la
    /// transacción sigue adelante pese a ellos, se inserta una fila en
    /// <c>JustificacionExcepcion</c> (<c>tipo_excepcion =
    /// 'forzar_cierre_turno'</c>, nunca decidido por quien llama) y
    /// <c>JornadaLinea.CerradoForzadoPor = @usuario_id</c>. Si NO hay
    /// bloqueos, el cierre es normal y <c>CerradoForzadoPor</c> queda
    /// NULL — igual que <c>sp_ExtraccionInversa</c> no escribe
    /// justificación cuando el piso no hizo falta forzarlo, adjuntar
    /// justificación "por si acaso" a un cierre que no la necesitaba no
    /// crea una excepción fantasma.
    ///
    /// 00 §A6, segunda regla dura, literal: "Lo que la excepción del
    /// Coordinador NUNCA salta: restricciones médicas y compatibilidad
    /// de categoría" — no aplica aquí: <c>sp_CerrarTurno</c> no evalúa
    /// ninguna de las dos cosas en ningún punto (cierra asignaciones
    /// existentes, no crea ninguna), así que no hay nada que ese límite
    /// deba impedir en este procedimiento. No se inventa una
    /// verificación que ninguna fuente pide aquí (R2).
    /// </summary>
    public partial class CierreForzadoConJustificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CerrarTurno
                    @jornada_linea_id INT, @usuario_id INT,
                    @justificacion_motivo_id SMALLINT = NULL,
                    @justificacion_texto NVARCHAR(600) = NULL,
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

                    -- 00 §A6: sin bandera de "forzar" propia — la lista de
                    -- bloqueos ya devuelta ES la señal. @forzando exige los
                    -- dos parámetros a la vez, mismo patrón que
                    -- sp_ExtraccionInversa/@forzando_piso.
                    DECLARE @forzando BIT = CASE WHEN @justificacion_motivo_id IS NOT NULL AND @justificacion_texto IS NOT NULL THEN 1 ELSE 0 END;

                    IF @bloqueos IS NOT NULL AND @forzando = 0
                    BEGIN
                        SET @codigo_rechazo = 'CIERRE_BLOQUEADO';
                        SET @mensaje = 'Hay bloqueos pendientes para cerrar el turno.';
                        RETURN;
                    END

                    DECLARE @forzo_de_verdad BIT = CASE WHEN @bloqueos IS NOT NULL AND @forzando = 1 THEN 1 ELSE 0 END;

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

                        -- 00 §A6: fila de justificación SOLO cuando de
                        -- verdad se forzó pese a bloqueos reales — igual que
                        -- sp_ExtraccionInversa no escribe una excepción
                        -- cuando el piso no hizo falta forzarlo. tipo_excepcion
                        -- lo decide el procedimiento, nunca quien llama.
                        IF @forzo_de_verdad = 1
                        BEGIN
                            INSERT INTO JustificacionExcepcion (tipo_excepcion, motivo_id, texto, usuario_id)
                            VALUES ('forzar_cierre_turno', @justificacion_motivo_id, @justificacion_texto, @usuario_id);
                        END

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

                        UPDATE JornadaLinea
                           SET estado = 'cerrada', cerrado_en = @ahora,
                               cerrado_forzado_por = CASE WHEN @forzo_de_verdad = 1 THEN @usuario_id ELSE NULL END
                         WHERE Id = @jornada_linea_id;
                    COMMIT;

                    -- El cierre forzado SÍ sucedió pese a los bloqueos —
                    -- @bloqueos ya quedó poblado arriba con la lista exacta
                    -- que se pasó por encima, para que quien llama pueda
                    -- mostrarla igual (auditoría/confirmación), sin
                    -- convertirla en NULL solo porque la operación terminó
                    -- en éxito.
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte a la firma de E14.1 (sin cierre forzado).
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
                        DECLARE @ahora DATETIME2 = SYSUTCDATETIME();

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

                        UPDATE SolicitudRelevo
                           SET resultado = 'cierre_turno', resuelta_en = @ahora
                         WHERE jornada_linea_id = @jornada_linea_id AND resultado IS NULL;

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
    }
}

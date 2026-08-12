using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E10.5 (docs/PROGRESO.md): <c>JustificacionExcepcion</c> en toda
    /// excepción — 00 §A6, 04 §5.4. Regla dura, literal: "Toda excepción
    /// del Coordinador la exige. Sin formulario, la operación no se
    /// ejecuta."
    ///
    /// Sin cambios de esquema — <c>JustificacionExcepcion</c>/
    /// <c>MotivoExcepcion</c> ya existían completas desde E4.1
    /// (<c>MotorDeValidacionEsquema</c>) con las 7 filas de catálogo ya
    /// sembradas desde E0 (<c>SemillaEstructuralYCatalogo</c>), y
    /// <c>Asignacion.justificacion_id</c>/<c>Movimiento.justificacion_id</c>
    /// ya existían como FK nulables desde que esas tablas se crearon.
    /// Hasta esta UT, NINGÚN procedimiento escribía nunca en
    /// <c>JustificacionExcepcion</c> — la tabla existía pero A6 no se
    /// exigía en ningún punto real. Esta UT cierra ese hueco en los
    /// puntos cuyo mecanismo anfitrión YA EXISTE:
    ///
    /// | `tipo_excepcion` | motivo_id | Procedimiento extendido |
    /// |---|---|---|
    /// | `asignacion_liderazgo` (A7b) | 7 | `sp_AsignarPersona` — `@es_liderazgo_manual` ya existía en `sp_ValidarAsignacion` desde E4.6 pero **nunca era alcanzable**: `sp_AsignarPersona` (E6.8) lo llamaba con `0` fijo. Código muerto desde entonces. |
    /// | `saltar_ventana_arranque` (B12) | 5 | `sp_AsignarPersona`/`sp_ValidarAsignacion` — el paso 7 no tenía ninguna vía de excepción; se añade `@permitir_saltar_ventana`, mismo patrón que los bypass ya existentes de los pasos 3 y 5. |
    /// | `movimiento_fuera_de_flujo` (§2.1.9) | 1 | `sp_AsignarPersona`/`sp_DespacharPersona` cuando `@origen`/`@motivo = 'intervencion_coordinador'` — ese valor ya era válido en ambos CHECK desde que esas tablas se crearon (E4.1/E8.1); §2.1.9 no es un mecanismo nuevo, es este mismo camino sin justificar todavía. |
    /// | `forzar_bajo_piso_seguridad` (B5) | 4 | `sp_ExtraccionInversa` (E10.3) y `sp_CubrirVacanteCritica`/N2 (E10.4) — ambos lo dejaron dicho explícitamente como pendiente para esta UT. |
    /// | `extraccion_operador_b` (C15-N3) | 2 | `sp_CubrirVacanteCritica`/N3 (E10.4) — dejado dicho explícitamente como pendiente para esta UT. |
    ///
    /// **Deliberadamente fuera de alcance** (R2 — no construir un
    /// mecanismo por adelantado): `forzar_cierre_turno` (motivo_id 3) no
    /// tiene procedimiento anfitrión — `sp_CerrarTurno` llega en E14.1
    /// (00 §C13), y ese es el UT correcto para exigirle justificación,
    /// igual que aquí se exige justo cuando el mecanismo ya existe.
    /// `cancelar_transito` (motivo_id 6) tampoco tiene procedimiento
    /// anfitrión — ningún UT del plan construye todavía una cancelación
    /// manual de tránsito caducado (`sp_CaducarTransitos`, B11/E8.6,
    /// "marca demorados y alerta, no mueve a nadie" — deliberadamente).
    /// Inventar esos dos procedimientos aquí solo para completar la
    /// lista de siete sería construir negocio que ningún documento pide
    /// todavía en esta etapa.
    ///
    /// **Patrón uniforme:** cada punto extendido recibe
    /// <c>@justificacion_motivo_id SMALLINT = NULL, @justificacion_texto
    /// NVARCHAR(600) = NULL</c>. Si la condición de excepción aplica
    /// (bandera de bypass encendida, u origen/motivo de intervención) y
    /// faltan esos dos parámetros, la operación se RECHAZA con
    /// <c>JUSTIFICACION_REQUERIDA</c> — nunca se ejecuta sin formulario
    /// (A6, regla dura). Si están presentes, el procedimiento inserta la
    /// fila en <c>JustificacionExcepcion</c> con el <c>tipo_excepcion</c>
    /// literal que le corresponde (nunca lo decide quien llama — evita
    /// que un tipo y un motivo_id inconsistentes lleguen a convivir en
    /// la misma fila) y enlaza <c>justificacion_id</c> en
    /// <c>Asignacion</c>/<c>Movimiento</c> según corresponda.
    ///
    /// `CK_JE_texto` (mínimo 10 caracteres no en blanco, ya existente
    /// desde E4.1) es quien impide un formulario vacío — ningún
    /// procedimiento de esta UT repite esa validación.
    ///
    /// **Precedencia cuando más de una condición aplica en la misma
    /// llamada a `sp_AsignarPersona`** (decisión de ingeniería, no de
    /// negocio — ninguna fuente cubre el caso de dos excepciones a la
    /// vez en una sola operación): `@es_liderazgo_manual` gana sobre
    /// `@permitir_saltar_ventana`, que gana sobre
    /// `@origen = 'intervencion_coordinador'` — de más a menos
    /// específico. `Asignacion`/`Movimiento` solo tienen una columna
    /// `justificacion_id`; una sola excepción por operación es
    /// consistente con que 00 §A6 describe cada excepción como un acto
    /// deliberado propio, nunca dos a la vez.
    /// </summary>
    public partial class JustificacionEnExcepciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── sp_ValidarAsignacion — paso 7 (ventana de arranque, §8.4)
            // gana su primera vía de excepción, mismo patrón que los
            // bypass ya existentes de los pasos 3 (@es_liderazgo_manual)
            // y 5 (@permitir_ceder_perfil).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ValidarAsignacion
                    @personal_id INT, @puesto_id INT, @usuario_id INT,
                    @permitir_ceder_perfil BIT = 0,
                    @es_liderazgo_manual   BIT = 0,
                    @permitir_saltar_ventana BIT = 0,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje        NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);
                    SET @codigo_rechazo = NULL;
                    SET @mensaje = NULL;

                    -- 1 · ¿El puesto sigue libre?
                    IF EXISTS (SELECT 1 FROM Asignacion WHERE puesto_id = @puesto_id AND fin IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'PUESTO_OCUPADO';
                        SET @mensaje = 'El puesto acaba de ser ocupado por otra persona.';
                        RETURN;
                    END

                    -- 2 · ¿La persona sigue disponible?
                    IF EXISTS (SELECT 1 FROM Personal WHERE Id = @personal_id
                               AND situacion IN ('asignado','en_transito','retirado_temporal','ausente_justificado'))
                    BEGIN
                        SET @codigo_rechazo = 'PERSONA_NO_DISPONIBLE';
                        SET @mensaje = 'Esta persona no está disponible para ser asignada ahora mismo.';
                        RETURN;
                    END

                    -- 3 · ¿Categoría compatible? (§4.2) — salvo liderazgo manual (A7b)
                    IF @es_liderazgo_manual = 0
                       AND dbo.fn_CategoriaCompatible(@personal_id, @puesto_id) = 0
                    BEGIN
                        SET @codigo_rechazo = 'CATEGORIA_INCOMPATIBLE';
                        SET @mensaje = 'La categoría de esta persona no es compatible con este puesto.';
                        RETURN;
                    END

                    -- 4 · ¿Restricciones médicas? [REGLA DURA] — NUNCA cede (§7.2, B12)
                    IF dbo.fn_TieneRestriccionBloqueante(@personal_id, @puesto_id, @hoy) = 1
                    BEGIN
                        SET @codigo_rechazo = 'RESTRICCION_MEDICA';
                        SET @mensaje = 'Esta persona tiene una restricción médica vigente que este puesto exige. No se puede asignar.';
                        RETURN;
                    END

                    -- 5 · ¿Perfil preferente? [REGLA BLANDA] — única que cede (§7.3, §8.5)
                    IF @permitir_ceder_perfil = 0
                       AND dbo.fn_PerfilIncompatible(@personal_id, @puesto_id) = 1
                    BEGIN
                        SET @codigo_rechazo = 'PERFIL_PREFERENTE';
                        SET @mensaje = 'Esta persona no cumple el sexo preferente de este puesto.';
                        RETURN;
                    END

                    -- 6 · ¿No repitió la tarea en su jornada anterior? (§7.4, A4, A12, B6)
                    IF dbo.fn_ViolaNoRepeticion24h(@personal_id, @puesto_id) = 1
                    BEGIN
                        SET @codigo_rechazo = 'NO_REPETICION_24H';
                        SET @mensaje = 'Esta persona hizo esta misma actividad en su jornada anterior. No puede repetirla todavía.';
                        RETURN;
                    END

                    -- 7 · ¿La ventana de arranque lo permite? (§8.4) — salvo excepción justificada (B12, A6)
                    IF @permitir_saltar_ventana = 0
                       AND dbo.fn_VentanaArranqueBloquea(@personal_id, @puesto_id) = 1
                    BEGIN
                        SET @codigo_rechazo = 'VENTANA_ARRANQUE';
                        SET @mensaje = 'Ventana de arranque activa: esta persona no está físicamente en esta línea.';
                        RETURN;
                    END
                END;
                """);

            // ── sp_AsignarPersona — cierra el hueco de A7b (código muerto
            // desde E6.8: @es_liderazgo_manual llegaba fijo en 0) y añade
            // saltar_ventana_arranque / movimiento_fuera_de_flujo.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_AsignarPersona
                    @personal_id INT, @puesto_id INT, @usuario_id INT,
                    @jornada_linea_id INT, @origen VARCHAR(25),
                    @idempotency_key UNIQUEIDENTIFIER,
                    @ceder_perfil BIT = 0,
                    @es_liderazgo_manual BIT = 0,
                    @permitir_saltar_ventana BIT = 0,
                    @justificacion_motivo_id SMALLINT = NULL,
                    @justificacion_texto NVARCHAR(600) = NULL,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT,
                    @asignacion_id BIGINT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL; SET @asignacion_id = NULL;

                    -- Idempotencia (§12.4: bloqueo contra doble toque) —
                    -- fuera de la transacción de escritura a propósito:
                    -- un reintento nunca debe volver a tomar bloqueos.
                    IF EXISTS (SELECT 1 FROM OperacionIdempotente WHERE clave = @idempotency_key)
                    BEGIN
                        SELECT @codigo_rechazo = codigo_rechazo, @mensaje = mensaje, @asignacion_id = asignacion_id
                          FROM OperacionIdempotente WHERE clave = @idempotency_key;
                        RETURN;
                    END

                    -- 00 §A6, regla dura: sin formulario, la operación no se
                    -- ejecuta. Precedencia (más específico primero) cuando
                    -- más de una condición aplica a la vez: liderazgo >
                    -- saltar ventana > intervención fuera de flujo.
                    DECLARE @tipo_excepcion VARCHAR(35) = NULL;
                    IF @origen = 'intervencion_coordinador' SET @tipo_excepcion = 'movimiento_fuera_de_flujo';
                    IF @permitir_saltar_ventana = 1 SET @tipo_excepcion = 'saltar_ventana_arranque';
                    IF @es_liderazgo_manual = 1 SET @tipo_excepcion = 'asignacion_liderazgo';

                    IF @tipo_excepcion IS NOT NULL AND (@justificacion_motivo_id IS NULL OR @justificacion_texto IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'JUSTIFICACION_REQUERIDA';
                        SET @mensaje = 'Esta operación es una excepción del Coordinador — requiere un formulario de justificación.';
                        RETURN;
                    END

                    DECLARE @rol VARCHAR(15);
                    SELECT @rol = rol FROM Usuario WHERE Id = @usuario_id;

                    DECLARE @linea_id TINYINT;

                    BEGIN TRAN;
                        -- Bloqueo determinista (B1, 04 §7.3): SIEMPRE
                        -- puesto antes que persona — un orden inconsistente
                        -- entre procedimientos produce interbloqueos bajo
                        -- la carga de arranque, el momento de mayor
                        -- concurrencia de la jornada.
                        SELECT @linea_id = linea_id FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id;
                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        EXEC dbo.sp_ValidarAsignacion @personal_id, @puesto_id, @usuario_id,
                             @permitir_ceder_perfil = @ceder_perfil, @es_liderazgo_manual = @es_liderazgo_manual,
                             @permitir_saltar_ventana = @permitir_saltar_ventana,
                             @codigo_rechazo = @codigo_rechazo OUTPUT, @mensaje = @mensaje OUTPUT;

                        IF @codigo_rechazo IS NOT NULL
                        BEGIN
                            -- B1: rechazo NOMINAL cuando alguien más ganó —
                            -- "quién y dónde", nunca un mensaje genérico.
                            IF @codigo_rechazo = 'PUESTO_OCUPADO'
                            BEGIN
                                DECLARE @nombre_ganador NVARCHAR(120), @codigo_linea VARCHAR(5), @nombre_puesto NVARCHAR(120);
                                SELECT TOP (1) @nombre_ganador = per.nombre_completo
                                  FROM Asignacion a JOIN Personal per ON per.Id = a.personal_id
                                 WHERE a.puesto_id = @puesto_id AND a.fin IS NULL
                                 ORDER BY a.Id DESC;
                                SELECT @codigo_linea = l.codigo, @nombre_puesto = p.nombre_puesto
                                  FROM Puesto p JOIN Linea l ON l.Id = p.linea_id
                                 WHERE p.Id = @puesto_id;
                                IF @nombre_ganador IS NOT NULL
                                    SET @mensaje = @nombre_ganador + N' acaba de ser registrado en ' + @codigo_linea + N' · ' + @nombre_puesto + N' por otro supervisor.';
                            END

                            EXEC dbo.sp_RegistrarAuditoria
                                 @usuario_id = @usuario_id, @rol = @rol, @accion = 'ASIGNAR', @entidad = 'Asignacion',
                                 @entidad_id = NULL, @personal_id = @personal_id, @linea_id = @linea_id,
                                 @resultado = 'RECHAZO', @codigo_rechazo = @codigo_rechazo, @device_id = NULL;

                            INSERT INTO OperacionIdempotente (clave, exitosa, codigo_rechazo, mensaje, asignacion_id)
                            VALUES (@idempotency_key, 0, @codigo_rechazo, @mensaje, NULL);

                            COMMIT;
                            RETURN;
                        END

                        DECLARE @justificacion_id BIGINT = NULL;
                        IF @tipo_excepcion IS NOT NULL
                        BEGIN
                            INSERT INTO JustificacionExcepcion (tipo_excepcion, motivo_id, texto, usuario_id)
                            VALUES (@tipo_excepcion, @justificacion_motivo_id, @justificacion_texto, @usuario_id);
                            SET @justificacion_id = SCOPE_IDENTITY();
                        END

                        INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id, origen, asignado_por, cede_perfil, justificacion_id)
                        VALUES (@jornada_linea_id, @puesto_id, @personal_id, @origen, @usuario_id, @ceder_perfil, @justificacion_id);
                        SET @asignacion_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'asignado' WHERE Id = @personal_id;

                        EXEC dbo.sp_RegistrarAuditoria
                             @usuario_id = @usuario_id, @rol = @rol, @accion = 'ASIGNAR', @entidad = 'Asignacion',
                             @entidad_id = @asignacion_id, @personal_id = @personal_id, @linea_id = @linea_id,
                             @resultado = 'OK', @device_id = NULL;

                        INSERT INTO OperacionIdempotente (clave, exitosa, codigo_rechazo, mensaje, asignacion_id)
                        VALUES (@idempotency_key, 1, NULL, NULL, @asignacion_id);
                    COMMIT;
                END;
                """);

            // ── sp_DespacharPersona — movimiento_fuera_de_flujo (§2.1.9)
            // cuando @motivo = 'intervencion_coordinador'. Ese valor ya
            // era válido en CK_Mov_motivo desde E8.1.
            // ⚠ Preserva íntegra la reserva de puesto de E8.5
            // (@puesto_destino_id, PUESTO_DESTINO_INEXISTENTE,
            // PUESTO_YA_RESERVADO) — CREATE OR ALTER reemplaza el cuerpo
            // completo, así que esta extensión tiene que partir de la
            // forma vigente (E8.5), no de la original de E8.1.
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
                    COMMIT;
                END;
                """);

            // ── sp_ExtraccionInversa — forzar_bajo_piso_seguridad (B5),
            // dejado dicho explícitamente pendiente en E10.3. La
            // justificación, si llega, autoriza el recorrido COMPLETO —
            // no solo el último recurso: 00 §B5 no ordena "agotar las
            // líneas con margen antes de forzar", solo que forzar
            // exige justificación cuando de verdad se usa.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ExtraccionInversa
                    @puesto_id_solicitante INT, @usuario_id INT,
                    @justificacion_motivo_id SMALLINT = NULL,
                    @justificacion_texto NVARCHAR(600) = NULL,
                    @candidato_id INT OUTPUT,
                    @linea_origen TINYINT OUTPUT,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @candidato_id = NULL; SET @linea_origen = NULL; SET @movimiento_id = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @linea_solicitante TINYINT = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id_solicitante);
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);
                    DECLARE @forzando_piso BIT = CASE WHEN @justificacion_motivo_id IS NOT NULL AND @justificacion_texto IS NOT NULL THEN 1 ELSE 0 END;

                    -- E10.2: solo con la L8 completamente vacía — mismo chequeo que sp_ProponerRelevista ya hace.
                    DECLARE @sub_candidato INT, @sub_cede BIT, @sub_codigo VARCHAR(40), @sub_mensaje NVARCHAR(400);
                    EXEC dbo.sp_ProponerRelevista @puesto_id_solicitante,
                         @candidato_id = @sub_candidato OUTPUT, @cede_perfil = @sub_cede OUTPUT,
                         @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;

                    IF @sub_candidato IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'L8_TIENE_CANDIDATOS_DISPONIBLES';
                        SET @mensaje = 'Todavía hay personal disponible en el Bolsón — no corresponde extracción inversa.';
                        RETURN;
                    END

                    DECLARE @minimo_default INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'minimo_operarios_default');

                    -- E10.1: orden derivado, línea por línea.
                    DECLARE @linea_donante TINYINT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT linea_id FROM dbo.fn_OrdenExtraccionInversa(@linea_solicitante) ORDER BY orden DESC;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @linea_donante;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        DECLARE @minimo INT = COALESCE(
                            (SELECT minimo_operarios FROM Linea WHERE Id = @linea_donante), @minimo_default);
                        DECLARE @ocupantes INT = (
                            SELECT COUNT(*) FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_donante AND p.tipo = 'rotativo' AND a.fin IS NULL);

                        -- B5: al alcanzar el mínimo, la línea queda inmune —
                        -- salvo justificación (A6) que fuerza por debajo.
                        -- @minimo NULL = sin piso configurado, la regla no aplica todavía (R2).
                        IF @minimo IS NULL OR @ocupantes > @minimo OR @forzando_piso = 1
                        BEGIN
                            SELECT TOP (1) @candidato_id = per.Id
                              FROM Asignacion a
                              JOIN Personal per ON per.Id = a.personal_id
                              JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_donante AND p.tipo = 'rotativo' AND a.fin IS NULL
                               AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id_solicitante) = 1
                               AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id_solicitante, @hoy) = 0
                               AND dbo.fn_ViolaNoRepeticion24h(per.Id, @puesto_id_solicitante) = 0
                             ORDER BY per.Ficha ASC;

                            IF @candidato_id IS NOT NULL
                            BEGIN
                                SET @linea_origen = @linea_donante;
                                DECLARE @forzo_esta_linea BIT = CASE WHEN @minimo IS NOT NULL AND @ocupantes <= @minimo THEN 1 ELSE 0 END;
                                CLOSE cur_lineas; DEALLOCATE cur_lineas;

                                BEGIN TRAN;
                                    -- Puesto antes que persona (04 §7.3), mismo orden que toda la familia.
                                    SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id_solicitante;
                                    SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @candidato_id;

                                    DECLARE @justificacion_id BIGINT = NULL;
                                    IF @forzo_esta_linea = 1
                                    BEGIN
                                        INSERT INTO JustificacionExcepcion (tipo_excepcion, motivo_id, texto, usuario_id)
                                        VALUES ('forzar_bajo_piso_seguridad', @justificacion_motivo_id, @justificacion_texto, @usuario_id);
                                        SET @justificacion_id = SCOPE_IDENTITY();
                                    END

                                    UPDATE Asignacion SET fin = SYSUTCDATETIME()
                                     WHERE personal_id = @candidato_id AND fin IS NULL;

                                    INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por, justificacion_id)
                                    VALUES (@candidato_id, @linea_origen, @linea_solicitante, @puesto_id_solicitante, 'extraccion_inversa', @usuario_id, @justificacion_id);
                                    SET @movimiento_id = SCOPE_IDENTITY();

                                    UPDATE Personal SET situacion = 'en_transito' WHERE Id = @candidato_id;
                                COMMIT;
                                RETURN;
                            END
                        END

                        FETCH NEXT FROM cur_lineas INTO @linea_donante;
                    END
                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                    -- §9.6, literal: nada en todo el recorrido — ninguna línea con margen o candidato.
                    SET @codigo_rechazo = 'CAPACIDAD_CRITICA_DE_PLANTA_AGOTADA';
                    SET @mensaje = 'Capacidad crítica de planta agotada. Requiere intervención humana.';
                END;
                """);

            // ── sp_CubrirVacanteCritica — N3 (extraccion_operador_b, C15
            // + A6) pasa de "detectar" a "ejecutar" cuando llega
            // justificación; la misma justificación cubre también el
            // forzado del piso en N3, si hiciera falta (decisión de
            // ingeniería: exigir dos formularios para una sola operación
            // no lo pide ninguna fuente). N2, si el piso de su propia
            // línea lo bloquea, también puede forzarse con la misma vía
            // (B5 aplica a N2 y N3 por igual, 00 §C15).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CubrirVacanteCritica
                    @puesto_id_vacante INT, @usuario_id INT,
                    @justificacion_motivo_id SMALLINT = NULL,
                    @justificacion_texto NVARCHAR(600) = NULL,
                    @nivel_aplicado VARCHAR(2) OUTPUT,
                    @candidato_id INT OUTPUT,
                    @linea_origen TINYINT OUTPUT,
                    @solicitud_id BIGINT OUTPUT,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @nivel_aplicado = NULL; SET @candidato_id = NULL; SET @linea_origen = NULL;
                    SET @solicitud_id = NULL; SET @movimiento_id = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @tiene_justificacion BIT = CASE WHEN @justificacion_motivo_id IS NOT NULL AND @justificacion_texto IS NOT NULL THEN 1 ELSE 0 END;

                    IF dbo.fn_EsVacanteCritica(@puesto_id_vacante) = 0
                    BEGIN
                        SET @codigo_rechazo = 'PUESTO_NO_ES_VACANTE_CRITICA';
                        SET @mensaje = 'Este puesto no está en vacante crítica.';
                        RETURN;
                    END

                    IF EXISTS (SELECT 1 FROM SolicitudRelevo WHERE puesto_id = @puesto_id_vacante AND resuelta_en IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'YA_TIENE_SOLICITUD_ABIERTA';
                        SET @mensaje = 'Esta vacante crítica ya tiene una cobertura en curso.';
                        RETURN;
                    END

                    DECLARE @linea_afectada TINYINT = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id_vacante);
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);

                    -- N1 · Bolson, sin hueco — reutiliza el motor de relevos tal cual (E9).
                    DECLARE @sub_candidato INT, @sub_cede BIT, @sub_codigo VARCHAR(40), @sub_mensaje NVARCHAR(400);
                    EXEC dbo.sp_ProponerRelevista @puesto_id_vacante,
                         @candidato_id = @sub_candidato OUTPUT, @cede_perfil = @sub_cede OUTPUT,
                         @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;

                    IF @sub_candidato IS NOT NULL
                    BEGIN
                        DECLARE @jornada_n1 INT = (SELECT TOP (1) Id FROM JornadaLinea
                            WHERE linea_id = @linea_afectada AND cerrado_en IS NULL ORDER BY Id DESC);

                        INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel)
                        VALUES (@puesto_id_vacante, @jornada_n1, 'vacante_critica', 'maxima');

                        SET @solicitud_id = SCOPE_IDENTITY();
                        SET @nivel_aplicado = 'N1';
                        SET @candidato_id = @sub_candidato;
                        RETURN;
                    END

                    DECLARE @minimo_default INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'minimo_operarios_default');

                    -- N2 · Operador B en un rotativo de la MISMA línea (piso de B5, forzable con justificación).
                    DECLARE @minimo_propia INT = COALESCE((SELECT minimo_operarios FROM Linea WHERE Id = @linea_afectada), @minimo_default);
                    DECLARE @ocupantes_propia INT = (
                        SELECT COUNT(*) FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                         WHERE p.linea_id = @linea_afectada AND p.tipo = 'rotativo' AND a.fin IS NULL);
                    DECLARE @puesto_donante INT = NULL;
                    DECLARE @n2_forzo_piso BIT = 0;

                    IF @minimo_propia IS NULL OR @ocupantes_propia > @minimo_propia OR @tiene_justificacion = 1
                    BEGIN
                        SELECT TOP (1) @candidato_id = per.Id, @puesto_donante = p.Id
                          FROM Asignacion a
                          JOIN Personal per ON per.Id = a.personal_id
                          JOIN Puesto p ON p.Id = a.puesto_id
                         WHERE p.linea_id = @linea_afectada AND p.tipo = 'rotativo' AND a.fin IS NULL
                           AND per.categoria = 'operador_b'
                           AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id_vacante) = 1
                           AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id_vacante, @hoy) = 0
                           AND dbo.fn_ViolaNoRepeticion24h(per.Id, @puesto_id_vacante) = 0
                         ORDER BY per.Ficha ASC;

                        IF @candidato_id IS NOT NULL AND @minimo_propia IS NOT NULL AND @ocupantes_propia <= @minimo_propia
                            SET @n2_forzo_piso = 1;
                    END

                    IF @candidato_id IS NOT NULL
                    BEGIN
                        SET @linea_origen = @linea_afectada;

                        BEGIN TRAN;
                            -- Puesto antes que persona (04 §7.3), mismo orden que toda la familia.
                            SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id_vacante;
                            SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @candidato_id;

                            DECLARE @justificacion_id_n2 BIGINT = NULL;
                            IF @n2_forzo_piso = 1
                            BEGIN
                                INSERT INTO JustificacionExcepcion (tipo_excepcion, motivo_id, texto, usuario_id)
                                VALUES ('forzar_bajo_piso_seguridad', @justificacion_motivo_id, @justificacion_texto, @usuario_id);
                                SET @justificacion_id_n2 = SCOPE_IDENTITY();
                            END

                            UPDATE Asignacion SET fin = SYSUTCDATETIME()
                             WHERE personal_id = @candidato_id AND fin IS NULL;

                            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por, justificacion_id)
                            VALUES (@candidato_id, @linea_origen, @linea_afectada, @puesto_id_vacante, 'cobertura_vacante_critica', @usuario_id, @justificacion_id_n2);
                            SET @movimiento_id = SCOPE_IDENTITY();

                            UPDATE Personal SET situacion = 'en_transito' WHERE Id = @candidato_id;

                            -- Guarda anti-dominó (C15, literal): prioridad NORMAL, nunca una emergencia nueva.
                            DECLARE @jornada_n2 INT = (SELECT TOP (1) Id FROM JornadaLinea
                                WHERE linea_id = @linea_afectada AND cerrado_en IS NULL ORDER BY Id DESC);

                            INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel)
                            VALUES (@puesto_donante, @jornada_n2, 'manual_supervisor', 'sugerido');
                            SET @solicitud_id = SCOPE_IDENTITY();
                        COMMIT;

                        SET @nivel_aplicado = 'N2';
                        RETURN;
                    END

                    -- N3 · Operador B en un rotativo de OTRA línea, recorrido de proximidad A1 (nunca prioridad, A9).
                    DECLARE @linea_vecina TINYINT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT linea_destino FROM ProximidadLinea WHERE linea_origen = @linea_afectada ORDER BY orden;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @linea_vecina;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM Linea WHERE Id = @linea_vecina AND es_bolson = 1)
                        BEGIN
                            DECLARE @minimo_vecina INT = COALESCE((SELECT minimo_operarios FROM Linea WHERE Id = @linea_vecina), @minimo_default);
                            DECLARE @ocupantes_vecina INT = (
                                SELECT COUNT(*) FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                                 WHERE p.linea_id = @linea_vecina AND p.tipo = 'rotativo' AND a.fin IS NULL);

                            IF @minimo_vecina IS NULL OR @ocupantes_vecina > @minimo_vecina OR @tiene_justificacion = 1
                            BEGIN
                                SELECT TOP (1) @candidato_id = per.Id
                                  FROM Asignacion a
                                  JOIN Personal per ON per.Id = a.personal_id
                                  JOIN Puesto p ON p.Id = a.puesto_id
                                 WHERE p.linea_id = @linea_vecina AND p.tipo = 'rotativo' AND a.fin IS NULL
                                   AND per.categoria = 'operador_b'
                                   AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id_vacante) = 1
                                   AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id_vacante, @hoy) = 0
                                   AND dbo.fn_ViolaNoRepeticion24h(per.Id, @puesto_id_vacante) = 0
                                 ORDER BY per.Ficha ASC;

                                IF @candidato_id IS NOT NULL
                                BEGIN
                                    SET @linea_origen = @linea_vecina;
                                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                                    -- 00 §A6: "extraer un Operador B de otra línea (C15-N3)" exige
                                    -- SIEMPRE justificación del Coordinador.
                                    IF @tiene_justificacion = 0
                                    BEGIN
                                        SET @nivel_aplicado = 'N3';
                                        SET @codigo_rechazo = 'N3_REQUIERE_JUSTIFICACION_COORDINADOR';
                                        SET @mensaje = 'Solo el Coordinador puede extraer un Operador B de otra línea, con justificación.';
                                        RETURN;
                                    END

                                    SET @puesto_donante = NULL;
                                    SELECT @puesto_donante = a.puesto_id FROM Asignacion a
                                     WHERE a.personal_id = @candidato_id AND a.fin IS NULL;

                                    BEGIN TRAN;
                                        SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id_vacante;
                                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @candidato_id;

                                        INSERT INTO JustificacionExcepcion (tipo_excepcion, motivo_id, texto, usuario_id)
                                        VALUES ('extraccion_operador_b', @justificacion_motivo_id, @justificacion_texto, @usuario_id);
                                        DECLARE @justificacion_id_n3 BIGINT = SCOPE_IDENTITY();

                                        UPDATE Asignacion SET fin = SYSUTCDATETIME()
                                         WHERE personal_id = @candidato_id AND fin IS NULL;

                                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por, justificacion_id)
                                        VALUES (@candidato_id, @linea_origen, @linea_afectada, @puesto_id_vacante, 'cobertura_vacante_critica', @usuario_id, @justificacion_id_n3);
                                        SET @movimiento_id = SCOPE_IDENTITY();

                                        UPDATE Personal SET situacion = 'en_transito' WHERE Id = @candidato_id;

                                        -- Guarda anti-dominó (C15, literal): prioridad NORMAL, nunca una emergencia nueva.
                                        DECLARE @jornada_n3 INT = (SELECT TOP (1) Id FROM JornadaLinea
                                            WHERE linea_id = @linea_origen AND cerrado_en IS NULL ORDER BY Id DESC);

                                        INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel)
                                        VALUES (@puesto_donante, @jornada_n3, 'manual_supervisor', 'sugerido');
                                        SET @solicitud_id = SCOPE_IDENTITY();
                                    COMMIT;

                                    SET @nivel_aplicado = 'N3';
                                    RETURN;
                                END
                            END
                        END

                        FETCH NEXT FROM cur_lineas INTO @linea_vecina;
                    END
                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                    -- N4 · nada en todo el recorrido (C15, literal).
                    SET @nivel_aplicado = 'N4';
                    SET @codigo_rechazo = 'VACANTE_CRITICA_PERSISTENTE';
                    SET @mensaje = 'Vacante crítica persistente — no hay ningún Operador B disponible en planta. Alerta al Coordinador.';
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte cada procedimiento a la firma que tenía antes de esta UT.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ValidarAsignacion
                    @personal_id INT, @puesto_id INT, @usuario_id INT,
                    @permitir_ceder_perfil BIT = 0,
                    @es_liderazgo_manual   BIT = 0,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje        NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);
                    SET @codigo_rechazo = NULL;
                    SET @mensaje = NULL;

                    IF EXISTS (SELECT 1 FROM Asignacion WHERE puesto_id = @puesto_id AND fin IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'PUESTO_OCUPADO';
                        SET @mensaje = 'El puesto acaba de ser ocupado por otra persona.';
                        RETURN;
                    END

                    IF EXISTS (SELECT 1 FROM Personal WHERE Id = @personal_id
                               AND situacion IN ('asignado','en_transito','retirado_temporal','ausente_justificado'))
                    BEGIN
                        SET @codigo_rechazo = 'PERSONA_NO_DISPONIBLE';
                        SET @mensaje = 'Esta persona no está disponible para ser asignada ahora mismo.';
                        RETURN;
                    END

                    IF @es_liderazgo_manual = 0
                       AND dbo.fn_CategoriaCompatible(@personal_id, @puesto_id) = 0
                    BEGIN
                        SET @codigo_rechazo = 'CATEGORIA_INCOMPATIBLE';
                        SET @mensaje = 'La categoría de esta persona no es compatible con este puesto.';
                        RETURN;
                    END

                    IF dbo.fn_TieneRestriccionBloqueante(@personal_id, @puesto_id, @hoy) = 1
                    BEGIN
                        SET @codigo_rechazo = 'RESTRICCION_MEDICA';
                        SET @mensaje = 'Esta persona tiene una restricción médica vigente que este puesto exige. No se puede asignar.';
                        RETURN;
                    END

                    IF @permitir_ceder_perfil = 0
                       AND dbo.fn_PerfilIncompatible(@personal_id, @puesto_id) = 1
                    BEGIN
                        SET @codigo_rechazo = 'PERFIL_PREFERENTE';
                        SET @mensaje = 'Esta persona no cumple el sexo preferente de este puesto.';
                        RETURN;
                    END

                    IF dbo.fn_ViolaNoRepeticion24h(@personal_id, @puesto_id) = 1
                    BEGIN
                        SET @codigo_rechazo = 'NO_REPETICION_24H';
                        SET @mensaje = 'Esta persona hizo esta misma actividad en su jornada anterior. No puede repetirla todavía.';
                        RETURN;
                    END

                    IF dbo.fn_VentanaArranqueBloquea(@personal_id, @puesto_id) = 1
                    BEGIN
                        SET @codigo_rechazo = 'VENTANA_ARRANQUE';
                        SET @mensaje = 'Ventana de arranque activa: esta persona no está físicamente en esta línea.';
                        RETURN;
                    END
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_AsignarPersona
                    @personal_id INT, @puesto_id INT, @usuario_id INT,
                    @jornada_linea_id INT, @origen VARCHAR(25),
                    @idempotency_key UNIQUEIDENTIFIER,
                    @ceder_perfil BIT = 0,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT,
                    @asignacion_id BIGINT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL; SET @asignacion_id = NULL;

                    IF EXISTS (SELECT 1 FROM OperacionIdempotente WHERE clave = @idempotency_key)
                    BEGIN
                        SELECT @codigo_rechazo = codigo_rechazo, @mensaje = mensaje, @asignacion_id = asignacion_id
                          FROM OperacionIdempotente WHERE clave = @idempotency_key;
                        RETURN;
                    END

                    DECLARE @rol VARCHAR(15);
                    SELECT @rol = rol FROM Usuario WHERE Id = @usuario_id;

                    DECLARE @linea_id TINYINT;

                    BEGIN TRAN;
                        SELECT @linea_id = linea_id FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id;
                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        EXEC dbo.sp_ValidarAsignacion @personal_id, @puesto_id, @usuario_id,
                             @permitir_ceder_perfil = @ceder_perfil, @es_liderazgo_manual = 0,
                             @codigo_rechazo = @codigo_rechazo OUTPUT, @mensaje = @mensaje OUTPUT;

                        IF @codigo_rechazo IS NOT NULL
                        BEGIN
                            IF @codigo_rechazo = 'PUESTO_OCUPADO'
                            BEGIN
                                DECLARE @nombre_ganador NVARCHAR(120), @codigo_linea VARCHAR(5), @nombre_puesto NVARCHAR(120);
                                SELECT TOP (1) @nombre_ganador = per.nombre_completo
                                  FROM Asignacion a JOIN Personal per ON per.Id = a.personal_id
                                 WHERE a.puesto_id = @puesto_id AND a.fin IS NULL
                                 ORDER BY a.Id DESC;
                                SELECT @codigo_linea = l.codigo, @nombre_puesto = p.nombre_puesto
                                  FROM Puesto p JOIN Linea l ON l.Id = p.linea_id
                                 WHERE p.Id = @puesto_id;
                                IF @nombre_ganador IS NOT NULL
                                    SET @mensaje = @nombre_ganador + N' acaba de ser registrado en ' + @codigo_linea + N' · ' + @nombre_puesto + N' por otro supervisor.';
                            END

                            EXEC dbo.sp_RegistrarAuditoria
                                 @usuario_id = @usuario_id, @rol = @rol, @accion = 'ASIGNAR', @entidad = 'Asignacion',
                                 @entidad_id = NULL, @personal_id = @personal_id, @linea_id = @linea_id,
                                 @resultado = 'RECHAZO', @codigo_rechazo = @codigo_rechazo, @device_id = NULL;

                            INSERT INTO OperacionIdempotente (clave, exitosa, codigo_rechazo, mensaje, asignacion_id)
                            VALUES (@idempotency_key, 0, @codigo_rechazo, @mensaje, NULL);

                            COMMIT;
                            RETURN;
                        END

                        INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id, origen, asignado_por, cede_perfil)
                        VALUES (@jornada_linea_id, @puesto_id, @personal_id, @origen, @usuario_id, @ceder_perfil);
                        SET @asignacion_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'asignado' WHERE Id = @personal_id;

                        EXEC dbo.sp_RegistrarAuditoria
                             @usuario_id = @usuario_id, @rol = @rol, @accion = 'ASIGNAR', @entidad = 'Asignacion',
                             @entidad_id = @asignacion_id, @personal_id = @personal_id, @linea_id = @linea_id,
                             @resultado = 'OK', @device_id = NULL;

                        INSERT INTO OperacionIdempotente (clave, exitosa, codigo_rechazo, mensaje, asignacion_id)
                        VALUES (@idempotency_key, 1, NULL, NULL, @asignacion_id);
                    COMMIT;
                END;
                """);

            // Revierte a la forma de E8.5 (con reserva de puesto), no a la de E8.1.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_DespacharPersona
                    @personal_id INT, @linea_destino TINYINT, @motivo VARCHAR(30), @usuario_id INT,
                    @puesto_destino_id INT = NULL,
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

                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
                        VALUES (@personal_id, @linea_origen, @linea_destino, @puesto_destino_id, @motivo, @usuario_id);
                        SET @movimiento_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;
                    COMMIT;
                END;
                """);

            // Revierte sp_ExtraccionInversa a su forma de E10.3, sin vía de forzado.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ExtraccionInversa
                    @puesto_id_solicitante INT, @usuario_id INT,
                    @candidato_id INT OUTPUT,
                    @linea_origen TINYINT OUTPUT,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @candidato_id = NULL; SET @linea_origen = NULL; SET @movimiento_id = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @linea_solicitante TINYINT = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id_solicitante);
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);

                    DECLARE @sub_candidato INT, @sub_cede BIT, @sub_codigo VARCHAR(40), @sub_mensaje NVARCHAR(400);
                    EXEC dbo.sp_ProponerRelevista @puesto_id_solicitante,
                         @candidato_id = @sub_candidato OUTPUT, @cede_perfil = @sub_cede OUTPUT,
                         @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;

                    IF @sub_candidato IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'L8_TIENE_CANDIDATOS_DISPONIBLES';
                        SET @mensaje = 'Todavía hay personal disponible en el Bolsón — no corresponde extracción inversa.';
                        RETURN;
                    END

                    DECLARE @minimo_default INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'minimo_operarios_default');

                    DECLARE @linea_donante TINYINT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT linea_id FROM dbo.fn_OrdenExtraccionInversa(@linea_solicitante) ORDER BY orden DESC;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @linea_donante;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        DECLARE @minimo INT = COALESCE(
                            (SELECT minimo_operarios FROM Linea WHERE Id = @linea_donante), @minimo_default);
                        DECLARE @ocupantes INT = (
                            SELECT COUNT(*) FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_donante AND p.tipo = 'rotativo' AND a.fin IS NULL);

                        IF @minimo IS NULL OR @ocupantes > @minimo
                        BEGIN
                            SELECT TOP (1) @candidato_id = per.Id
                              FROM Asignacion a
                              JOIN Personal per ON per.Id = a.personal_id
                              JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_donante AND p.tipo = 'rotativo' AND a.fin IS NULL
                               AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id_solicitante) = 1
                               AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id_solicitante, @hoy) = 0
                               AND dbo.fn_ViolaNoRepeticion24h(per.Id, @puesto_id_solicitante) = 0
                             ORDER BY per.Ficha ASC;

                            IF @candidato_id IS NOT NULL
                            BEGIN
                                SET @linea_origen = @linea_donante;
                                CLOSE cur_lineas; DEALLOCATE cur_lineas;

                                BEGIN TRAN;
                                    SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id_solicitante;
                                    SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @candidato_id;

                                    UPDATE Asignacion SET fin = SYSUTCDATETIME()
                                     WHERE personal_id = @candidato_id AND fin IS NULL;

                                    INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
                                    VALUES (@candidato_id, @linea_origen, @linea_solicitante, @puesto_id_solicitante, 'extraccion_inversa', @usuario_id);
                                    SET @movimiento_id = SCOPE_IDENTITY();

                                    UPDATE Personal SET situacion = 'en_transito' WHERE Id = @candidato_id;
                                COMMIT;
                                RETURN;
                            END
                        END

                        FETCH NEXT FROM cur_lineas INTO @linea_donante;
                    END
                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                    SET @codigo_rechazo = 'CAPACIDAD_CRITICA_DE_PLANTA_AGOTADA';
                    SET @mensaje = 'Capacidad crítica de planta agotada. Requiere intervención humana.';
                END;
                """);

            // Revierte sp_CubrirVacanteCritica a su forma de E10.4, sin ejecución de N3.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CubrirVacanteCritica
                    @puesto_id_vacante INT, @usuario_id INT,
                    @nivel_aplicado VARCHAR(2) OUTPUT,
                    @candidato_id INT OUTPUT,
                    @linea_origen TINYINT OUTPUT,
                    @solicitud_id BIGINT OUTPUT,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @nivel_aplicado = NULL; SET @candidato_id = NULL; SET @linea_origen = NULL;
                    SET @solicitud_id = NULL; SET @movimiento_id = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF dbo.fn_EsVacanteCritica(@puesto_id_vacante) = 0
                    BEGIN
                        SET @codigo_rechazo = 'PUESTO_NO_ES_VACANTE_CRITICA';
                        SET @mensaje = 'Este puesto no está en vacante crítica.';
                        RETURN;
                    END

                    IF EXISTS (SELECT 1 FROM SolicitudRelevo WHERE puesto_id = @puesto_id_vacante AND resuelta_en IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'YA_TIENE_SOLICITUD_ABIERTA';
                        SET @mensaje = 'Esta vacante crítica ya tiene una cobertura en curso.';
                        RETURN;
                    END

                    DECLARE @linea_afectada TINYINT = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id_vacante);
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);

                    DECLARE @sub_candidato INT, @sub_cede BIT, @sub_codigo VARCHAR(40), @sub_mensaje NVARCHAR(400);
                    EXEC dbo.sp_ProponerRelevista @puesto_id_vacante,
                         @candidato_id = @sub_candidato OUTPUT, @cede_perfil = @sub_cede OUTPUT,
                         @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;

                    IF @sub_candidato IS NOT NULL
                    BEGIN
                        DECLARE @jornada_n1 INT = (SELECT TOP (1) Id FROM JornadaLinea
                            WHERE linea_id = @linea_afectada AND cerrado_en IS NULL ORDER BY Id DESC);

                        INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel)
                        VALUES (@puesto_id_vacante, @jornada_n1, 'vacante_critica', 'maxima');

                        SET @solicitud_id = SCOPE_IDENTITY();
                        SET @nivel_aplicado = 'N1';
                        SET @candidato_id = @sub_candidato;
                        RETURN;
                    END

                    DECLARE @minimo_default INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'minimo_operarios_default');

                    DECLARE @minimo_propia INT = COALESCE((SELECT minimo_operarios FROM Linea WHERE Id = @linea_afectada), @minimo_default);
                    DECLARE @ocupantes_propia INT = (
                        SELECT COUNT(*) FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                         WHERE p.linea_id = @linea_afectada AND p.tipo = 'rotativo' AND a.fin IS NULL);
                    DECLARE @puesto_donante INT = NULL;

                    IF @minimo_propia IS NULL OR @ocupantes_propia > @minimo_propia
                    BEGIN
                        SELECT TOP (1) @candidato_id = per.Id, @puesto_donante = p.Id
                          FROM Asignacion a
                          JOIN Personal per ON per.Id = a.personal_id
                          JOIN Puesto p ON p.Id = a.puesto_id
                         WHERE p.linea_id = @linea_afectada AND p.tipo = 'rotativo' AND a.fin IS NULL
                           AND per.categoria = 'operador_b'
                           AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id_vacante) = 1
                           AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id_vacante, @hoy) = 0
                           AND dbo.fn_ViolaNoRepeticion24h(per.Id, @puesto_id_vacante) = 0
                         ORDER BY per.Ficha ASC;
                    END

                    IF @candidato_id IS NOT NULL
                    BEGIN
                        SET @linea_origen = @linea_afectada;

                        BEGIN TRAN;
                            SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id_vacante;
                            SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @candidato_id;

                            UPDATE Asignacion SET fin = SYSUTCDATETIME()
                             WHERE personal_id = @candidato_id AND fin IS NULL;

                            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
                            VALUES (@candidato_id, @linea_origen, @linea_afectada, @puesto_id_vacante, 'cobertura_vacante_critica', @usuario_id);
                            SET @movimiento_id = SCOPE_IDENTITY();

                            UPDATE Personal SET situacion = 'en_transito' WHERE Id = @candidato_id;

                            DECLARE @jornada_n2 INT = (SELECT TOP (1) Id FROM JornadaLinea
                                WHERE linea_id = @linea_afectada AND cerrado_en IS NULL ORDER BY Id DESC);

                            INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel)
                            VALUES (@puesto_donante, @jornada_n2, 'manual_supervisor', 'sugerido');
                            SET @solicitud_id = SCOPE_IDENTITY();
                        COMMIT;

                        SET @nivel_aplicado = 'N2';
                        RETURN;
                    END

                    DECLARE @linea_vecina TINYINT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT linea_destino FROM ProximidadLinea WHERE linea_origen = @linea_afectada ORDER BY orden;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @linea_vecina;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM Linea WHERE Id = @linea_vecina AND es_bolson = 1)
                        BEGIN
                            DECLARE @minimo_vecina INT = COALESCE((SELECT minimo_operarios FROM Linea WHERE Id = @linea_vecina), @minimo_default);
                            DECLARE @ocupantes_vecina INT = (
                                SELECT COUNT(*) FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                                 WHERE p.linea_id = @linea_vecina AND p.tipo = 'rotativo' AND a.fin IS NULL);

                            IF @minimo_vecina IS NULL OR @ocupantes_vecina > @minimo_vecina
                            BEGIN
                                SELECT TOP (1) @candidato_id = per.Id
                                  FROM Asignacion a
                                  JOIN Personal per ON per.Id = a.personal_id
                                  JOIN Puesto p ON p.Id = a.puesto_id
                                 WHERE p.linea_id = @linea_vecina AND p.tipo = 'rotativo' AND a.fin IS NULL
                                   AND per.categoria = 'operador_b'
                                   AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id_vacante) = 1
                                   AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id_vacante, @hoy) = 0
                                   AND dbo.fn_ViolaNoRepeticion24h(per.Id, @puesto_id_vacante) = 0
                                 ORDER BY per.Ficha ASC;

                                IF @candidato_id IS NOT NULL
                                BEGIN
                                    SET @linea_origen = @linea_vecina;
                                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                                    SET @nivel_aplicado = 'N3';
                                    SET @codigo_rechazo = 'N3_REQUIERE_JUSTIFICACION_COORDINADOR';
                                    SET @mensaje = 'Solo el Coordinador puede extraer un Operador B de otra línea, con justificación.';
                                    RETURN;
                                END
                            END
                        END

                        FETCH NEXT FROM cur_lineas INTO @linea_vecina;
                    END
                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                    SET @nivel_aplicado = 'N4';
                    SET @codigo_rechazo = 'VACANTE_CRITICA_PERSISTENTE';
                    SET @mensaje = 'Vacante crítica persistente — no hay ningún Operador B disponible en planta. Alerta al Coordinador.';
                END;
                """);
        }
    }
}

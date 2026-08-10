using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E5.2 a E5.7 (docs/PROGRESO.md): el ciclo de vida completo de la
    /// jornada — cambio de prioridad versionado (B8), planificación con
    /// rechazo si falta supervisor (§8.1), el barrido automático de
    /// puestos fijos por prioridad (§8.3, único motor que usa A9), y la
    /// ventana de arranque por jornada-línea (§8.4). También cierra 00
    /// §G5 (categoria_titular): con el mapeo real ya aplicado por el
    /// importador (E5.3), <c>fn_CategoriaCompatible</c> (E4.2, sin tocar)
    /// vuelve a tener candidatos válidos para los puestos fijos reales.
    ///
    /// <c>fn_PuestoFueraDeOperacion</c>/<c>fn_EsVacanteCritica</c>/
    /// <c>fn_SituacionPuesto</c> son deliberadamente NO invocadas desde
    /// <c>sp_ValidarAsignacion</c> (E4.6, ya congelada en PC-2): 05 §7.1
    /// enumera exactamente 7 pasos y "fuera de operación" no es uno de
    /// ellos — es un estado de LECTURA (§5.3), no una regla de validación
    /// de escritura. Quien la respeta es <c>sp_BarridoPuestosFijos</c>,
    /// que la usa como filtro ANTES de intentar asignar.
    ///
    /// Ver docs/PROGRESO.md, notas de ingeniería de la etapa E5.
    /// </summary>
    public partial class PlanificacionYBarrido : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── E5.3 · fn_PuestoFueraDeOperacion (04 §2.5, §5.3) ──
            // Sin jornada abierta para la línea, o con jornada pero sin
            // SKU (línea inactiva, §8.1): fuera de operación. Con jornada
            // y SKU: un puesto SIN ninguna fila en PuestoSKU no depende
            // del SKU (04 §2.5, "PuestoSKU es lo que hace computable
            // fuera de operación") — está disponible siempre que la línea
            // opere. Un puesto CON filas en PuestoSKU solo está disponible
            // para los SKU que declara.
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_PuestoFueraDeOperacion (@puesto_id INT)
                RETURNS BIT AS
                BEGIN
                    DECLARE @linea_id TINYINT;
                    DECLARE @sku_id INT;
                    DECLARE @hay_jornada BIT = 0;

                    SELECT @linea_id = linea_id FROM Puesto WHERE Id = @puesto_id;

                    SELECT @sku_id = sku_id, @hay_jornada = 1
                      FROM JornadaLinea
                     WHERE linea_id = @linea_id AND cerrado_en IS NULL;

                    IF @hay_jornada = 0 OR @sku_id IS NULL
                        RETURN 1;

                    IF NOT EXISTS (SELECT 1 FROM PuestoSKU WHERE puesto_id = @puesto_id)
                        RETURN 0;

                    RETURN CASE WHEN EXISTS (
                        SELECT 1 FROM PuestoSKU WHERE puesto_id = @puesto_id AND sku_id = @sku_id
                    ) THEN 0 ELSE 1 END;
                END;
                """);

            // ── E5.6 · fn_EsVacanteCritica (§5.3, §8.3) ──
            // Recomputa en vivo, no se persiste como bandera: un fijo,
            // activo, no fuera de operación, en una línea ya arrancada
            // (arrancado_en NOT NULL), sin nadie asignado. Solo
            // sp_BarridoPuestosFijos asigna fijos automáticamente (C12) —
            // si sigue sin ocupante después de arrancar, es porque el
            // barrido no encontró titular ni suplente. Recalcularlo así
            // (en vez de guardar un booleano histórico) es más honesto
            // (§12.4): si más tarde alguien libera a un titular, el
            // estado refleja la realidad sin depender de que algo lo
            // actualice.
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_EsVacanteCritica (@puesto_id INT)
                RETURNS BIT AS
                BEGIN
                    DECLARE @tipo VARCHAR(10);
                    DECLARE @activo BIT;
                    DECLARE @linea_id TINYINT;
                    DECLARE @perfil_requerido VARCHAR(30);

                    SELECT @tipo = tipo, @activo = activo, @linea_id = linea_id, @perfil_requerido = perfil_requerido
                      FROM Puesto WHERE Id = @puesto_id;

                    -- Personal de liderazgo nunca se asigna automáticamente
                    -- (§4.1): sp_BarridoPuestosFijos ni siquiera lo intenta,
                    -- así que tampoco es una vacante que el motor "falló en
                    -- resolver" — mismo criterio en ambos lugares.
                    IF @tipo <> 'fijo' OR @activo = 0 OR ISNULL(@perfil_requerido, '') = 'Supervisor'
                        RETURN 0;

                    IF NOT EXISTS (
                        SELECT 1 FROM JornadaLinea
                         WHERE linea_id = @linea_id AND cerrado_en IS NULL AND arrancado_en IS NOT NULL
                    )
                        RETURN 0;

                    IF dbo.fn_PuestoFueraDeOperacion(@puesto_id) = 1
                        RETURN 0;

                    RETURN CASE WHEN EXISTS (
                        SELECT 1 FROM Asignacion WHERE puesto_id = @puesto_id AND fin IS NULL
                    ) THEN 0 ELSE 1 END;
                END;
                """);

            // ── E5.6 · fn_SituacionPuesto — las cuatro categorías de §5.3,
            // en el orden de precedencia que el propio §5.3 exige: "fuera
            // de operación" NUNCA se confunde con "libre" ni "ocupado".
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_SituacionPuesto (@puesto_id INT)
                RETURNS VARCHAR(20) AS
                BEGIN
                    IF dbo.fn_PuestoFueraDeOperacion(@puesto_id) = 1
                        RETURN 'fuera_de_operacion';

                    IF EXISTS (SELECT 1 FROM Asignacion WHERE puesto_id = @puesto_id AND fin IS NULL)
                        RETURN 'ocupado';

                    IF dbo.fn_EsVacanteCritica(@puesto_id) = 1
                        RETURN 'vacante_critica';

                    RETURN 'libre';
                END;
                """);

            // ── E5.2 · sp_CambiarPrioridadLinea (04 §2.2, 00 §B8) ──
            // "Solo hacia adelante": cierra la fila vigente de la línea y
            // abre otra — nunca UPDATE del orden en su sitio. Semántica
            // de INTERCAMBIO: si el orden destino ya lo tiene otra línea,
            // esa línea recibe el orden que la primera deja libre. Es la
            // interpretación más simple y de menor radio de efecto de
            // "cambio de jerarquía en caliente" (B8 no detalla si debe
            // desplazar en cascada a las demás; intercambiar solo toca dos
            // líneas, nunca las otras ocho) — decisión de ingeniería, no
            // de negocio (R2 no aplica). Ambas filas nuevas comparten
            // @ahora y @usuario_id: "quién, cuándo, valor anterior y
            // nuevo" queda auditado en las propias filas cerradas.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CambiarPrioridadLinea
                    @linea_id TINYINT, @orden_nuevo TINYINT, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @codigo_rechazo = NULL;

                    IF @orden_nuevo NOT BETWEEN 1 AND 10
                    BEGIN
                        SET @codigo_rechazo = 'ORDEN_FUERA_DE_RANGO';
                        RETURN;
                    END

                    DECLARE @orden_actual TINYINT =
                        (SELECT orden FROM PrioridadLinea WHERE linea_id = @linea_id AND vigente_hasta IS NULL);

                    IF @orden_actual IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'LINEA_SIN_PRIORIDAD_VIGENTE';
                        RETURN;
                    END

                    IF @orden_actual = @orden_nuevo
                    BEGIN
                        SET @codigo_rechazo = 'SIN_CAMBIO';
                        RETURN;
                    END

                    DECLARE @linea_a_intercambiar TINYINT =
                        (SELECT linea_id FROM PrioridadLinea WHERE orden = @orden_nuevo AND vigente_hasta IS NULL);
                    DECLARE @ahora DATETIME2(0) = SYSUTCDATETIME();

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        -- Cierra ambas filas vigentes ANTES de insertar
                        -- las nuevas: si se insertara antes de cerrar, se
                        -- violaría UX_Prioridad_orden_vigente a mitad de
                        -- transacción (dos filas vigentes con el mismo orden).
                        UPDATE PrioridadLinea SET vigente_hasta = @ahora
                         WHERE linea_id = @linea_id AND vigente_hasta IS NULL;

                        IF @linea_a_intercambiar IS NOT NULL
                            UPDATE PrioridadLinea SET vigente_hasta = @ahora
                             WHERE linea_id = @linea_a_intercambiar AND vigente_hasta IS NULL;

                        INSERT INTO PrioridadLinea (linea_id, orden, vigente_desde, cambiado_por)
                            VALUES (@linea_id, @orden_nuevo, @ahora, @usuario_id);

                        IF @linea_a_intercambiar IS NOT NULL
                            INSERT INTO PrioridadLinea (linea_id, orden, vigente_desde, cambiado_por)
                                VALUES (@linea_a_intercambiar, @orden_actual, @ahora, @usuario_id);

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH
                END;
                """);

            // ── E5.4 · sp_PlanificarLinea (02 §3.1 paso 1-2, §8.1) ──
            // Upsert por (linea_id, turno_id, dia_operacion) — UQ_Jornada
            // lo respalda. @sku_id = NULL marca la línea explícitamente
            // INACTIVA ese turno (04 §4.1: "NULL => línea inactiva").
            // Se puede repetir mientras siga en 'planificada' (§3.1:
            // "se puede reabrir mientras el turno no haya arrancado");
            // después de confirmada, cambiar de plan es una intervención
            // (A6), no esta ruta.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_PlanificarLinea
                    @linea_id TINYINT, @turno_id TINYINT, @dia_operacion DATE,
                    @sku_id INT = NULL, @supervisor_id INT = NULL, @usuario_id INT,
                    @jornada_linea_id INT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @codigo_rechazo = NULL;
                    SET @jornada_linea_id = NULL;

                    DECLARE @id_actual INT, @estado_actual VARCHAR(20);
                    SELECT @id_actual = Id, @estado_actual = estado
                      FROM JornadaLinea
                     WHERE linea_id = @linea_id AND turno_id = @turno_id AND dia_operacion = @dia_operacion;

                    IF @id_actual IS NOT NULL AND @estado_actual <> 'planificada'
                    BEGIN
                        SET @codigo_rechazo = 'PLANIFICACION_YA_CONFIRMADA';
                        RETURN;
                    END

                    IF @id_actual IS NULL
                    BEGIN
                        INSERT INTO JornadaLinea (linea_id, turno_id, dia_operacion, sku_id, supervisor_id, estado)
                            VALUES (@linea_id, @turno_id, @dia_operacion, @sku_id, @supervisor_id, 'planificada');
                        SET @jornada_linea_id = SCOPE_IDENTITY();
                    END
                    ELSE
                    BEGIN
                        UPDATE JornadaLinea SET sku_id = @sku_id, supervisor_id = @supervisor_id
                         WHERE Id = @id_actual;
                        SET @jornada_linea_id = @id_actual;
                    END
                END;
                """);

            // ── E5.4 · sp_ConfirmarPlanificacion (02 §3.1 paso 4, §8.1) ──
            // Rechaza nominalmente si alguna línea ACTIVA (con SKU) de
            // ese turno/día no tiene supervisor — lista los códigos
            // exactos, igual que el ejemplo normativo del flujo
            // ("L4 y L7 están activas sin supervisor asignado"). Al
            // confirmar, sincroniza Linea.supervisor_actual (fuente de
            // verdad en vivo del aislamiento, 04 §6.1, D6) — liberando
            // primero cualquier línea anterior del mismo supervisor
            // (UX_Linea_supervisor: uno no puede tener dos líneas).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ConfirmarPlanificacion
                    @turno_id TINYINT, @dia_operacion DATE, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @lineas_sin_supervisor VARCHAR(200) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @codigo_rechazo = NULL;
                    SET @lineas_sin_supervisor = NULL;

                    IF NOT EXISTS (SELECT 1 FROM JornadaLinea WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion)
                    BEGIN
                        SET @codigo_rechazo = 'SIN_PLANIFICACION';
                        RETURN;
                    END

                    IF EXISTS (
                        SELECT 1 FROM JornadaLinea
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion AND estado <> 'planificada'
                    )
                    BEGIN
                        SET @codigo_rechazo = 'PLANIFICACION_YA_CONFIRMADA';
                        RETURN;
                    END

                    SELECT @lineas_sin_supervisor = STRING_AGG(l.codigo, ', ') WITHIN GROUP (ORDER BY l.codigo)
                      FROM JornadaLinea jl
                      JOIN Linea l ON l.Id = jl.linea_id
                     WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion
                       AND jl.sku_id IS NOT NULL AND jl.supervisor_id IS NULL;

                    IF @lineas_sin_supervisor IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'FALTA_SUPERVISOR';
                        RETURN;
                    END

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        UPDATE JornadaLinea SET estado = 'confirmada'
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion;

                        -- Libera a cada supervisor de cualquier línea
                        -- distinta a la que se le confirma en este lote
                        -- (evita violar UX_Linea_supervisor al moverlo).
                        UPDATE l SET supervisor_actual = NULL
                          FROM Linea l
                         WHERE EXISTS (
                            SELECT 1 FROM JornadaLinea jl
                             WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion
                               AND jl.supervisor_id = l.supervisor_actual AND jl.linea_id <> l.Id
                         );

                        UPDATE l SET supervisor_actual = jl.supervisor_id, situacion = 'activa', activa_hoy = 1
                          FROM Linea l
                          JOIN JornadaLinea jl ON jl.linea_id = l.Id
                         WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion AND jl.sku_id IS NOT NULL;

                        UPDATE l SET supervisor_actual = NULL, situacion = 'inactiva', activa_hoy = 0
                          FROM Linea l
                          JOIN JornadaLinea jl ON jl.linea_id = l.Id
                         WHERE jl.turno_id = @turno_id AND jl.dia_operacion = @dia_operacion AND jl.sku_id IS NULL;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH
                END;
                """);

            // ── E5.5 · sp_BarridoPuestosFijos (§8.3) — el motor de
            // asignación inicial, UNA jornada-línea a la vez. Recorre
            // solo puestos fijos, activos, no fuera de operación, sin
            // ocupante — y excluye explícitamente los puestos cuyo
            // PerfilRequerido literal es 'Supervisor': personal de
            // liderazgo NUNCA se asigna automáticamente (§4.1), así que
            // ese puesto ni siquiera entra en la búsqueda (no cuenta como
            // vacante crítica tampoco, fn_EsVacanteCritica ya filtra por
            // tipo/activo/ocupación, no por PerfilRequerido — este SP es
            // quien decide no tocarlo).
            //
            // Candidato: primero el titular si está presente_sin_asignar;
            // si no pasa sp_ValidarAsignacion (ausente O bloqueado por
            // una regla dura — p. ej. médica, B12 nunca cede ni aquí) se
            // busca un Operador B presente_sin_asignar EN TODA LA PLANTA,
            // no solo en esta línea (§8.3: "los Operadores B disponibles
            // son un recurso escaso... una línea de baja prioridad podría
            // consumir al ÚLTIMO Operador B disponible y dejar sin cubrir
            // un puesto crítico de L4" — la escasez es de planta, no de
            // línea; si se buscara solo entre quienes ya están físicamente
            // en esta línea, nunca habría contención real entre líneas y
            // el orden de A9 no cambiaría nada). Es la asignación INICIAL
            // del día, antes de que exista ningún tránsito que registrar
            // (§6.1, Parte X llega en E8) — candidato determinista
            // (MIN(Id)) porque ninguna decisión fija un criterio de
            // desempate distinto para el barrido inicial (a diferencia
            // del ranking del motor de relevos, 00 §B2).
            //
            // Cada asignación exitosa marca a la persona 'asignado' de
            // inmediato — sin esto, el mismo Operador B podría "aparecer"
            // otra vez como disponible para el siguiente puesto (de esta
            // línea o de la próxima en el recorrido de sp_ArrancarTurno) y
            // reventar UX_Asig_personal_activo con una excepción cruda en
            // vez de simplemente no ser encontrado.
            //
            // Si ninguno pasa: el puesto queda SIN Asignacion — vacante
            // crítica, derivada en vivo por fn_EsVacanteCritica (E5.6),
            // nunca escrita aquí como bandera.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_BarridoPuestosFijos
                    @jornada_linea_id INT, @usuario_id INT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    DECLARE @linea_id TINYINT = (SELECT linea_id FROM JornadaLinea WHERE Id = @jornada_linea_id);
                    DECLARE @puesto_id INT, @titular_id INT;

                    DECLARE cur_puestos CURSOR LOCAL FAST_FORWARD FOR
                        SELECT p.Id, p.titular_id
                          FROM Puesto p
                         WHERE p.linea_id = @linea_id AND p.tipo = 'fijo' AND p.activo = 1
                           AND ISNULL(p.perfil_requerido, '') <> 'Supervisor'
                           AND dbo.fn_PuestoFueraDeOperacion(p.Id) = 0
                           AND NOT EXISTS (SELECT 1 FROM Asignacion a WHERE a.puesto_id = p.Id AND a.fin IS NULL);

                    OPEN cur_puestos;
                    FETCH NEXT FROM cur_puestos INTO @puesto_id, @titular_id;

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        DECLARE @candidato_id INT = NULL;
                        DECLARE @es_suplente BIT = 0;
                        DECLARE @codigo VARCHAR(40), @mensaje NVARCHAR(400);

                        IF @titular_id IS NOT NULL AND EXISTS (
                            SELECT 1 FROM Personal WHERE Id = @titular_id AND situacion = 'presente_sin_asignar'
                        )
                        BEGIN
                            EXEC dbo.sp_ValidarAsignacion
                                @personal_id = @titular_id, @puesto_id = @puesto_id, @usuario_id = @usuario_id,
                                @codigo_rechazo = @codigo OUTPUT, @mensaje = @mensaje OUTPUT;
                            IF @codigo IS NULL SET @candidato_id = @titular_id;
                        END

                        IF @candidato_id IS NULL
                        BEGIN
                            DECLARE @suplente_id INT = (
                                SELECT MIN(per.Id) FROM Personal per
                                 WHERE per.categoria = 'operador_b'
                                   AND per.situacion = 'presente_sin_asignar'
                            );

                            IF @suplente_id IS NOT NULL
                            BEGIN
                                EXEC dbo.sp_ValidarAsignacion
                                    @personal_id = @suplente_id, @puesto_id = @puesto_id, @usuario_id = @usuario_id,
                                    @codigo_rechazo = @codigo OUTPUT, @mensaje = @mensaje OUTPUT;
                                IF @codigo IS NULL
                                BEGIN
                                    SET @candidato_id = @suplente_id;
                                    SET @es_suplente = 1;
                                END
                            END
                        END

                        IF @candidato_id IS NOT NULL
                        BEGIN
                            INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id, titular_original_id, origen, asignado_por)
                                VALUES (@jornada_linea_id, @puesto_id, @candidato_id,
                                        CASE WHEN @es_suplente = 1 THEN @titular_id ELSE NULL END,
                                        'barrido_automatico', @usuario_id);

                            UPDATE Personal SET situacion = 'asignado' WHERE Id = @candidato_id;
                        END

                        FETCH NEXT FROM cur_puestos INTO @puesto_id, @titular_id;
                    END

                    CLOSE cur_puestos;
                    DEALLOCATE cur_puestos;
                END;
                """);

            // ── E5.7 · sp_ArrancarTurno (§8.3, §8.4) — el orquestador de
            // "[Arrancar turno]" del flujo 02 §3.2. Secuencia EXACTA del
            // diagrama: primero el barrido de fijos, DESPUÉS arranca la
            // ventana — no al revés. Por eso ventana_arranque_fin se deja
            // NULL mientras corre el barrido: fn_VentanaArranqueBloquea
            // (E4.5) ya trata NULL como "la regla no aplica todavía", así
            // que ningún candidato del barrido queda bloqueado por una
            // ventana que conceptualmente ni ha empezado. Sin parámetro
            // 'ventana_arranque_min' configurado (04 §9, sigue "a
            // definir"), la ventana no se activa — mismo criterio que
            // E4.5, nunca un valor por defecto inventado (R2).
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

                    -- Ahora sí arranca la ventana (§8.4) — mismo instante
                    -- @ahora para las 10 líneas, tras terminar el barrido.
                    DECLARE @ventana_min INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'ventana_arranque_min');

                    IF @ventana_min IS NOT NULL
                        UPDATE JornadaLinea SET ventana_arranque_fin = DATEADD(MINUTE, @ventana_min, @ahora)
                         WHERE turno_id = @turno_id AND dia_operacion = @dia_operacion
                           AND sku_id IS NOT NULL AND estado = 'arrancada';
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ArrancarTurno;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_BarridoPuestosFijos;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ConfirmarPlanificacion;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_PlanificarLinea;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CambiarPrioridadLinea;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_SituacionPuesto;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_EsVacanteCritica;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_PuestoFueraDeOperacion;");
        }
    }
}

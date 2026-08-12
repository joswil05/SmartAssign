using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E10.4 (docs/PROGRESO.md): <c>sp_CubrirVacanteCritica</c> — 00
    /// §C15, la escalera N1→N4 para una vacante crítica de puesto FIJO
    /// detectada a mitad de turno (el barrido de §8.3 ya corrió y no
    /// vuelve a correr). Regla del cliente, literal: "Si hay déficit de
    /// Operador A, el que le sigue es el Operador B. Sin importar dónde
    /// esté, debe ser asignado al puesto — y se debe ejecutar la
    /// rotación, porque dejará un puesto vacío."
    ///
    /// Sin cambios de esquema: <c>CK_SR_origen</c> ya admitía
    /// <c>'vacante_critica'</c> y <c>CK_SR_nivel</c> ya admitía
    /// <c>'maxima'</c> desde E9.1, y <c>CK_Mov_motivo</c> ya admitía
    /// <c>'cobertura_vacante_critica'</c> desde E8.1 — las tres,
    /// anticipadas exactamente para esta UT porque 00 §C15 ya existía
    /// (cerrada por el cliente) cuando esas tablas se escribieron. Único
    /// caso en el proyecto donde un UT entero no toca DDL.
    ///
    /// **Nivel 1 — Bolsón, sin hueco.** Reutiliza el motor de relevos ya
    /// construido (E9): si <c>sp_ProponerRelevista</c> encuentra un
    /// candidato para el puesto vacante (nada le impide operar sobre un
    /// puesto fijo — no asume rotativo en ningún punto), se abre una
    /// <c>SolicitudRelevo</c> con <c>nivel='maxima'</c> — la "excepción de
    /// máxima prioridad" que 00 §B3 ya reserva literalmente para esto:
    /// "una solicitud generada por vacante crítica de puesto fijo
    /// (C15-N1) encabeza la cola por delante de cualquier fatiga". Este
    /// procedimiento NO despacha — la tabla de C15 dice "Quién ejecuta:
    /// Supervisor de L8, por flujo de relevo estándar", es decir
    /// <c>sp_AceptarRelevo</c> (ya construido), tocado por un humano.
    ///
    /// **Nivel 2 — rotativo de la MISMA línea.** Sin flujo previo que
    /// reutilizar (a diferencia de N1): el candidato está <c>asignado</c>
    /// de verdad, no "disponible", así que — mismo motivo que
    /// <c>sp_ExtraccionInversa</c> (E10.3) — no puede pasar por
    /// <c>sp_DespacharPersona</c>. Cierra su <c>Asignacion</c> vigente e
    /// inserta el <c>Movimiento</c> directamente (motivo
    /// <c>'cobertura_vacante_critica'</c>, <c>linea_origen = linea_destino</c>
    /// porque es la misma línea — nada en el esquema lo prohíbe: el
    /// único CHECK de líneas distintas vive en <c>ProximidadLinea</c>,
    /// no en <c>Movimiento</c>). Se decide llevarlo por Parte X completa
    /// (con tránsito y recepción, aunque el trayecto sea corto) en vez
    /// de un swap instantáneo de <c>Asignacion</c>: mantiene una única
    /// forma de mover gente en todo el proyecto y es la única lectura
    /// que explica por qué <c>'cobertura_vacante_critica'</c> es un
    /// <c>Movimiento.motivo</c> y no, por ejemplo, un nuevo
    /// <c>Asignacion.origen</c>.
    ///
    /// **Piso de seguridad (B5) aplica a N2 y N3, literal de C15** —
    /// mismo cálculo que E10.3: cuenta ocupantes de rotativos activos de
    /// la línea candidata contra <c>Linea.minimo_operarios</c> ??
    /// <c>Parametro['minimo_operarios_default']</c>; sin ninguno de los
    /// dos configurado, nunca inmune (R2).
    ///
    /// **Guarda anti-dominó (C15, literal):** "El puesto rotativo que
    /// queda vacío en N2 y N3 entra a la cola de relevos pendientes a
    /// prioridad normal, no como una emergencia nueva." Se modela como
    /// una <c>SolicitudRelevo</c> nueva sobre el puesto que el candidato
    /// deja vacío, <c>nivel='sugerido'</c> (CK_SR_nivel no admite
    /// "normal" — mismo piso que usa <c>sp_MarcarRelevoSolicitado</c>,
    /// E9.1) y <c>origen='manual_supervisor'</c> (ninguno de los otros
    /// dos valores encaja: no es umbral automático de fatiga, y
    /// <c>'vacante_critica'</c> queda reservado para la solicitud N1
    /// sobre el puesto FIJO original, no para su efecto dominó). Sin
    /// esta guarda la cadena no se detendría en un nivel — exactamente
    /// el efecto que C15 dice impedir.
    ///
    /// **Candidato N2/N3:** ocupante de un rotativo activo, categoría
    /// <c>'operador_b'</c> explícita (la propia regla del cliente lo
    /// nombra: "el que le sigue es el Operador B", no un operario
    /// cualquiera — mismo criterio que el camino de suplente de
    /// <c>sp_BarridoPuestosFijos</c>, E5.5) más <c>fn_CategoriaCompatible</c>
    /// (defensa en profundidad, coherente con el resto del proyecto),
    /// sin restricción médica bloqueante ni violación de no-repetición-
    /// 24h contra el puesto vacante. Sin perfil preferente: igual que
    /// <c>sp_ExtraccionInversa</c>, B12 dice que nunca bloquea y esto es
    /// una emergencia. Ficha ascendente como desempate — mismo criterio
    /// que toda la familia B2/B5.
    ///
    /// **Nivel 3 — rotativo de OTRA línea, vía A1.** Recorre
    /// <c>ProximidadLinea</c> desde la línea afectada (00 §A1, la fila
    /// propia — NUNCA <c>fn_OrdenExtraccionInversa</c>/prioridad: A9 es
    /// tajante en que el motor de relevos y sus parientes de proximidad
    /// nunca usan la jerarquía de prioridad, y C15-N3 es "extracción de
    /// Operador B" por proximidad, no una extracción inversa), saltando
    /// la L8 (no aplica como línea donante — sus candidatos ya se
    /// agotaron en N1).
    ///
    /// **N3 se detecta pero deliberadamente NO se ejecuta todavía.** 00
    /// §A6 es explícito y sin condición de piso: "extraer un Operador B
    /// de otra línea (C15-N3)" está en la lista de excepciones que
    /// SIEMPRE exigen <c>JustificacionExcepcion</c> — a diferencia de
    /// <c>sp_ExtraccionInversa</c> (§9.6/A5), que A6 no menciona y por
    /// eso E10.3 pudo ejecutarla directamente. Como
    /// <c>JustificacionExcepcion</c> todavía no existe (llega en E10.5,
    /// mismo <c>CREATE OR ALTER</c> incremental que el resto de la
    /// sesión), este procedimiento devuelve el candidato y la línea de
    /// origen que aplicarían — información suficiente para que el
    /// Coordinador la vea — con <c>codigo_rechazo =
    /// 'N3_REQUIERE_JUSTIFICACION_COORDINADOR'</c>, sin tocar
    /// <c>Asignacion</c> ni <c>Movimiento</c>.
    ///
    /// **Nivel 4 — nada en todo el recorrido:** "Vacante crítica
    /// persistente + alerta al Coordinador" (C15, literal) — ningún
    /// mensaje citado en la fuente para copiar palabra por palabra
    /// (a diferencia de §9.6 en E10.3), así que el texto compone las
    /// dos frases literales de la propia tabla de C15.
    /// </summary>
    public partial class CoberturaVacanteCritica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

                    -- N1 · Bolsón, sin hueco — reutiliza el motor de relevos tal cual (E9).
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

                    -- N2 · Operador B en un rotativo de la MISMA línea (piso de B5).
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
                            -- Puesto antes que persona (04 §7.3), mismo orden que toda la familia.
                            SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id_vacante;
                            SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @candidato_id;

                            UPDATE Asignacion SET fin = SYSUTCDATETIME()
                             WHERE personal_id = @candidato_id AND fin IS NULL;

                            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
                            VALUES (@candidato_id, @linea_origen, @linea_afectada, @puesto_id_vacante, 'cobertura_vacante_critica', @usuario_id);
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

                                    -- 00 §A6: "extraer un Operador B de otra línea (C15-N3)" exige
                                    -- SIEMPRE justificación del Coordinador — JustificacionExcepcion
                                    -- llega en E10.5. Se detecta y se informa; no se ejecuta todavía.
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
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CubrirVacanteCritica;");
        }
    }
}

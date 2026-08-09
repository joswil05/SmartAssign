using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E4.1 a E4.7 (docs/PROGRESO.md): las cinco funciones de la
    /// especificación (§7.1), <c>sp_ValidarAsignacion</c> con los siete
    /// pasos en el orden exacto, el resto del DENY de 04 §7.5 sobre las
    /// tablas que ya existen (<c>Asignacion</c>, <c>Auditoria</c>) —
    /// <c>Movimiento</c> queda para cuando esa tabla se cree (etapa E8) —
    /// y la segunda mitad de la RLS de 04 §6.3 sobre <c>JornadaLinea</c>,
    /// tal como la migración <c>RlsAlcanceLinea</c> (E2) ya anticipaba.
    ///
    /// <c>fn_ViolaNoRepeticion24h</c> combina dos decisiones cerradas que
    /// nunca se habían puesto una junto a la otra: A12 (el tiempo de
    /// recuperación es un valor en HORAS, propio de cada puesto — ya no
    /// una bandera fija de 24h en un único tipo de actividad) y B6 (la
    /// referencia temporal es la ÚLTIMA JORNADA TRABAJADA, nunca el
    /// calendario). La síntesis es literal, no inventada: el ancla es
    /// <c>UltimaTareaJornada.registrado_en</c> — que por diseño (00 §C13)
    /// solo avanza al cerrar un turno realmente trabajado — y el umbral
    /// es <c>Puesto.horas_recuperacion</c>, el dato real del cliente.
    /// Ver docs/PROGRESO.md, notas de ingeniería de la etapa E4.
    /// </summary>
    public partial class MotorDeValidacion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── E4.1 · fn_TieneRestriccionBloqueante (§7.2, C14) ──
            // ⚠ CLÁUSULA DE NO INVENCIÓN ACTIVA (06_ROADMAP.md P3.1): la
            // verificación es general, sobre TODAS las restricciones
            // vigentes — nunca limitada a un tipo concreto de esfuerzo.
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_TieneRestriccionBloqueante
                    (@personal_id INT, @puesto_id INT, @fecha DATE)
                RETURNS BIT AS
                BEGIN
                    RETURN CASE WHEN EXISTS (
                        SELECT 1
                          FROM RestriccionMedica rm
                          JOIN PuestoCapacidad  pc ON pc.capacidad_id = rm.capacidad_id
                         WHERE rm.personal_id = @personal_id
                           AND pc.puesto_id   = @puesto_id
                           AND rm.fecha_inicio <= @fecha
                           AND (rm.fecha_fin IS NULL OR rm.fecha_fin >= @fecha)
                    ) THEN 1 ELSE 0 END;
                END;
                """);

            // ── E4.2 · fn_CategoriaCompatible — matriz §4.2, casilla por
            // casilla. Nada fuera de esta tabla es compatible (⚠ no
            // inventar casillas, 06_ROADMAP.md P3.2). Un puesto fijo sin
            // categoria_titular (00 §G5 — dato real aún sin categorizar)
            // cae en el ELSE: nadie es compatible hasta que se sepa qué
            // exige, nunca "cualquiera".
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_CategoriaCompatible
                    (@personal_id INT, @puesto_id INT)
                RETURNS BIT AS
                BEGIN
                    DECLARE @categoria_persona VARCHAR(20);
                    DECLARE @tipo_puesto       VARCHAR(10);
                    DECLARE @categoria_titular VARCHAR(15);

                    SELECT @categoria_persona = categoria FROM Personal WHERE Id = @personal_id;
                    SELECT @tipo_puesto = tipo, @categoria_titular = categoria_titular
                      FROM Puesto WHERE Id = @puesto_id;

                    RETURN CASE
                        WHEN @tipo_puesto = 'rotativo' THEN
                            CASE WHEN @categoria_persona IN ('operario','operador_b') THEN 1 ELSE 0 END
                        WHEN @tipo_puesto = 'fijo' AND @categoria_titular = 'operador_a' THEN
                            CASE WHEN @categoria_persona IN ('operador_a','operador_b') THEN 1 ELSE 0 END
                        WHEN @tipo_puesto = 'fijo' AND @categoria_titular = 'averiero' THEN
                            CASE WHEN @categoria_persona IN ('averiero','operador_b') THEN 1 ELSE 0 END
                        WHEN @tipo_puesto = 'fijo' AND @categoria_titular = 'operador_c' THEN
                            CASE WHEN @categoria_persona IN ('operador_c','operador_b','operador_a') THEN 1 ELSE 0 END
                        ELSE 0
                    END;
                END;
                """);

            // ── E4.3 · fn_PerfilIncompatible — regla BLANDA (§7.3, B12).
            // Si el puesto no declara preferencia (NULL o "Indistinto"),
            // o si el dato de la persona no está registrado, la regla NO
            // se aplica — nunca se infiere. LOWER() porque Personal.sexo
            // se guarda en minúsculas y Puesto.sexo_preferente capitalizado
            // (dos importadores distintos, mismo significado — 00 §A13).
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_PerfilIncompatible
                    (@personal_id INT, @puesto_id INT)
                RETURNS BIT AS
                BEGIN
                    DECLARE @sexo_persona     VARCHAR(12);
                    DECLARE @sexo_preferente  VARCHAR(12);

                    SELECT @sexo_persona = sexo FROM Personal WHERE Id = @personal_id;
                    SELECT @sexo_preferente = sexo_preferente FROM Puesto WHERE Id = @puesto_id;

                    RETURN CASE
                        WHEN @sexo_preferente IS NULL OR LOWER(@sexo_preferente) = 'indistinto' THEN 0
                        WHEN @sexo_persona IS NULL THEN 0
                        WHEN LOWER(@sexo_persona) <> LOWER(@sexo_preferente) THEN 1
                        ELSE 0
                    END;
                END;
                """);

            // ── E4.4 · fn_ViolaNoRepeticion24h — generalizada (A12+B6,
            // ver comentario de la clase). Un puesto sin tipo_actividad_id
            // o sin horas_recuperacion no tiene esta regla (A4/A14: no se
            // inventa un umbral que el dato real no trae).
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_ViolaNoRepeticion24h
                    (@personal_id INT, @puesto_id INT)
                RETURNS BIT AS
                BEGIN
                    DECLARE @tipo_actividad_id  SMALLINT;
                    DECLARE @horas_recuperacion SMALLINT;

                    SELECT @tipo_actividad_id = tipo_actividad_id, @horas_recuperacion = horas_recuperacion
                      FROM Puesto WHERE Id = @puesto_id;

                    IF @tipo_actividad_id IS NULL OR @horas_recuperacion IS NULL
                        RETURN 0;

                    RETURN CASE WHEN EXISTS (
                        SELECT 1 FROM UltimaTareaJornada utj
                         WHERE utj.personal_id = @personal_id
                           AND utj.tipo_actividad_id = @tipo_actividad_id
                           AND DATEADD(HOUR, @horas_recuperacion, utj.registrado_en) > SYSUTCDATETIME()
                    ) THEN 1 ELSE 0 END;
                END;
                """);

            // ── E4.5 · fn_VentanaArranqueBloquea (§8.4). La ventana la
            // evalúa el SERVIDOR, nunca el dispositivo. Sin jornada
            // abierta para la línea, o sin ventana calculada todavía
            // (arrancado_en/ventana_arranque_fin nulos — el barrido que
            // los fija se construye en E5.5), la regla no aplica: no es
            // un "bloquea por defecto", es "todavía no hay nada que
            // evaluar".
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_VentanaArranqueBloquea
                    (@personal_id INT, @puesto_id INT)
                RETURNS BIT AS
                BEGIN
                    DECLARE @linea_id      TINYINT;
                    DECLARE @linea_fisica  TINYINT;
                    DECLARE @ventana_fin   DATETIME2(0);

                    SELECT @linea_id = linea_id FROM Puesto WHERE Id = @puesto_id;
                    SELECT @linea_fisica = linea_fisica_actual FROM Personal WHERE Id = @personal_id;

                    SELECT @ventana_fin = ventana_arranque_fin
                      FROM JornadaLinea
                     WHERE linea_id = @linea_id AND cerrado_en IS NULL;

                    IF @ventana_fin IS NULL OR @ventana_fin <= SYSUTCDATETIME()
                        RETURN 0;

                    RETURN CASE WHEN @linea_fisica IS NULL OR @linea_fisica <> @linea_id
                                THEN 1 ELSE 0 END;
                END;
                """);

            // ── E4.6 · sp_ValidarAsignacion — los 7 pasos de §7.1, en
            // ESTE orden exacto. El primer rechazo detiene. Ningún
            // parámetro salta el paso 4 (restricción médica): @mensaje
            // aquí es informativo y genérico — la micro-copia literal de
            // 02 §5.4 con nombre/línea/minutos interpolados es una
            // responsabilidad de la capa de aplicación (se aplica cuando
            // exista UI que la consuma, etapa E6), no de este SP.
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

                    -- 7 · ¿La ventana de arranque lo permite? (§8.4)
                    IF dbo.fn_VentanaArranqueBloquea(@personal_id, @puesto_id) = 1
                    BEGIN
                        SET @codigo_rechazo = 'VENTANA_ARRANQUE';
                        SET @mensaje = 'Ventana de arranque activa: esta persona no está físicamente en esta línea.';
                        RETURN;
                    END
                END;
                """);

            // ── E4.7 · DENY sobre tablas críticas (04 §7.5). Asignacion y
            // Auditoria ya existen; Movimiento se añade cuando esa tabla
            // se cree (etapa E8) — DELETE de RestriccionMedica ya estaba
            // denegado desde E3.4.
            migrationBuilder.Sql("DENY INSERT, UPDATE, DELETE ON dbo.Asignacion TO rol_app;");
            migrationBuilder.Sql("DENY DELETE, UPDATE ON dbo.Auditoria TO rol_app;");
            migrationBuilder.Sql("GRANT EXECUTE ON SCHEMA::dbo TO rol_app;");

            // ── Capa 3 del aislamiento (04 §6.3), segunda mitad: la
            // migración RlsAlcanceLinea (E2) ya dejó dicho que el
            // predicado sobre JornadaLinea se añadiría aquí, "en la
            // migración que cree esa tabla" — es esta.
            migrationBuilder.Sql("""
                ALTER SECURITY POLICY dbo.PoliticaAlcanceLinea
                    ADD FILTER PREDICATE dbo.fn_AlcanceLinea(linea_id) ON dbo.JornadaLinea;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER SECURITY POLICY dbo.PoliticaAlcanceLinea
                    DROP FILTER PREDICATE ON dbo.JornadaLinea;
                """);

            migrationBuilder.Sql("REVOKE EXECUTE ON SCHEMA::dbo TO rol_app;");
            migrationBuilder.Sql("REVOKE DELETE, UPDATE ON dbo.Auditoria TO rol_app;");
            migrationBuilder.Sql("REVOKE INSERT, UPDATE, DELETE ON dbo.Asignacion TO rol_app;");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ValidarAsignacion;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_VentanaArranqueBloquea;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_ViolaNoRepeticion24h;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_PerfilIncompatible;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_CategoriaCompatible;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_TieneRestriccionBloqueante;");
        }
    }
}

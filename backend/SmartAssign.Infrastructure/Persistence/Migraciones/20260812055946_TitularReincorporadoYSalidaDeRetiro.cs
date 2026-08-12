using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E10.6 (docs/PROGRESO.md), cierra E10 (6/6): dos flujos de
    /// salida que §12.5/§9.7 mencionaban pero nadie cerraba. Sin cambios
    /// de esquema: <c>Puesto.titular_id</c>, <c>Asignacion.titular_original_id</c>/
    /// <c>motivo_fin</c> y el propio valor <c>'retirado_temporal'</c> de
    /// <c>CK_Personal_situacion</c> ya existían completos.
    ///
    /// <c>sp_ReincorporarTitular</c> (00 §C1) — literal §12.5: "Titular
    /// reincorporado — suplente liberado". Resuelve el puesto fijo desde
    /// <c>Puesto.titular_id = @titular_id</c> (C12: la asignación técnica
    /// que ya identifica quién es el titular real). Si lo ocupa alguien
    /// más (el suplente — <c>sp_BarridoPuestosFijos</c>, E5.5, es el
    /// único que puede haberlo puesto ahí), cierra esa <c>Asignacion</c>
    /// y abre una nueva para el titular. Restricción médica sigue
    /// aplicando — nunca cede (§7.2) — por si el titular vuelve con una
    /// restricción nueva desde que se fue. **El "puede declinar"** de
    /// C1 punto 3 no tiene contraparte en la base: declinar es
    /// simplemente NO llamar a este procedimiento — no hay nada que
    /// escribir para "no hacer nada".
    ///
    /// **El Operador B liberado no va a la L8** (C1, literal, por A7):
    /// esta UT reutiliza <c>sp_SugerirDestinoRelevado</c> (E9.7) tal
    /// cual, **standalone** — misma limitación que ya dejó dicho esa UT
    /// ("no orquesta la integración con la recepción... esa integración
    /// no tiene UT propia todavía en el plan"). Aquí tampoco: se
    /// devuelve la sugerencia (línea/puesto), no se ejecuta un
    /// despacho — construir esa orquestación sería inventar el UT que
    /// todavía no existe en el plan para esa integración.
    ///
    /// Sin llamada a <c>sp_RegistrarAuditoria</c>: a diferencia de C2 (que
    /// sí dice explícitamente "queda auditado"), C1 no lo menciona —
    /// no se añade ceremonia que la fuente no pide (R2).
    ///
    /// <c>sp_FinalizarRetiroTemporal</c> (00 §C2) — "Solo lo reincorpora
    /// el Coordinador" (mismo patrón de chequeo de rol que
    /// <c>sp_LimpiarDescartado</c>, E9.6, con <c>SESSION_CONTEXT</c> no
    /// aplicable aquí porque <c>Personal</c> no tiene RLS). Pasa a
    /// <c>presente_sin_asignar</c> **en su línea física** — no la toca,
    /// solo cambia la situación; el supervisor la registra normalmente
    /// desde ahí (C2, literal: "la persona pasa a presente, sin
    /// asignar en su línea física; el supervisor la registra
    /// normalmente" — este procedimiento hace la primera mitad, la
    /// segunda ya existe: <c>sp_AsignarPersona</c>). Con
    /// <c>sp_RegistrarAuditoria</c> — "queda auditado (§12.7)" es
    /// literal aquí, a diferencia de C1.
    ///
    /// **Deliberadamente fuera de alcance:** el camino de ENTRADA a
    /// <c>retirado_temporal</c> (quién lo marca, con qué motivo) no
    /// tiene UT propia en el plan — ni siquiera el LEE de esta UT lo
    /// pide (00 §C2, no §9.7 completo). Ningún procedimiento del
    /// proyecto pone a nadie en ese estado todavía; inventar esa
    /// entrada aquí sería construir negocio no pedido (R2).
    /// </summary>
    public partial class TitularReincorporadoYSalidaDeRetiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ReincorporarTitular
                    @titular_id INT, @usuario_id INT,
                    @puesto_id INT OUTPUT,
                    @suplente_liberado_id INT OUTPUT,
                    @asignacion_id BIGINT OUTPUT,
                    @linea_sugerida_suplente TINYINT OUTPUT,
                    @puesto_sugerido_suplente INT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @puesto_id = NULL; SET @suplente_liberado_id = NULL; SET @asignacion_id = NULL;
                    SET @linea_sugerida_suplente = NULL; SET @puesto_sugerido_suplente = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);

                    SELECT @puesto_id = Id FROM Puesto WHERE titular_id = @titular_id AND tipo = 'fijo';
                    IF @puesto_id IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'NO_ES_TITULAR_DE_NINGUN_PUESTO_FIJO';
                        SET @mensaje = 'Esta persona no es titular de ningún puesto fijo.';
                        RETURN;
                    END

                    DECLARE @linea_id TINYINT = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id);

                    IF dbo.fn_TieneRestriccionBloqueante(@titular_id, @puesto_id, @hoy) = 1
                    BEGIN
                        SET @codigo_rechazo = 'RESTRICCION_MEDICA';
                        SET @mensaje = 'Esta persona tiene una restricción médica vigente que este puesto exige. No se puede reincorporar.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        -- Puesto antes que persona (04 §7.3), mismo orden que toda la familia.
                        SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id;

                        SELECT @suplente_liberado_id = personal_id FROM Asignacion
                         WHERE puesto_id = @puesto_id AND fin IS NULL;

                        IF @suplente_liberado_id IS NULL
                        BEGIN
                            SET @codigo_rechazo = 'PUESTO_SIN_OCUPANTE';
                            SET @mensaje = 'Este puesto no tiene a nadie ocupándolo — no hay suplente que liberar.';
                            COMMIT;
                            RETURN;
                        END

                        IF @suplente_liberado_id = @titular_id
                        BEGIN
                            SET @codigo_rechazo = 'TITULAR_YA_EN_SU_PUESTO';
                            SET @mensaje = 'El titular ya está ocupando su propio puesto.';
                            SET @suplente_liberado_id = NULL;
                            COMMIT;
                            RETURN;
                        END

                        -- Bloqueo determinista de las dos personas por Id
                        -- ascendente — nunca "titular luego suplente" fijo,
                        -- para no interbloquear con otra reincorporación
                        -- concurrente que tome los mismos dos en el orden
                        -- contrario.
                        IF @titular_id < @suplente_liberado_id
                        BEGIN
                            SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @titular_id;
                            SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @suplente_liberado_id;
                        END
                        ELSE
                        BEGIN
                            SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @suplente_liberado_id;
                            SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @titular_id;
                        END

                        UPDATE Asignacion SET fin = SYSUTCDATETIME(), motivo_fin = 'titular_reincorporado'
                         WHERE puesto_id = @puesto_id AND fin IS NULL;

                        INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id, origen, asignado_por)
                        SELECT jl.Id, @puesto_id, @titular_id, 'manual_supervisor', @usuario_id
                          FROM JornadaLinea jl WHERE jl.linea_id = @linea_id AND jl.cerrado_en IS NULL;
                        SET @asignacion_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'asignado' WHERE Id = @titular_id;
                        -- Liberado, no a la L8 (A7) — queda disponible en su
                        -- propia línea mientras se decide su próximo puesto.
                        UPDATE Personal SET situacion = 'presente_sin_asignar' WHERE Id = @suplente_liberado_id;
                    COMMIT;

                    -- §9.4 paso 6 / 00 §B4: misma línea (mayor exceso) →
                    -- proximidad A1 → L8. Standalone, igual que E9.7: solo
                    -- sugiere, no despacha.
                    EXEC dbo.sp_SugerirDestinoRelevado
                         @personal_id = @suplente_liberado_id, @linea_actual = @linea_id,
                         @puesto_id_sugerido = @puesto_sugerido_suplente OUTPUT,
                         @linea_sugerida = @linea_sugerida_suplente OUTPUT,
                         @codigo_rechazo = @codigo_rechazo OUTPUT, @mensaje = @mensaje OUTPUT;

                    -- La sugerencia de destino nunca es un rechazo de la
                    -- reincorporación en sí — ya ocurrió y se confirmó.
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_FinalizarRetiroTemporal
                    @personal_id INT, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    -- C2, literal: "Solo lo reincorpora el Coordinador."
                    DECLARE @rol VARCHAR(15) = (SELECT rol FROM Usuario WHERE Id = @usuario_id);
                    IF @rol <> 'coordinador'
                    BEGIN
                        SET @codigo_rechazo = 'SOLO_COORDINADOR';
                        SET @mensaje = 'Solo el Coordinador puede finalizar un retiro temporal.';
                        RETURN;
                    END

                    DECLARE @linea_fisica TINYINT;

                    BEGIN TRAN;
                        DECLARE @situacion VARCHAR(25);
                        SELECT @situacion = situacion, @linea_fisica = linea_fisica_actual
                          FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        IF @situacion IS NULL
                        BEGIN
                            SET @codigo_rechazo = 'PERSONA_INEXISTENTE';
                            SET @mensaje = 'Esta persona no existe.';
                            COMMIT;
                            RETURN;
                        END

                        IF @situacion <> 'retirado_temporal'
                        BEGIN
                            SET @codigo_rechazo = 'NO_ESTA_RETIRADO_TEMPORALMENTE';
                            SET @mensaje = 'Esta persona no está en retiro temporal ahora mismo.';
                            COMMIT;
                            RETURN;
                        END

                        -- C2, literal: "pasa a presente, sin asignar en su
                        -- línea física" — linea_fisica_actual no se toca.
                        UPDATE Personal SET situacion = 'presente_sin_asignar' WHERE Id = @personal_id;

                        -- C2, literal: "Queda auditado (§12.7)".
                        EXEC dbo.sp_RegistrarAuditoria
                             @usuario_id = @usuario_id, @rol = @rol, @accion = 'FINALIZAR_RETIRO_TEMPORAL', @entidad = 'Personal',
                             @entidad_id = @personal_id, @personal_id = @personal_id, @linea_id = @linea_fisica,
                             @resultado = 'OK', @device_id = NULL;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ReincorporarTitular;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_FinalizarRetiroTemporal;");
        }
    }
}

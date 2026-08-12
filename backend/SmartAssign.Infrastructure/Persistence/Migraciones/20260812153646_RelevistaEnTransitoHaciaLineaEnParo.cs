using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E11.4 (docs/PROGRESO.md): 00 §C9 — "Paro con un relevista en
    /// tránsito hacia esa línea". Sin cambios de esquema.
    ///
    /// C9 tiene cuatro afirmaciones; dos ya eran ciertas antes de esta
    /// UT y se verifican aquí sin código nuevo (mismo criterio que las
    /// UTs "solo verificación" de E8.2/E9.9/E10.2):
    ///
    /// 1. **"El tránsito no se cancela: es inmune (§6.1)".** Ya
    ///    garantizado desde E8.2 — nada en <c>sp_RegistrarParo</c>
    ///    (E11.2) toca <c>Movimiento</c>, solo cierra <c>Asignacion</c>
    ///    del rotativo y despacha al ocupante a la L8. El Movimiento del
    ///    relevista que ya estaba en camino sigue <c>en_transito</c>
    ///    exactamente igual.
    /// 4. **"La reserva del puesto fatigado se libera cuando el paro
    ///    vacía los rotativos".** También consecuencia natural, sin
    ///    código nuevo: cuando el relevista llegue (inmune, sigue en
    ///    camino), el puesto que iba a cubrir ya no tiene ocupante que
    ///    relevar — <c>sp_RegistrarParo</c> ya lo dejó vacío. La fila de
    ///    <c>Movimiento</c> con su <c>puesto_destino_id</c> sigue
    ///    existiendo (es el registro del tránsito, §12.7), pero deja de
    ///    representar una relación real con alguien que cubrir — nunca
    ///    se "limpia" el campo, sería reescribir un registro de
    ///    auditoría.
    ///
    /// Las otras dos sí requieren código — se extiende
    /// <c>sp_RecibirPersona</c> (E8.3) con <c>CREATE OR ALTER</c>,
    /// ambos parámetros nuevos con default para no romper a quien lo
    /// llama sin pedirlos (E9.6/sp_AceptarRelevo, RechazoDeRecepcion,
    /// CaducidadDeTransito):
    ///
    /// 2. **"Al llegar, el supervisor destino ve el estado explícito"**
    ///    — <c>@destino_en_paro</c>/<c>@aviso_linea_en_paro</c> se
    ///    calculan DESPUÉS de cerrar el tránsito (recepción real
    ///    primero, aviso informativo después — nunca al revés), mismo
    ///    criterio compositor de texto servidor que
    ///    <c>fn_TextoAvisoFatiga</c> (E9.3, D2): el mensaje literal de
    ///    C9 con el código de línea interpolado. Genérico a cualquier
    ///    recepción (no solo <c>motivo='relevo'</c>) — nada en C9 limita
    ///    el aviso a un origen concreto, y filtrar por motivo sería una
    ///    condición que la fuente no pide (R2).
    /// 3. **"Única acción disponible: despacharla a la L8. Si el paro
    ///    ya terminó, se asigna normalmente."** Deliberadamente NO se
    ///    modela como un bloqueo nuevo en <c>sp_ValidarAsignacion</c>:
    ///    05 §7.1 enumera EXACTAMENTE 7 pasos y esa función quedó
    ///    congelada desde PC-2 (E4.6) — añadir un octavo paso violaría
    ///    ese punto de control. <c>sp_RecibirPersona</c> nunca asigna
    ///    ningún puesto de todas formas (E8.3, "solo entonces se le
    ///    asigna el puesto" es un paso posterior y separado), así que no
    ///    hay nada que bloquear aquí — es guía de interfaz (qué botones
    ///    mostrar), no una regla de escritura que viva en la base.
    /// </summary>
    public partial class RelevistaEnTransitoHaciaLineaEnParo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RecibirPersona
                    @movimiento_id BIGINT, @usuario_id INT,
                    @destino_en_paro BIT = 0 OUTPUT,
                    @aviso_linea_en_paro NVARCHAR(200) = NULL OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @destino_en_paro = 0; SET @aviso_linea_en_paro = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @personal_id INT;
                    DECLARE @linea_destino TINYINT;
                    DECLARE @estado VARCHAR(20);

                    BEGIN TRAN;
                        -- UPDLOCK: dos toques del mismo "CONFIRMAR" (red
                        -- lenta, reintento) no pueden recibir la misma
                        -- fila dos veces — mismo criterio de bloqueo
                        -- determinista que sp_DespacharPersona (E8.1).
                        SELECT @personal_id = personal_id, @linea_destino = linea_destino, @estado = estado
                          FROM Movimiento WITH (UPDLOCK, HOLDLOCK)
                         WHERE Id = @movimiento_id;

                        IF @personal_id IS NULL
                        BEGIN
                            SET @codigo_rechazo = 'MOVIMIENTO_INEXISTENTE';
                            SET @mensaje = 'Este tránsito no existe.';
                            COMMIT;
                            RETURN;
                        END

                        IF @estado <> 'en_transito'
                        BEGIN
                            SET @codigo_rechazo = 'MOVIMIENTO_NO_EN_TRANSITO';
                            SET @mensaje = 'Este movimiento ya fue resuelto.';
                            COMMIT;
                            RETURN;
                        END

                        DECLARE @es_bolson BIT = (SELECT es_bolson FROM Linea WHERE Id = @linea_destino);

                        UPDATE Movimiento
                           SET estado = 'recibido', hora_llegada = SYSUTCDATETIME(), recibido_por = @usuario_id
                         WHERE Id = @movimiento_id;

                        UPDATE Personal
                           SET situacion = CASE WHEN @es_bolson = 1 THEN 'en_bolson' ELSE 'presente_sin_asignar' END,
                               linea_fisica_actual = @linea_destino
                         WHERE Id = @personal_id;

                        -- 00 §C9: el aviso se calcula DESPUÉS de recibir de
                        -- verdad — la recepción real nunca espera a esto.
                        DECLARE @jornada_destino INT = (SELECT TOP (1) Id FROM JornadaLinea
                            WHERE linea_id = @linea_destino AND cerrado_en IS NULL ORDER BY Id DESC);

                        IF @jornada_destino IS NOT NULL AND EXISTS (
                            SELECT 1 FROM Paro WHERE jornada_linea_id = @jornada_destino AND fin IS NULL
                        )
                        BEGIN
                            SET @destino_en_paro = 1;
                            DECLARE @codigo_linea VARCHAR(5) = (SELECT codigo FROM Linea WHERE Id = @linea_destino);
                            SET @aviso_linea_en_paro = @codigo_linea + N' está en paro — el puesto que venía a cubrir fue liberado.';
                        END
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte sp_RecibirPersona a su forma de E8.3, sin aviso de paro.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RecibirPersona
                    @movimiento_id BIGINT, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @personal_id INT;
                    DECLARE @linea_destino TINYINT;
                    DECLARE @estado VARCHAR(20);

                    BEGIN TRAN;
                        SELECT @personal_id = personal_id, @linea_destino = linea_destino, @estado = estado
                          FROM Movimiento WITH (UPDLOCK, HOLDLOCK)
                         WHERE Id = @movimiento_id;

                        IF @personal_id IS NULL
                        BEGIN
                            SET @codigo_rechazo = 'MOVIMIENTO_INEXISTENTE';
                            SET @mensaje = 'Este tránsito no existe.';
                            COMMIT;
                            RETURN;
                        END

                        IF @estado <> 'en_transito'
                        BEGIN
                            SET @codigo_rechazo = 'MOVIMIENTO_NO_EN_TRANSITO';
                            SET @mensaje = 'Este movimiento ya fue resuelto.';
                            COMMIT;
                            RETURN;
                        END

                        DECLARE @es_bolson BIT = (SELECT es_bolson FROM Linea WHERE Id = @linea_destino);

                        UPDATE Movimiento
                           SET estado = 'recibido', hora_llegada = SYSUTCDATETIME(), recibido_por = @usuario_id
                         WHERE Id = @movimiento_id;

                        UPDATE Personal
                           SET situacion = CASE WHEN @es_bolson = 1 THEN 'en_bolson' ELSE 'presente_sin_asignar' END,
                               linea_fisica_actual = @linea_destino
                         WHERE Id = @personal_id;
                    COMMIT;
                END;
                """);
        }
    }
}

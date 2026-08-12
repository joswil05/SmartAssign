using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E8.3 (docs/PROGRESO.md): <c>sp_RecibirPersona</c> — paso 3 de
    /// Parte X ("El supervisor destino confirma que llegó físicamente, y
    /// solo entonces se le asigna el puesto"). Nótese el "y solo
    /// entonces": la recepción NO asigna ningún puesto — solo cierra el
    /// tránsito con <c>hora_llegada</c> real y deja a la persona
    /// disponible para el flujo normal de asignación
    /// (<c>sp_AsignarPersona</c>/<c>sp_SugerirPuesto</c>, ya existentes).
    /// Sin cambios de esquema: <c>Movimiento</c> ya traía todas las
    /// columnas que este paso necesita desde E8.1.
    ///
    /// 00 §C8: la confirmación es individual, persona por persona — nunca
    /// en bloque — por eso el parámetro es <c>@movimiento_id</c> (una
    /// fila concreta), no un filtro por línea que resolviera "todos los
    /// pendientes". "Proporcionalidad de la verificación" (mismo §C8):
    /// recibir no asigna un puesto, así que este procedimiento no exige
    /// ni valida categoría ni restricciones médicas — esas se verifican
    /// al asignar, que es cuando de verdad importan.
    ///
    /// El destino determina la situación resultante sin asumir que "la
    /// línea 8 es el Bolsón" (comentario de <c>Linea.EsBolson</c>, 04
    /// §2.1): si <c>linea_destino.es_bolson = 1</c> la persona queda
    /// <c>en_bolson</c> (Parte VI: "trabajando en ensamble manual en L8,
    /// disponible"); si no, <c>presente_sin_asignar</c> (Parte VI: "en
    /// sala de espera, disponible") — igual que si hubiera marcado
    /// entrada ahí mismo. <c>linea_fisica_actual</c> se actualiza al
    /// destino aquí, no en el despacho (E8.1): hasta este momento la
    /// persona sigue físicamente caminando, no ha llegado todavía.
    /// </summary>
    public partial class RecepcionDeMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_RecibirPersona;");
        }
    }
}

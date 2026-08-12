using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E8.4 (docs/PROGRESO.md): <c>sp_RechazarRecepcion</c> — la otra
    /// mitad del paso 3 de Parte X ("también puede rechazar la
    /// recepción, devolviéndola a la L8"), literal según 00 §C10:
    ///
    /// 1. La persona queda **en tránsito hacia L8**, no directamente
    ///    *En Bolsón* — sigue físicamente en la línea que la rechazó y
    ///    tiene que caminar (§12.7 necesita las dos horas también para
    ///    este segundo trayecto). Por eso este SP no toca
    ///    <c>Personal.situacion</c> (ya estaba <c>en_transito</c>, sigue
    ///    estándolo) ni <c>linea_fisica_actual</c> (nunca llegó de
    ///    verdad) — solo cierra el <c>Movimiento</c> original como
    ///    <c>rechazado</c> y abre uno nuevo hacia la L8 con
    ///    <c>motivo='rechazo_recepcion'</c> (ya en <c>CK_Mov_motivo</c>
    ///    desde E8.1). El orden UPDATE-antes-que-INSERT importa:
    ///    <c>UX_Mov_transito</c> exige que la fila original ya esté
    ///    fuera de <c>en_transito</c> antes de abrir la nueva.
    /// 2. "El puesto vuelve a la cola con su fatiga actual" — no aplica
    ///    todavía: sin reserva de puesto (E8.5, <c>puesto_destino_id</c>
    ///    sigue NULL en todo despacho), no hay nada que "devolver a la
    ///    cola" más allá de lo que ya es cierto (el puesto nunca dejó de
    ///    estar libre).
    /// 3. "Entra en la lista de descartados de ese puesto" (B10) — tabla
    ///    <c>RelevoDescartado</c> (04 §5.3), deliberadamente **fuera**
    ///    de esta UT: su propia UT dedicada es E9.6 ("Aceptar/rechazar +
    ///    descartados con caducidad"), la nota de ingeniería de esa
    ///    sección del esquema ya la trata junto al motor de relevos
    ///    completo (§9.4), no junto al ciclo de Movimiento.
    /// 4. Motivo obligatorio: <c>CK_Mov_rechazo</c> (E8.1) ya lo exige a
    ///    nivel de columna, pero este SP lo valida ANTES para devolver
    ///    un código de rechazo legible en vez de una violación de CHECK
    ///    cruda — mismo criterio que el resto de la familia. "Queda
    ///    auditado" es la única pieza de C10 que sí distingue esta UT de
    ///    E8.1/E8.3 (que deliberadamente no auditan todavía): se llama a
    ///    <c>sp_RegistrarAuditoria</c> (E2.5) porque el propio texto de
    ///    C10 lo exige explícitamente ("sin él, nadie puede detectarlo
    ///    después") — ningún otro paso de Parte X tiene esa frase.
    /// </summary>
    public partial class RechazoDeRecepcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RechazarRecepcion
                    @movimiento_id BIGINT, @usuario_id INT,
                    @motivo_rechazo_id SMALLINT = NULL,
                    @nota_rechazo NVARCHAR(300) = NULL,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    -- C10 p4: "el rechazo exige motivo" — se valida aquí
                    -- para un código legible, aunque CK_Mov_rechazo ya lo
                    -- exigiría de todas formas al UPDATE.
                    IF @motivo_rechazo_id IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'MOTIVO_RECHAZO_OBLIGATORIO';
                        SET @mensaje = 'El rechazo de una recepción necesita un motivo.';
                        RETURN;
                    END

                    IF NOT EXISTS (SELECT 1 FROM MotivoRechazoRecepcion WHERE Id = @motivo_rechazo_id AND activo = 1)
                    BEGIN
                        SET @codigo_rechazo = 'MOTIVO_RECHAZO_INVALIDO';
                        SET @mensaje = 'Ese motivo de rechazo no existe o no está activo.';
                        RETURN;
                    END

                    DECLARE @personal_id INT;
                    DECLARE @linea_origen_original TINYINT; -- de la que la persona salió originalmente
                    DECLARE @linea_destino TINYINT;         -- la que la está rechazando ahora
                    DECLARE @estado VARCHAR(20);
                    DECLARE @rol VARCHAR(15) = (SELECT rol FROM Usuario WHERE Id = @usuario_id);

                    BEGIN TRAN;
                        -- UPDLOCK: la misma fila que sp_RecibirPersona
                        -- bloquea — confirmar y rechazar a la vez sobre
                        -- el mismo tránsito se serializan igual.
                        SELECT @personal_id = personal_id, @linea_origen_original = linea_origen,
                               @linea_destino = linea_destino, @estado = estado
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

                        DECLARE @linea_bolson TINYINT = (SELECT Id FROM Linea WHERE es_bolson = 1);

                        UPDATE Movimiento
                           SET estado = 'rechazado', motivo_rechazo_id = @motivo_rechazo_id, nota_rechazo = @nota_rechazo
                         WHERE Id = @movimiento_id;

                        -- C10 p1: en tránsito HACIA la L8, no directamente
                        -- en_bolson — un segundo trayecto real con sus
                        -- propias horas (§12.7). El origen de este nuevo
                        -- tramo es la línea que rechazó (ahí está físicamente
                        -- parada, no en la línea de la que salió originalmente).
                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
                        VALUES (@personal_id, @linea_destino, @linea_bolson, 'rechazo_recepcion', @usuario_id);

                        -- C10 p4: "queda auditado" — la única pieza de
                        -- Parte X que lo exige explícitamente hasta ahora.
                        EXEC dbo.sp_RegistrarAuditoria
                             @usuario_id = @usuario_id, @rol = @rol, @accion = 'RECHAZAR_RECEPCION', @entidad = 'Movimiento',
                             @entidad_id = @movimiento_id, @personal_id = @personal_id, @linea_id = @linea_destino,
                             @resultado = 'OK', @device_id = NULL;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_RechazarRecepcion;");
        }
    }
}

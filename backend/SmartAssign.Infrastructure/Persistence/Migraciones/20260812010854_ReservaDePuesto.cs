using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E8.5 (docs/PROGRESO.md): reserva de puesto en el despacho —
    /// literal 00 §B4: "el puesto destino no puede estar ya reservado por
    /// otro relevista en tránsito. Sin esta guarda, dos personas
    /// convergen al mismo puesto y una queda sin destino a mitad de la
    /// planta." Sin cambios de esquema: <c>Movimiento.puesto_destino_id</c>
    /// y <c>UX_Mov_reserva</c> ya existían desde E8.1 (04 §5.2 estaba
    /// completa) — lo único que faltaba era que
    /// <c>sp_DespacharPersona</c> aceptara y usara el parámetro. Se
    /// extiende con <c>CREATE OR ALTER</c> y un nuevo parámetro con
    /// default <c>NULL</c>, así que E8.1-E8.4 (que nunca lo pasan) siguen
    /// exactamente igual — mismo criterio de extensión sin romper
    /// contrato que <c>fn_ExcesoRelativoFatiga</c>/<c>fn_NivelFatiga</c>
    /// en E7.4.
    ///
    /// **Deliberadamente NO incluido** (no está en el LEE de esta UT):
    /// el algoritmo de selección "el puesto fatigado más conveniente"
    /// (B4 puntos 1-3: misma línea/mayor exceso, fila de proximidad A1,
    /// L8) — eso es el motor de relevos completo, §9.4, con su propia
    /// etapa (E9). Esta UT solo construye la GUARDA de convergencia una
    /// vez que a alguien YA se le indicó un puesto destino concreto, sea
    /// cual sea el origen de esa decisión. Tampoco valida que el puesto
    /// pertenezca a <c>linea_destino</c> ni que sea rotativo — eso es
    /// responsabilidad de quien elija el puesto (E9), no de esta guarda
    /// mecánica; inventar esa validación aquí sería una regla de negocio
    /// no pedida (R2).
    ///
    /// Bloqueo determinista: <c>Puesto</c> se bloquea ANTES que
    /// <c>Personal</c> cuando hay <c>@puesto_destino_id</c> — mismo orden
    /// que <c>sp_AsignarPersona</c> (04 §7.3) para no interbloquear entre
    /// ambos procedimientos si algún día contienden por el mismo puesto.
    /// </summary>
    public partial class ReservaDePuesto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
                        VALUES (@personal_id, @linea_origen, @linea_destino, @puesto_destino_id, @motivo, @usuario_id);
                        SET @movimiento_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte sp_DespacharPersona a su forma de E8.1, sin reserva de puesto.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_DespacharPersona
                    @personal_id INT, @linea_destino TINYINT, @motivo VARCHAR(30), @usuario_id INT,
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

                    BEGIN TRAN;
                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        IF EXISTS (SELECT 1 FROM Movimiento WHERE personal_id = @personal_id AND estado = 'en_transito')
                        BEGIN
                            SET @codigo_rechazo = 'YA_EN_TRANSITO';
                            SET @mensaje = 'Esta persona ya está en tránsito hacia otra línea.';
                            COMMIT;
                            RETURN;
                        END

                        INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
                        VALUES (@personal_id, @linea_origen, @linea_destino, @motivo, @usuario_id);
                        SET @movimiento_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;
                    COMMIT;
                END;
                """);
        }
    }
}

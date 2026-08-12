using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E11.2 (docs/PROGRESO.md): <c>sp_RegistrarParo</c> gana lo que
    /// el catálogo de 04 §7.4 ya le atribuye en una sola línea:
    /// "Libera rotativos, conserva fijos, genera tránsitos" — §11.1,
    /// literal: "Los puestos fijos permanecen ocupados: los operadores
    /// técnicos son quienes ejecutan la reparación. Los puestos
    /// rotativos se liberan y sus operarios se reubican en la L8, para
    /// no quedar ociosos."
    ///
    /// Sin cambios de esquema: <c>Movimiento.motivo</c> ya admitía
    /// <c>'paro'</c> desde <c>CK_Mov_motivo</c> original de E8.1 —
    /// anticipado exactamente para esta UT.
    ///
    /// **Fijos: no se tocan.** El cursor solo recorre
    /// <c>Puesto.tipo = 'rotativo'</c> — ningún fijo entra en el
    /// recorrido, ni siquiera se evalúa.
    ///
    /// **Rotativos: tránsito INDIVIDUAL, no en bloque** (00 §C8,
    /// literal: "Cada persona genera su propio tránsito con su hora de
    /// salida y de llegada [...] la confirmación de recepción es
    /// individual, persona por persona. No hay confirmación en
    /// bloque."). Por eso el cursor inserta un <c>Movimiento</c> por
    /// cada ocupante, no una fila agregada. **No reutiliza
    /// <c>sp_DespacharPersona</c>** — mismo motivo que
    /// <c>sp_ExtraccionInversa</c> (E10.3) y <c>sp_CubrirVacanteCritica</c>
    /// (E10.4): el ocupante está <c>asignado</c> de verdad, no
    /// "disponible (Bolsón o sin asignar)", así que cierra su
    /// <c>Asignacion</c> e inserta el <c>Movimiento</c> directamente.
    /// Destino siempre la L8, sin <c>puesto_destino_id</c> — "se
    /// reubican en la L8" es genérico, ningún puesto concreto que
    /// reservar (a diferencia del motor de relevos, B4).
    ///
    /// **Deliberadamente fuera de alcance:**
    /// - 00 §C9 ("relevista en tránsito hacia una línea que entra en
    ///   paro") tiene su propia UT, E11.4 — nada aquí toca
    ///   <c>Movimiento.puesto_destino_id</c> reservado por otros ni el
    ///   estado que ve el supervisor destino al recibir.
    /// - <c>SolicitudRelevo</c> abiertas sobre los rotativos liberados
    ///   no se resuelven aquí: ni §11.1 ni 00 §C8 lo piden, y B10/B3 ya
    ///   tienen su propio ciclo de vida — cerrarlas sin que la fuente lo
    ///   exija sería inventar una regla de negocio (R2).
    /// - Cronómetro visible (§11.1) es E11.3, pantalla de Android, no
    ///   backend.
    ///
    /// <c>@rotativos_liberados</c> es un nuevo OUTPUT con default
    /// (<c>= 0 OUTPUT</c>), así que las llamadas de E11.1 que no lo
    /// pasan siguen funcionando exactamente igual — mismo criterio de
    /// extensión sin romper contrato que <c>@puesto_destino_id</c> en
    /// <c>sp_DespacharPersona</c> (E8.5).
    /// </summary>
    public partial class FijosOcupadosRotativosLiberados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RegistrarParo
                    @jornada_linea_id INT, @categoria_id SMALLINT, @causa_id SMALLINT,
                    @descripcion NVARCHAR(500), @usuario_id INT,
                    @paro_id INT OUTPUT,
                    @rotativos_liberados INT = 0 OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @paro_id = NULL; SET @rotativos_liberados = 0;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF LEN(LTRIM(RTRIM(ISNULL(@descripcion, '')))) = 0
                    BEGIN
                        SET @codigo_rechazo = 'DESCRIPCION_OBLIGATORIA';
                        SET @mensaje = 'Debes describir qué observaste antes de confirmar el paro.';
                        RETURN;
                    END

                    -- Dos niveles (§11.1): la causa tiene que pertenecer a la categoría elegida.
                    IF NOT EXISTS (SELECT 1 FROM CausaParo WHERE Id = @causa_id AND categoria_id = @categoria_id)
                    BEGIN
                        SET @codigo_rechazo = 'CAUSA_NO_PERTENECE_A_LA_CATEGORIA';
                        SET @mensaje = 'Esa causa no pertenece a la categoría seleccionada.';
                        RETURN;
                    END

                    DECLARE @linea_id TINYINT = (SELECT linea_id FROM JornadaLinea WHERE Id = @jornada_linea_id);
                    DECLARE @linea_bolson TINYINT = (SELECT Id FROM Linea WHERE es_bolson = 1);

                    BEGIN TRAN;
                        -- Bloqueo determinista sobre la fila padre (JornadaLinea) — mismo
                        -- patrón que sp_MarcarRelevoSolicitado (E9.1) para serializar dos
                        -- intentos concurrentes de abrir un paro en la misma línea.
                        SELECT 1 FROM JornadaLinea WITH (UPDLOCK, HOLDLOCK) WHERE Id = @jornada_linea_id;

                        IF EXISTS (SELECT 1 FROM Paro WHERE jornada_linea_id = @jornada_linea_id AND fin IS NULL)
                        BEGIN
                            SET @codigo_rechazo = 'PARO_YA_ABIERTO';
                            SET @mensaje = 'Esta línea ya tiene un paro abierto.';
                            COMMIT;
                            RETURN;
                        END

                        INSERT INTO Paro (jornada_linea_id, categoria_id, causa_id, descripcion, registrado_por)
                        VALUES (@jornada_linea_id, @categoria_id, @causa_id, @descripcion, @usuario_id);
                        SET @paro_id = SCOPE_IDENTITY();

                        -- §11.1: fijos permanecen ocupados (ni siquiera entran al
                        -- recorrido); rotativos se liberan con tránsito INDIVIDUAL
                        -- hacia la L8 (00 §C8).
                        DECLARE @personal_id INT;
                        DECLARE cur_rotativos CURSOR LOCAL FAST_FORWARD FOR
                            SELECT a.personal_id
                              FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_id AND p.tipo = 'rotativo' AND a.fin IS NULL;

                        OPEN cur_rotativos;
                        FETCH NEXT FROM cur_rotativos INTO @personal_id;
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            UPDATE Asignacion SET fin = SYSUTCDATETIME(), motivo_fin = 'paro'
                             WHERE personal_id = @personal_id AND fin IS NULL;

                            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
                            VALUES (@personal_id, @linea_id, @linea_bolson, 'paro', @usuario_id);

                            UPDATE Personal SET situacion = 'en_transito' WHERE Id = @personal_id;

                            SET @rotativos_liberados += 1;
                            FETCH NEXT FROM cur_rotativos INTO @personal_id;
                        END
                        CLOSE cur_rotativos; DEALLOCATE cur_rotativos;
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte sp_RegistrarParo a su forma de E11.1, sin liberar rotativos.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RegistrarParo
                    @jornada_linea_id INT, @categoria_id SMALLINT, @causa_id SMALLINT,
                    @descripcion NVARCHAR(500), @usuario_id INT,
                    @paro_id INT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @paro_id = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF LEN(LTRIM(RTRIM(ISNULL(@descripcion, '')))) = 0
                    BEGIN
                        SET @codigo_rechazo = 'DESCRIPCION_OBLIGATORIA';
                        SET @mensaje = 'Debes describir qué observaste antes de confirmar el paro.';
                        RETURN;
                    END

                    IF NOT EXISTS (SELECT 1 FROM CausaParo WHERE Id = @causa_id AND categoria_id = @categoria_id)
                    BEGIN
                        SET @codigo_rechazo = 'CAUSA_NO_PERTENECE_A_LA_CATEGORIA';
                        SET @mensaje = 'Esa causa no pertenece a la categoría seleccionada.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        SELECT 1 FROM JornadaLinea WITH (UPDLOCK, HOLDLOCK) WHERE Id = @jornada_linea_id;

                        IF EXISTS (SELECT 1 FROM Paro WHERE jornada_linea_id = @jornada_linea_id AND fin IS NULL)
                        BEGIN
                            SET @codigo_rechazo = 'PARO_YA_ABIERTO';
                            SET @mensaje = 'Esta línea ya tiene un paro abierto.';
                            COMMIT;
                            RETURN;
                        END

                        INSERT INTO Paro (jornada_linea_id, categoria_id, causa_id, descripcion, registrado_por)
                        VALUES (@jornada_linea_id, @categoria_id, @causa_id, @descripcion, @usuario_id);
                        SET @paro_id = SCOPE_IDENTITY();
                    COMMIT;
                END;
                """);
        }
    }
}

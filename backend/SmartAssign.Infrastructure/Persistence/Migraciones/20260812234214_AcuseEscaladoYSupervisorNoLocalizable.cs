using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E12.6 (docs/PROGRESO.md): "Acuse, escalado y 'supervisor no
    /// localizable'" (D5, 04 §10) — cierra E12 (6/6) → F10.
    ///
    /// Sin cambios de esquema: <c>Notificacion.EscaladaEn</c>/<c>AcusadaEn</c>
    /// ya existían desde E12.4 (04 §10 los traía especificados desde el
    /// principio). Esta UT solo agrega el procedimiento que los usa.
    ///
    /// <c>sp_EscalarNotificacionesVencidas</c> (nuevo) — mismo patrón de
    /// "barrido" que <c>sp_CaducarTransitos</c> (E8.6): un procedimiento
    /// idempotente y repetible que un servicio en segundo plano dispara
    /// por su cuenta. A diferencia de ese, cada fila vencida SÍ dispara un
    /// efecto (encolar <c>AlertaCoordinadorEvento</c>, D5: "escala al
    /// Coordinador... 'supervisor no localizable'"), así que en vez de un
    /// solo <c>UPDATE</c> masivo usa un cursor — mismo criterio que el
    /// cursor de rotativos de <c>sp_RegistrarParo</c> (E11.2/E12.3) — con
    /// <c>UPDATE</c> + <c>EXEC sp_EncolarEvento</c> (E12.3) atómicos por
    /// fila, dentro de su propia transacción: escalar sin avisar al
    /// Coordinador, o avisar sin marcar <c>escalada_en</c>, serían ambos
    /// un estado a medias.
    ///
    /// <c>notificacion_acuse_timeout_min</c> (04 §9) sigue "a definir" —
    /// sin sembrar, el procedimiento no escala NADA (honestidad del dato,
    /// §12.4): "el tiempo configurado" de D5 literalmente no existe
    /// todavía, mismo criterio exacto que <c>fn_NivelFatiga</c> (E7.3) o
    /// <c>umbral_desperdicio_justificacion_pct</c> (E11.6) sin sembrar. A
    /// diferencia de <c>duracion_maxima_transito_min</c> (E8.6), ninguna
    /// fuente da aquí un número provisional — no hay ISNULL con default.
    ///
    /// **Decisión de alcance (R2):** ningún productor real de esta sesión
    /// emite todavía <c>criticidad='critica'</c> — <c>sp_DespacharPersona</c>
    /// (E12.4) usa <c>'normal'</c> porque ninguna fuente dice que
    /// "TransitoEntrante" sea crítico. Qué escenarios de negocio SON
    /// críticos es una clasificación que nadie ha decidido — no se
    /// inventa aquí (mismo criterio que "cobertura de la línea" en
    /// E11.8). Esta UT prueba el MECANISMO completo con notificaciones
    /// críticas encoladas directamente (mismo criterio que
    /// <c>sp_EncolarEvento</c> probado con un evento sintético en E12.3).
    /// </summary>
    public partial class AcuseEscaladoYSupervisorNoLocalizable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_EscalarNotificacionesVencidas
                    @escaladas INT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @escaladas = 0;

                    DECLARE @timeout_min INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'notificacion_acuse_timeout_min');
                    -- Sin el parámetro configurado no hay "tiempo configurado"
                    -- que aplicar (honestidad del dato, §12.4) — no se inventa
                    -- un número razonable.
                    IF @timeout_min IS NULL RETURN;

                    DECLARE @notificacion_id BIGINT, @titulo NVARCHAR(120), @usuario_id INT;

                    DECLARE cur_vencidas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT Id, titulo, usuario_id
                          FROM Notificacion
                         WHERE criticidad = 'critica'
                           AND acusada_en IS NULL
                           AND escalada_en IS NULL
                           AND DATEDIFF(MINUTE, creada_en, SYSUTCDATETIME()) >= @timeout_min;

                    OPEN cur_vencidas;
                    FETCH NEXT FROM cur_vencidas INTO @notificacion_id, @titulo, @usuario_id;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        BEGIN TRAN;
                            -- UPDLOCK/HOLDLOCK: dos sondeos concurrentes del
                            -- mismo dispatcher (o dos instancias) no pueden
                            -- escalar la misma fila dos veces.
                            UPDATE Notificacion WITH (UPDLOCK, HOLDLOCK)
                               SET escalada_en = SYSUTCDATETIME()
                             WHERE Id = @notificacion_id AND escalada_en IS NULL;

                            IF @@ROWCOUNT > 0
                            BEGIN
                                -- D5, literal: "escala al Coordinador y aparece
                                -- en su panel como 'supervisor no localizable'".
                                DECLARE @mensaje NVARCHAR(300) =
                                    N'Supervisor no localizable: sin acuse de "' + @titulo + N'".';
                                DECLARE @payload_escalado NVARCHAR(MAX) = JSON_OBJECT(
                                    'NotificacionId': @notificacion_id, 'UsuarioId': @usuario_id, 'Mensaje': @mensaje);
                                EXEC dbo.sp_EncolarEvento
                                    @tipo_evento = 'AlertaCoordinadorEvento',
                                    @grupos = 'planta',
                                    @payload_json = @payload_escalado;

                                SET @escaladas += 1;
                            END
                        COMMIT;

                        FETCH NEXT FROM cur_vencidas INTO @notificacion_id, @titulo, @usuario_id;
                    END
                    CLOSE cur_vencidas; DEALLOCATE cur_vencidas;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_EscalarNotificacionesVencidas;");
        }
    }
}

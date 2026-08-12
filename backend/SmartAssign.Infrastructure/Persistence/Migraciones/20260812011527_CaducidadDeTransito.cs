using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E8.6 (docs/PROGRESO.md): <c>sp_CaducarTransitos</c> — cierra E8.
    /// Literal 00 §B11: "Hoy una persona en tránsito es inmune (§6.1) y
    /// su puesto destino queda reservado sin ninguna salida definida. Si
    /// nunca llega, queda congelada y el puesto bloqueado." Este SP es
    /// esa salida — pero "la caducidad convierte un cuelgue silencioso
    /// en una alerta accionable, **sin mover a nadie**": no cambia
    /// <c>estado</c> (sigue <c>en_transito</c>), no toca
    /// <c>Personal.situacion</c>, no libera la reserva del puesto. Solo
    /// marca <c>caducado_en</c> — columna que ya existía desde E8.1
    /// reservada exactamente para esto ("B11 lo marca, no lo borra").
    ///
    /// <c>duracion_maxima_transito</c>: B11 da un número real, "valor
    /// inicial provisional: 15 minutos, a calibrar con los datos de
    /// salida/llegada del §12.7" — mismo caso que <c>factor_doble_turno</c>
    /// en E7.4 (00 §B7), la excepción a "nunca un default inventado": el
    /// propio documento especifica el número. Mismo patrón exacto de
    /// <c>ISNULL(TRY_CAST(...), default)</c> sobre <c>Parametro</c>.
    ///
    /// Deliberadamente **fuera** de esta UT (no está en su LEE, `00
    /// §B11` describe la cancelación pero el título de la UT en el plan
    /// es explícito: "alerta y no mueve a nadie"): la parte de B11 sobre
    /// "cancelar es acto humano" — el supervisor libera la reserva o el
    /// Coordinador cancela el tránsito y la persona vuelve a
    /// <c>presente_sin_asignar</c> — es una acción DISTINTA, deliberada,
    /// de un humano, no algo que este job automático haga. No tiene UT
    /// propia asignada en 07_PLAN_DE_EJECUCION.md todavía;
    /// <c>Movimiento.CanceladoPor</c> ya existe desde E8.1, reservado
    /// para cuando llegue.
    ///
    /// Notificar a los tres supervisores (origen, destino, Coordinador)
    /// tampoco es parte de esta UT: no hay mecanismo de entrega de
    /// alertas construido todavía (`DispositivoPush` es solo el
    /// registro del dispositivo, E2). El marcador <c>caducado_en</c> es
    /// la señal que una capa de notificación futura consumiría — SIN
    /// inventar esa capa aquí.
    /// </summary>
    public partial class CaducidadDeTransito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_CaducarTransitos
                    @caducados INT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    DECLARE @duracion_maxima INT = ISNULL(
                        (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'duracion_maxima_transito'),
                        15
                    );

                    -- Un solo UPDATE atómico — "sin mover a nadie": no
                    -- toca estado ni Personal.situacion, solo marca.
                    UPDATE Movimiento
                       SET caducado_en = SYSUTCDATETIME()
                     WHERE estado = 'en_transito'
                       AND caducado_en IS NULL
                       AND DATEDIFF(MINUTE, hora_salida, SYSUTCDATETIME()) >= @duracion_maxima;

                    SET @caducados = @@ROWCOUNT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CaducarTransitos;");
        }
    }
}

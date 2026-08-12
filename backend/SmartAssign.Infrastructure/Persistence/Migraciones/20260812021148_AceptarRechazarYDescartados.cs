using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E9.6 (docs/PROGRESO.md): aceptar/rechazar la propuesta de la
    /// L8 + lista de descartados con caducidad — §9.4 paso 3, 00 §B10.
    ///
    /// <c>RelevoDescartado</c> creada completa (04 §5.3 ya la
    /// especificaba entera desde que se leyó en E9.1). <c>jornada_dia</c>
    /// es el <c>DiaOperacion</c> de la <c>JornadaLinea</c> abierta de la
    /// línea del puesto — NO un <c>CAST(SYSUTCDATETIME() AS DATE)</c>
    /// crudo: un turno nocturno cruza medianoche sin cambiar de "día de
    /// turno" (04 §4.1, comentario de <c>JornadaLinea.DiaOperacion</c>,
    /// §C6), y B10 dice literal "caduca al CIERRE DE TURNO", no a
    /// medianoche del calendario.
    ///
    /// <c>sp_AceptarRelevo</c> ("tránsito + reserva atómicos", catálogo
    /// 04 §7.4): orquesta piezas que ya existen, no reimplementa
    /// despacho — pide el candidato a <c>sp_ProponerRelevista</c> (E9.5)
    /// y lo despacha con <c>sp_DespacharPersona</c> (E8.1) pasando
    /// <c>@puesto_destino_id</c> (la reserva de E8.5). Si el despacho
    /// sale bien, cierra la <c>SolicitudRelevo</c> como
    /// <c>resultado='cubierta'</c> — "cubierta" en el sentido de que ya
    /// hay un relevista comprometido en camino, no que el puesto ya
    /// tenga a alguien físicamente (eso es la recepción, §9.4 p5,
    /// deliberadamente fuera de esta UT).
    ///
    /// <c>sp_RechazarPropuestaRelevo</c> (paso 3, rama de rechazo): NO
    /// resuelve la <c>SolicitudRelevo</c> — sigue abierta, literal
    /// "el sistema carga otra sugerencia si hay alguna disponible". Solo
    /// registra el descarte del par (puesto, persona) de HOY.
    ///
    /// <c>sp_ProponerRelevista</c> se extiende (<c>CREATE OR ALTER</c>,
    /// mismo criterio que E7.4/E8.5) para excluir a quien tenga un
    /// descarte vigente (mismo puesto, misma <c>jornada_dia</c>, sin
    /// limpiar) — cierra la promesa que el catálogo de 04 §7.4 ya hacía
    /// desde antes de que <c>RelevoDescartado</c> existiera ("Ranking
    /// B2, excluye descartados").
    ///
    /// <c>sp_LimpiarDescartado</c>: "dentro del turno lo limpian el
    /// supervisor de la L8 (que lo creó) o el Coordinador" (B10) —
    /// verificado en SQL con <c>@rol</c>/<c>@usuario_id</c>, mismo
    /// patrón que el resto de la familia de Movimiento.
    /// </summary>
    public partial class AceptarRechazarYDescartados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelevoDescartado",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    puesto_id = table.Column<int>(type: "int", nullable: false),
                    personal_id = table.Column<int>(type: "int", nullable: false),
                    jornada_dia = table.Column<DateOnly>(type: "date", nullable: false),
                    descartado_por = table.Column<int>(type: "int", nullable: false),
                    descartado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    limpiado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    limpiado_por = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelevoDescartado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelevoDescartado_Personal_personal_id",
                        column: x => x.personal_id,
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RelevoDescartado_Puesto_puesto_id",
                        column: x => x.puesto_id,
                        principalTable: "Puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RelevoDescartado_Usuario_descartado_por",
                        column: x => x.descartado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RelevoDescartado_Usuario_limpiado_por",
                        column: x => x.limpiado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RelevoDescartado_descartado_por",
                table: "RelevoDescartado",
                column: "descartado_por");

            migrationBuilder.CreateIndex(
                name: "IX_RelevoDescartado_limpiado_por",
                table: "RelevoDescartado",
                column: "limpiado_por");

            migrationBuilder.CreateIndex(
                name: "IX_RelevoDescartado_personal_id",
                table: "RelevoDescartado",
                column: "personal_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Descartado",
                table: "RelevoDescartado",
                columns: new[] { "puesto_id", "personal_id", "jornada_dia" },
                unique: true);

            // ── sp_ProponerRelevista extendido: excluye descartados vigentes (B10) ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ProponerRelevista
                    @puesto_id INT,
                    @candidato_id INT OUTPUT,
                    @cede_perfil BIT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @candidato_id = NULL; SET @cede_perfil = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @linea_bolson TINYINT = (SELECT Id FROM Linea WHERE es_bolson = 1);
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);
                    DECLARE @linea_puesto TINYINT = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id);
                    DECLARE @jornada_dia DATE = (
                        SELECT TOP (1) dia_operacion FROM JornadaLinea
                         WHERE linea_id = @linea_puesto AND cerrado_en IS NULL
                         ORDER BY Id DESC);

                    SELECT TOP (1)
                        @candidato_id = per.Id,
                        @cede_perfil = dbo.fn_PerfilIncompatible(per.Id, @puesto_id)
                    FROM Personal per
                    WHERE per.situacion = 'en_bolson'
                      AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id) = 1
                      AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id, @hoy) = 0
                      AND NOT EXISTS (
                          SELECT 1 FROM RelevoDescartado rd
                           WHERE rd.puesto_id = @puesto_id AND rd.personal_id = per.Id
                             AND rd.jornada_dia = @jornada_dia AND rd.limpiado_en IS NULL)
                    ORDER BY
                        -- 1 · titular/habitual del puesto
                        CASE WHEN EXISTS (SELECT 1 FROM Puesto p WHERE p.Id = @puesto_id AND p.titular_id = per.Id)
                             THEN 0 ELSE 1 END ASC,
                        -- 2 · más tiempo en el Bolsón (llegada más antigua primero)
                        ISNULL(
                            (SELECT MAX(m.hora_llegada) FROM Movimiento m
                              WHERE m.personal_id = per.Id AND m.linea_destino = @linea_bolson AND m.estado = 'recibido'),
                            '1900-01-01') ASC,
                        -- 3 · menor fatiga acumulada en la jornada de hoy
                        (SELECT ISNULL(SUM(DATEDIFF(MINUTE, a.inicio, ISNULL(a.fin, SYSUTCDATETIME()))), 0)
                           FROM Asignacion a
                          WHERE a.personal_id = per.Id AND CAST(a.inicio AS DATE) = @hoy) ASC,
                        -- 4 · perfil preferente ordena, no excluye — el peso más bajo (B12)
                        dbo.fn_PerfilIncompatible(per.Id, @puesto_id) ASC,
                        -- 5 · ficha ascendente, desempate estable
                        per.Ficha ASC;

                    IF @candidato_id IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'SIN_CANDIDATOS_EN_BOLSON';
                        SET @mensaje = 'No hay nadie en el Bolsón compatible con este puesto ahora mismo.';
                    END
                END;
                """);

            // ── sp_AceptarRelevo (§9.4 p3, rama de aceptación) ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_AceptarRelevo
                    @solicitud_id BIGINT, @usuario_id INT,
                    @candidato_id INT OUTPUT,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @candidato_id = NULL; SET @movimiento_id = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @puesto_id INT;
                    DECLARE @linea_destino TINYINT;
                    DECLARE @resuelta_en DATETIME2(0);

                    SELECT @puesto_id = sr.puesto_id, @resuelta_en = sr.resuelta_en
                      FROM SolicitudRelevo sr WHERE sr.Id = @solicitud_id;

                    IF @puesto_id IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'SOLICITUD_INEXISTENTE';
                        SET @mensaje = 'Esta solicitud de relevo no existe.';
                        RETURN;
                    END

                    IF @resuelta_en IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'SOLICITUD_NO_ABIERTA';
                        SET @mensaje = 'Esta solicitud de relevo ya fue resuelta.';
                        RETURN;
                    END

                    SET @linea_destino = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id);

                    DECLARE @cede_perfil BIT;
                    EXEC dbo.sp_ProponerRelevista
                         @puesto_id = @puesto_id,
                         @candidato_id = @candidato_id OUTPUT,
                         @cede_perfil = @cede_perfil OUTPUT,
                         @codigo_rechazo = @codigo_rechazo OUTPUT,
                         @mensaje = @mensaje OUTPUT;

                    IF @candidato_id IS NULL
                        RETURN; -- @codigo_rechazo/@mensaje ya los puso sp_ProponerRelevista

                    EXEC dbo.sp_DespacharPersona
                         @personal_id = @candidato_id, @linea_destino = @linea_destino,
                         @motivo = 'relevo', @usuario_id = @usuario_id, @puesto_destino_id = @puesto_id,
                         @movimiento_id = @movimiento_id OUTPUT,
                         @codigo_rechazo = @codigo_rechazo OUTPUT,
                         @mensaje = @mensaje OUTPUT;

                    IF @movimiento_id IS NULL
                    BEGIN
                        SET @candidato_id = NULL; -- el despacho falló pese a la propuesta — no hay relevista comprometido
                        RETURN;
                    END

                    -- Paso 3, rama aceptación: "el candidato queda en tránsito... y
                    -- el puesto fatigado queda reservado para él" — sp_DespacharPersona
                    -- ya hizo ambas cosas (Movimiento + UX_Mov_reserva). Aquí solo se
                    -- cierra la solicitud como cubierta.
                    UPDATE SolicitudRelevo
                       SET resuelta_en = SYSUTCDATETIME(), resultado = 'cubierta', movimiento_id = @movimiento_id
                     WHERE Id = @solicitud_id;
                END;
                """);

            // ── sp_RechazarPropuestaRelevo (§9.4 p3, rama de rechazo — B10) ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RechazarPropuestaRelevo
                    @solicitud_id BIGINT, @personal_id INT, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @puesto_id INT;
                    DECLARE @linea_id TINYINT;
                    DECLARE @resuelta_en DATETIME2(0);

                    SELECT @puesto_id = sr.puesto_id, @resuelta_en = sr.resuelta_en
                      FROM SolicitudRelevo sr WHERE sr.Id = @solicitud_id;

                    IF @puesto_id IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'SOLICITUD_INEXISTENTE';
                        SET @mensaje = 'Esta solicitud de relevo no existe.';
                        RETURN;
                    END

                    IF @resuelta_en IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'SOLICITUD_NO_ABIERTA';
                        SET @mensaje = 'Esta solicitud de relevo ya fue resuelta.';
                        RETURN;
                    END

                    SET @linea_id = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id);

                    DECLARE @jornada_dia DATE = (
                        SELECT TOP (1) dia_operacion FROM JornadaLinea
                         WHERE linea_id = @linea_id AND cerrado_en IS NULL
                         ORDER BY Id DESC);

                    IF @jornada_dia IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'SIN_JORNADA_ABIERTA';
                        SET @mensaje = 'La línea de este puesto no tiene una jornada abierta.';
                        RETURN;
                    END

                    IF EXISTS (
                        SELECT 1 FROM RelevoDescartado
                         WHERE puesto_id = @puesto_id AND personal_id = @personal_id
                           AND jornada_dia = @jornada_dia AND limpiado_en IS NULL)
                    BEGIN
                        SET @codigo_rechazo = 'YA_DESCARTADO';
                        SET @mensaje = 'Ya se había descartado a esta persona para este puesto hoy.';
                        RETURN;
                    END

                    -- B10: "el sistema carga otra sugerencia si hay alguna disponible"
                    -- — la SolicitudRelevo NO se toca, sigue abierta a propósito.
                    INSERT INTO RelevoDescartado (puesto_id, personal_id, jornada_dia, descartado_por)
                    VALUES (@puesto_id, @personal_id, @jornada_dia, @usuario_id);
                END;
                """);

            // ── sp_LimpiarDescartado (B10: "debe existir forma de limpiar") ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_LimpiarDescartado
                    @descarte_id BIGINT, @usuario_id INT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @descartado_por INT;
                    DECLARE @limpiado_en DATETIME2(0);
                    DECLARE @rol VARCHAR(15) = (SELECT rol FROM Usuario WHERE Id = @usuario_id);

                    SELECT @descartado_por = descartado_por, @limpiado_en = limpiado_en
                      FROM RelevoDescartado WHERE Id = @descarte_id;

                    IF @descartado_por IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'DESCARTE_INEXISTENTE';
                        SET @mensaje = 'Este descarte no existe.';
                        RETURN;
                    END

                    IF @limpiado_en IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'DESCARTE_YA_LIMPIADO';
                        SET @mensaje = 'Este descarte ya estaba limpio.';
                        RETURN;
                    END

                    -- B10: "lo limpian el supervisor de la L8 (que lo creó) o el
                    -- Coordinador. El supervisor destino NO: no manda sobre personal ajeno".
                    IF @rol <> 'coordinador' AND @usuario_id <> @descartado_por
                    BEGIN
                        SET @codigo_rechazo = 'SIN_PERMISO_PARA_LIMPIAR';
                        SET @mensaje = 'Solo quien descartó o el Coordinador pueden limpiar este descarte.';
                        RETURN;
                    END

                    UPDATE RelevoDescartado
                       SET limpiado_en = SYSUTCDATETIME(), limpiado_por = @usuario_id
                     WHERE Id = @descarte_id;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_LimpiarDescartado;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_RechazarPropuestaRelevo;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_AceptarRelevo;");

            // Revierte sp_ProponerRelevista a su forma de E9.5, sin excluir descartados.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ProponerRelevista
                    @puesto_id INT,
                    @candidato_id INT OUTPUT,
                    @cede_perfil BIT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @candidato_id = NULL; SET @cede_perfil = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @linea_bolson TINYINT = (SELECT Id FROM Linea WHERE es_bolson = 1);
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);

                    SELECT TOP (1)
                        @candidato_id = per.Id,
                        @cede_perfil = dbo.fn_PerfilIncompatible(per.Id, @puesto_id)
                    FROM Personal per
                    WHERE per.situacion = 'en_bolson'
                      AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id) = 1
                      AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id, @hoy) = 0
                    ORDER BY
                        CASE WHEN EXISTS (SELECT 1 FROM Puesto p WHERE p.Id = @puesto_id AND p.titular_id = per.Id)
                             THEN 0 ELSE 1 END ASC,
                        ISNULL(
                            (SELECT MAX(m.hora_llegada) FROM Movimiento m
                              WHERE m.personal_id = per.Id AND m.linea_destino = @linea_bolson AND m.estado = 'recibido'),
                            '1900-01-01') ASC,
                        (SELECT ISNULL(SUM(DATEDIFF(MINUTE, a.inicio, ISNULL(a.fin, SYSUTCDATETIME()))), 0)
                           FROM Asignacion a
                          WHERE a.personal_id = per.Id AND CAST(a.inicio AS DATE) = @hoy) ASC,
                        dbo.fn_PerfilIncompatible(per.Id, @puesto_id) ASC,
                        per.Ficha ASC;

                    IF @candidato_id IS NULL
                    BEGIN
                        SET @codigo_rechazo = 'SIN_CANDIDATOS_EN_BOLSON';
                        SET @mensaje = 'No hay nadie en el Bolsón compatible con este puesto ahora mismo.';
                    END
                END;
                """);

            migrationBuilder.DropTable(
                name: "RelevoDescartado");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E9.5 (docs/PROGRESO.md): <c>sp_ProponerRelevista</c> — §9.4
    /// paso 2, literal 00 §B2: "el candidato compatible más apto" entre
    /// el personal de la L8 disponible (<c>situacion='en_bolson'</c>),
    /// compatible (§4.2, <c>fn_CategoriaCompatible</c>, E4.6) y sin
    /// restricción médica que lo impida (§7.2,
    /// <c>fn_TieneRestriccionBloqueante</c>, E4.6). Propone UNO —
    /// <c>TOP (1)</c> tras el orden completo — mismo estilo que
    /// <c>sp_SugerirPuesto</c> (E6.7): nunca una lista, siempre "el
    /// mejor ahora mismo".
    ///
    /// Orden literal de B2, en este orden exacto:
    /// 1. Titular/habitual del puesto destino (<c>Puesto.titular_id</c>).
    /// 2. Más tiempo en el Bolsón — <c>MAX(hora_llegada)</c> de su
    ///    <c>Movimiento</c> más reciente recibido hacia la línea Bolsón
    ///    (único camino real hoy para llegar a <c>en_bolson</c>, E8.3);
    ///    sin ninguno registrado (nunca tuvo un tránsito real, p. ej.
    ///    sembrado directo), se trata como el peor caso de este
    ///    criterio — no se puede demostrar antigüedad que no existe.
    /// 3. Menor fatiga acumulada en la jornada — suma en minutos de
    ///    todas sus <c>Asignacion</c> de HOY (<c>inicio</c> de hoy),
    ///    cerradas o en curso. Concepto distinto del reloj de fatiga del
    ///    puesto (E7.1): es historial de desgaste de la PERSONA, no del
    ///    puesto actual.
    /// 4. Ficha ascendente — desempate estable (B2: "la misma situación
    ///    debe producir siempre la misma sugerencia").
    ///
    /// El perfil preferente (§7.3, <c>fn_PerfilIncompatible</c>) se
    /// intercala como quinto criterio, justo antes de ficha: "ordena, no
    /// excluye" y B12 confirma que es la ÚNICA regla que alguna vez cede
    /// — el peso más bajo de todos, nunca por delante de titular/tiempo
    /// en Bolsón/fatiga. <c>@cede_perfil</c> marca explícitamente cuando
    /// el elegido solo llega ahí cediendo perfil (B2: "se propone si no
    /// hay otro, marcado explícitamente como tal").
    ///
    /// Deliberadamente **sin excluir descartados** (B10) todavía, aunque
    /// el catálogo de 04 §7.4 describe el procedimiento final con esa
    /// exclusión ("Ranking B2, excluye descartados") — <c>RelevoDescartado</c>
    /// no existe hasta E9.6, que también extiende este mismo
    /// procedimiento (<c>CREATE OR ALTER</c>) para añadir el filtro,
    /// mismo criterio de extensión ya usado en E7.4/E8.5.
    /// </summary>
    public partial class ProponerRelevista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ProponerRelevista;");
        }
    }
}

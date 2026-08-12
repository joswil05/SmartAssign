using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E9.7 (docs/PROGRESO.md): <c>sp_SugerirDestinoRelevado</c> —
    /// §9.4 paso 6, literal 00 §B4: dónde va el relevado. Escalera de
    /// tres niveles, en este orden exacto:
    ///
    /// 1. **Misma línea primero** — entre los puestos fatigados
    ///    (<c>fn_NivelFatiga</c> sugerido/crítico, E7.3) y compatibles de
    ///    su propia línea, el de **mayor exceso relativo**
    ///    (<c>fn_ExcesoRelativoFatiga</c>, E7.2/E7.4).
    /// 2. Si no hay ninguno, recorre <c>ProximidadLinea</c> (00 §A1) en
    ///    su propio orden — nunca la jerarquía de prioridad de líneas:
    ///    00 §A9 es tajante, "el motor de relevos se rige únicamente por
    ///    proximidad... la jerarquía de prioridad solo aplica a la
    ///    asignación inicial" — en la primera línea con un puesto
    ///    fatigado compatible, otra vez el de mayor exceso.
    /// 3. Si no hay ninguno en todo el recorrido, la L8 — sin puesto
    ///    concreto, solo el destino de respaldo (mismo criterio que A1:
    ///    "la L8 nunca busca la línea más cercana").
    ///
    /// **"Compatibles" NO reutiliza <c>sp_ValidarAsignacion</c> completo**
    /// (a diferencia de <c>sp_SugerirPuesto</c>, E6.7) — su paso 1
    /// ("¿el puesto sigue libre?") rechazaría SIEMPRE a cualquier puesto
    /// fatigado, porque un puesto fatigado está, por definición,
    /// ocupado. El propio ejemplo normativo de §9.4 lo dice literal: "el
    /// sistema los sugiere como destino directo — cada relevado pasa a
    /// RELEVAR a uno de esos 2 compañeros" — el relevado va a un puesto
    /// OCUPADO para relevar a quien está ahí, no a uno libre. Por eso
    /// esta UT compone directamente las reglas de Parte VII que sí
    /// aplican a ese momento: categoría (§4.2), restricción médica
    /// (§7.2, nunca cede), no repetición de 24h (§7.4) y perfil
    /// preferente (§7.3, ordena/filtra aquí, sin escalera de cesión — B4
    /// no describe una). Deliberadamente sin el paso 1 (puesto
    /// libre — inaplicable aquí), el paso 2 (situación del propio
    /// relevado — no cambia entre candidatos, no afecta CUÁL puesto se
    /// elige) ni el paso 7 (ventana de arranque — no aplica a mitad de
    /// turno).
    ///
    /// La guarda de B4 ("el puesto destino no puede estar ya reservado
    /// por otro relevista en tránsito") se verifica aparte, con
    /// <c>Movimiento.puesto_destino_id</c> (E8.5) — ninguna de las
    /// funciones de Parte VII sabe de reservas.
    ///
    /// Deliberadamente **standalone**: no orquesta el "en el mismo
    /// momento en que el supervisor destino confirma la llegada" de
    /// §9.4 p6 — esa integración con la recepción (paso 5) no tiene UT
    /// propia todavía en el plan. Por eso <c>@linea_actual</c> es un
    /// parámetro explícito (no se deriva de <c>Personal</c>), mismo
    /// criterio que <c>@linea_id</c> en <c>sp_SugerirPuesto</c>.
    /// </summary>
    public partial class SugerirDestinoRelevado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_SugerirDestinoRelevado
                    @personal_id INT, @linea_actual TINYINT,
                    @puesto_id_sugerido INT OUTPUT,
                    @linea_sugerida TINYINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @puesto_id_sugerido = NULL; SET @linea_sugerida = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);
                    DECLARE @candidato INT;

                    -- B4 p1 · misma línea primero, mayor exceso relativo.
                    DECLARE cur_misma CURSOR LOCAL FAST_FORWARD FOR
                        SELECT p.Id FROM Puesto p
                         WHERE p.linea_id = @linea_actual AND p.tipo = 'rotativo' AND p.activo = 1
                           AND dbo.fn_NivelFatiga(p.Id) IN ('sugerido', 'critico')
                           AND NOT EXISTS (SELECT 1 FROM Movimiento m WHERE m.puesto_destino_id = p.Id AND m.estado = 'en_transito')
                         ORDER BY dbo.fn_ExcesoRelativoFatiga(p.Id) DESC;

                    OPEN cur_misma;
                    FETCH NEXT FROM cur_misma INTO @candidato;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF dbo.fn_CategoriaCompatible(@personal_id, @candidato) = 1
                           AND dbo.fn_TieneRestriccionBloqueante(@personal_id, @candidato, @hoy) = 0
                           AND dbo.fn_ViolaNoRepeticion24h(@personal_id, @candidato) = 0
                           AND dbo.fn_PerfilIncompatible(@personal_id, @candidato) = 0
                        BEGIN
                            SET @puesto_id_sugerido = @candidato; SET @linea_sugerida = @linea_actual;
                            CLOSE cur_misma; DEALLOCATE cur_misma;
                            RETURN;
                        END
                        FETCH NEXT FROM cur_misma INTO @candidato;
                    END
                    CLOSE cur_misma; DEALLOCATE cur_misma;

                    -- B4 p2 · recorrido de proximidad de A1, línea por línea — nunca prioridad (A9).
                    DECLARE @linea_vecina TINYINT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT linea_destino FROM ProximidadLinea WHERE linea_origen = @linea_actual ORDER BY orden;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @linea_vecina;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        DECLARE cur_vecina CURSOR LOCAL FAST_FORWARD FOR
                            SELECT p.Id FROM Puesto p
                             WHERE p.linea_id = @linea_vecina AND p.tipo = 'rotativo' AND p.activo = 1
                               AND dbo.fn_NivelFatiga(p.Id) IN ('sugerido', 'critico')
                               AND NOT EXISTS (SELECT 1 FROM Movimiento m WHERE m.puesto_destino_id = p.Id AND m.estado = 'en_transito')
                             ORDER BY dbo.fn_ExcesoRelativoFatiga(p.Id) DESC;

                        OPEN cur_vecina;
                        FETCH NEXT FROM cur_vecina INTO @candidato;
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            IF dbo.fn_CategoriaCompatible(@personal_id, @candidato) = 1
                               AND dbo.fn_TieneRestriccionBloqueante(@personal_id, @candidato, @hoy) = 0
                               AND dbo.fn_ViolaNoRepeticion24h(@personal_id, @candidato) = 0
                               AND dbo.fn_PerfilIncompatible(@personal_id, @candidato) = 0
                            BEGIN
                                SET @puesto_id_sugerido = @candidato; SET @linea_sugerida = @linea_vecina;
                                CLOSE cur_vecina; DEALLOCATE cur_vecina;
                                CLOSE cur_lineas; DEALLOCATE cur_lineas;
                                RETURN;
                            END
                            FETCH NEXT FROM cur_vecina INTO @candidato;
                        END
                        CLOSE cur_vecina; DEALLOCATE cur_vecina;

                        FETCH NEXT FROM cur_lineas INTO @linea_vecina;
                    END
                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                    -- B4 p3 · nada en todo el recorrido → L8, sin puesto concreto.
                    SET @linea_sugerida = (SELECT Id FROM Linea WHERE es_bolson = 1);
                    SET @puesto_id_sugerido = NULL;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_SugerirDestinoRelevado;");
        }
    }
}

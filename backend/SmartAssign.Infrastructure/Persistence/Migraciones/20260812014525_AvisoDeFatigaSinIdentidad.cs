using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E9.3 (docs/PROGRESO.md): <c>fn_TextoAvisoFatiga</c> — el
    /// contenido exacto del aviso de fatiga a todos los supervisores,
    /// literal 00 §D2: <c>"L4 · Puesto 3 — relevo sugerido · 62 min"</c>,
    /// "ninguna identidad de persona, ni en el aviso ni al abrirlo".
    ///
    /// Solo el CONTENIDO se construye aquí — la entrega real (tabla
    /// <c>Notificacion</c>, 04 §10/D5: <c>entregada_en</c>/
    /// <c>acusada_en</c>/<c>escalada_en</c>, FCM, acuse, escalado) es la
    /// etapa E12 completa, deliberadamente fuera de esta UT — su propio
    /// LEE (00 §D2, §2.2) no cita 04 §10. Por eso esta pieza es una
    /// función escalar, no una tabla ni un job: la misma composición que
    /// E12 llamará cuando exista, mismo criterio que
    /// <c>fn_PrioridadRelevo</c> en E9.2 (pieza pequeña y probada,
    /// reusada por quien construya después la capa de entrega).
    ///
    /// Usa <c>fn_MinutosEnPuesto</c> (E7.1, el reloj CRUDO), no
    /// <c>fn_MinutosEnPuestoEfectivos</c> (E7.4): el propio ejemplo
    /// normativo de este documento — "62 min" — es EXACTAMENTE el que
    /// motivó separar ambas funciones en E7.4 (01_PRD.md HU-D2 exige el
    /// minuto literal en los avisos, nunca ajustado por el factor de
    /// doble turno).
    ///
    /// §2.2 (aislamiento total entre supervisores) es la razón de ser de
    /// D2, no algo que esta función deba resolver: el aviso deliberadamente
    /// no lleva nada de <c>Personal</c> — línea, puesto y nivel bastan
    /// ("la conciencia de situación necesita el lugar, no la persona").
    /// Alcance solo fatiga (sugerido/crítico) — <c>NULL</c> para
    /// "normal" o para el <c>nivel='maxima'</c> de vacante crítica
    /// (C15-N1, fuera de "aviso DE FATIGA").
    /// </summary>
    public partial class AvisoDeFatigaSinIdentidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER FUNCTION dbo.fn_TextoAvisoFatiga (@puesto_id INT)
                RETURNS NVARCHAR(120) AS
                BEGIN
                    DECLARE @nivel VARCHAR(12) = dbo.fn_NivelFatiga(@puesto_id);

                    IF @nivel NOT IN ('sugerido', 'critico')
                        RETURN NULL;

                    DECLARE @minutos INT = dbo.fn_MinutosEnPuesto(@puesto_id);
                    DECLARE @codigo_linea VARCHAR(5);
                    DECLARE @nombre_puesto NVARCHAR(120);

                    SELECT @codigo_linea = l.codigo, @nombre_puesto = p.nombre_puesto
                      FROM Puesto p JOIN Linea l ON l.Id = p.linea_id
                     WHERE p.Id = @puesto_id;

                    DECLARE @texto_nivel NVARCHAR(20) = CASE @nivel
                        WHEN 'sugerido' THEN N'relevo sugerido'
                        WHEN 'critico'  THEN N'relevo crítico'
                    END;

                    RETURN @codigo_linea + N' · ' + @nombre_puesto + N' — ' + @texto_nivel
                         + N' · ' + CAST(@minutos AS NVARCHAR(10)) + N' min';
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_TextoAvisoFatiga;");
        }
    }
}

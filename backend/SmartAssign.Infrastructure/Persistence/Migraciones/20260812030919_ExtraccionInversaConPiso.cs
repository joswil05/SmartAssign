using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E10.3 (docs/PROGRESO.md): <c>sp_ExtraccionInversa</c> — 00 §B5,
    /// el piso de seguridad, combinado con lo que ya existía (E10.1:
    /// orden derivado; E10.2: el disparador ya es
    /// <c>SIN_CANDIDATOS_EN_BOLSON</c> de <c>sp_ProponerRelevista</c>).
    /// El catálogo de 04 §7.4 describe el procedimiento como "orden
    /// derivado + piso de seguridad" en una sola línea — por eso se
    /// construye completo aquí, no antes.
    ///
    /// <c>CK_Mov_motivo</c> (E8.1) se altera para admitir
    /// <c>'extraccion_inversa'</c> — no estaba en la lista original
    /// porque en E8.1 la extracción inversa (E10) todavía no existía.
    /// Primera vez en el proyecto que se altera un CHECK existente en
    /// vez de escribirlo completo desde el principio (el propio motivo
    /// por el que E9.1/E9.2 sí escribieron valores como <c>'maxima'</c>/
    /// <c>'vacante_critica'</c> por adelantado — aquí no había forma de
    /// saberlo en E8.1).
    ///
    /// **No reutiliza <c>sp_DespacharPersona</c>.** Parte X paso 1 exige
    /// literalmente que el despachado esté "disponible (en Bolsón o sin
    /// asignar)" — un candidato de extracción inversa está
    /// <c>asignado</c>, trabajando de verdad. Debilitar esa condición en
    /// el despacho normal para acomodar la emergencia sería un riesgo
    /// real para el flujo ordinario; 00 §B12 ya trata la extracción
    /// inversa como uno de "tres mecanismos distintos" con su propio
    /// servicio. Por eso este procedimiento cierra la <c>Asignacion</c>
    /// vigente del candidato e inserta el <c>Movimiento</c> directamente
    /// — mismos campos y mismo bloqueo determinista (puesto antes que
    /// persona, 04 §7.3) que <c>sp_DespacharPersona</c>, sin reutilizar
    /// su código porque las precondiciones de entrada son distintas.
    ///
    /// Candidato dentro de la línea donante: ocupante actual de un
    /// puesto ROTATIVO (B5: "los puestos fijos no cuentan: no se
    /// extraen nunca"), compatible con el puesto solicitante (§4.2),
    /// sin restricción médica bloqueante (§7.2) ni violación de
    /// no-repetición-24h (§7.4) contra ese mismo puesto — Parte VII se
    /// aplica siempre. Sin chequeo de perfil preferente: B12 confirma
    /// que nunca es más que una preferencia que ordena, nunca bloquea,
    /// y esta es una emergencia. Ficha ascendente como desempate
    /// determinista — mismo criterio que B2 (E9.5): ningún otro
    /// criterio está especificado, y la misma situación debe producir
    /// siempre la misma extracción.
    ///
    /// Piso: <c>Linea.MinimoOperarios</c> (nulable) con caída a
    /// <c>Parametro['minimo_operarios_default']</c> (04 §9: "a
    /// definir") — sin ninguno de los dos configurado, la línea nunca
    /// es inmune (regla no aplica todavía, R2). Cuenta ocupantes de
    /// rotativos activos, nunca fijos. Si NINGUNA línea del recorrido
    /// tiene margen sobre su piso (o no hay ningún candidato compatible
    /// en las que sí lo tienen), el mensaje es literal de §9.6:
    /// "Capacidad crítica de planta agotada. Requiere intervención
    /// humana."
    ///
    /// Deliberadamente **sin** la vía de forzar por debajo del piso con
    /// justificación (B5, A6) — llega en E10.5, cuando exista
    /// <c>JustificacionExcepcion</c> conectada a esta familia de UTs;
    /// mismo criterio de extensión incremental (<c>CREATE OR ALTER</c>)
    /// ya usado en toda la sesión.
    /// </summary>
    public partial class ExtraccionInversaConPiso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE dbo.Movimiento DROP CONSTRAINT CK_Mov_motivo;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE dbo.Movimiento ADD CONSTRAINT CK_Mov_motivo CHECK (motivo IN (
                    'relevo','reasignacion_relevado','liberacion_bolson','paro',
                    'cambio_sku','linea_inactiva','rechazo_recepcion',
                    'intervencion_coordinador','cobertura_vacante_critica','extraccion_inversa'));
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_ExtraccionInversa
                    @puesto_id_solicitante INT, @usuario_id INT,
                    @candidato_id INT OUTPUT,
                    @linea_origen TINYINT OUTPUT,
                    @movimiento_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @candidato_id = NULL; SET @linea_origen = NULL; SET @movimiento_id = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @linea_solicitante TINYINT = (SELECT linea_id FROM Puesto WHERE Id = @puesto_id_solicitante);
                    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);

                    -- E10.2: solo con la L8 completamente vacía — mismo chequeo que sp_ProponerRelevista ya hace.
                    DECLARE @sub_candidato INT, @sub_cede BIT, @sub_codigo VARCHAR(40), @sub_mensaje NVARCHAR(400);
                    EXEC dbo.sp_ProponerRelevista @puesto_id_solicitante,
                         @candidato_id = @sub_candidato OUTPUT, @cede_perfil = @sub_cede OUTPUT,
                         @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;

                    IF @sub_candidato IS NOT NULL
                    BEGIN
                        SET @codigo_rechazo = 'L8_TIENE_CANDIDATOS_DISPONIBLES';
                        SET @mensaje = 'Todavía hay personal disponible en el Bolsón — no corresponde extracción inversa.';
                        RETURN;
                    END

                    DECLARE @minimo_default INT = (SELECT TRY_CAST(valor AS INT) FROM Parametro WHERE clave = 'minimo_operarios_default');

                    -- E10.1: orden derivado, línea por línea.
                    DECLARE @linea_donante TINYINT;
                    DECLARE cur_lineas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT linea_id FROM dbo.fn_OrdenExtraccionInversa(@linea_solicitante) ORDER BY orden DESC;

                    OPEN cur_lineas;
                    FETCH NEXT FROM cur_lineas INTO @linea_donante;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        DECLARE @minimo INT = COALESCE(
                            (SELECT minimo_operarios FROM Linea WHERE Id = @linea_donante), @minimo_default);
                        DECLARE @ocupantes INT = (
                            SELECT COUNT(*) FROM Asignacion a JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_donante AND p.tipo = 'rotativo' AND a.fin IS NULL);

                        -- B5: al alcanzar el mínimo, la línea queda inmune.
                        -- @minimo NULL = sin piso configurado, la regla no aplica todavía (R2).
                        IF @minimo IS NULL OR @ocupantes > @minimo
                        BEGIN
                            SELECT TOP (1) @candidato_id = per.Id
                              FROM Asignacion a
                              JOIN Personal per ON per.Id = a.personal_id
                              JOIN Puesto p ON p.Id = a.puesto_id
                             WHERE p.linea_id = @linea_donante AND p.tipo = 'rotativo' AND a.fin IS NULL
                               AND dbo.fn_CategoriaCompatible(per.Id, @puesto_id_solicitante) = 1
                               AND dbo.fn_TieneRestriccionBloqueante(per.Id, @puesto_id_solicitante, @hoy) = 0
                               AND dbo.fn_ViolaNoRepeticion24h(per.Id, @puesto_id_solicitante) = 0
                             ORDER BY per.Ficha ASC;

                            IF @candidato_id IS NOT NULL
                            BEGIN
                                SET @linea_origen = @linea_donante;
                                CLOSE cur_lineas; DEALLOCATE cur_lineas;

                                BEGIN TRAN;
                                    -- Puesto antes que persona (04 §7.3), mismo orden que toda la familia.
                                    SELECT 1 FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id_solicitante;
                                    SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @candidato_id;

                                    UPDATE Asignacion SET fin = SYSUTCDATETIME()
                                     WHERE personal_id = @candidato_id AND fin IS NULL;

                                    INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
                                    VALUES (@candidato_id, @linea_origen, @linea_solicitante, @puesto_id_solicitante, 'extraccion_inversa', @usuario_id);
                                    SET @movimiento_id = SCOPE_IDENTITY();

                                    UPDATE Personal SET situacion = 'en_transito' WHERE Id = @candidato_id;
                                COMMIT;
                                RETURN;
                            END
                        END

                        FETCH NEXT FROM cur_lineas INTO @linea_donante;
                    END
                    CLOSE cur_lineas; DEALLOCATE cur_lineas;

                    -- §9.6, literal: nada en todo el recorrido — ninguna línea con margen o candidato.
                    SET @codigo_rechazo = 'CAPACIDAD_CRITICA_DE_PLANTA_AGOTADA';
                    SET @mensaje = 'Capacidad crítica de planta agotada. Requiere intervención humana.';
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ExtraccionInversa;");

            migrationBuilder.Sql("ALTER TABLE dbo.Movimiento DROP CONSTRAINT CK_Mov_motivo;");
            migrationBuilder.Sql("""
                ALTER TABLE dbo.Movimiento ADD CONSTRAINT CK_Mov_motivo CHECK (motivo IN (
                    'relevo','reasignacion_relevado','liberacion_bolson','paro',
                    'cambio_sku','linea_inactiva','rechazo_recepcion',
                    'intervencion_coordinador','cobertura_vacante_critica'));
                """);
        }
    }
}

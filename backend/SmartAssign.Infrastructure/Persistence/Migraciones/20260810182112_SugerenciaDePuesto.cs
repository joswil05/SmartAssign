using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E6.7 (docs/PROGRESO.md): <c>sp_SugerirPuesto</c> — la escalera
    /// de 4 niveles de la fuente §8.5, cuando el supervisor registra a
    /// alguien sin haber elegido antes un puesto (Etapa 4, §8.4).
    ///
    /// Orquesta piezas que E4/E3 ya construyeron sin reimplementar
    /// ninguna regla: <c>sp_ValidarAsignacion</c> (E4.6) ya trae
    /// <c>@permitir_ceder_perfil</c>, exactamente lo que los niveles 2 y
    /// 4 necesitan ("cumpliendo todo salvo el perfil preferente"); y
    /// <c>Puesto.TitularId</c>/<c>SexoPreferente</c> ya documentaban
    /// desde E3 su papel en esta escalera (ver los comentarios de
    /// <c>Puesto.cs</c>). Las restricciones médicas nunca ceden en
    /// ningún nivel — se cumple gratis porque <c>sp_ValidarAsignacion</c>
    /// nunca tiene un parámetro para saltar ese paso.
    ///
    /// Alcance solo <c>tipo='rotativo'</c>: los fijos ya quedaron
    /// resueltos por el barrido automático de §8.3/sp_BarridoPuestosFijos
    /// (E5.5) antes de que exista esta etapa manual — §8.5 vive dentro
    /// de "Etapa 4 — Llenado de puestos ROTATIVOS" (§8.4).
    ///
    /// Orden de "cualquier puesto libre compatible" (niveles 3/4): por
    /// <c>codigo</c> — la fuente no especifica un orden porque cualquier
    /// puesto compatible es igual de válido; se necesita uno determinista
    /// para que la respuesta sea reproducible, y es el mismo criterio que
    /// ya usa la malla (03 §4.3, E6.4). No es un valor de negocio, es una
    /// decisión de ingeniería.
    ///
    /// <c>SIN_PUESTOS_LIBRES</c> es un código nuevo, mismo vocabulario que
    /// los siete de <c>sp_ValidarAsignacion</c>, para el caso que ninguno
    /// de ellos cubre: no había ni un solo puesto rotativo libre que
    /// intentar. La fuente exige "decir cuál regla lo impidió" siempre
    /// que no hay sugerencia — nunca silencio.
    /// </summary>
    public partial class SugerenciaDePuesto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_SugerirPuesto
                    @personal_id INT, @linea_id TINYINT, @usuario_id INT,
                    @puesto_id_sugerido INT OUTPUT,
                    @nivel TINYINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @puesto_id_sugerido = NULL;
                    SET @nivel = NULL;
                    SET @codigo_rechazo = NULL;
                    SET @mensaje = NULL;

                    DECLARE @puesto_titular INT;
                    DECLARE @sub_codigo VARCHAR(40);
                    DECLARE @sub_mensaje NVARCHAR(400);
                    DECLARE @ultimo_codigo VARCHAR(40) = 'SIN_PUESTOS_LIBRES';
                    DECLARE @ultimo_mensaje NVARCHAR(400) = 'No hay puestos rotativos libres en esta línea.';

                    -- Nivel 1/2 · el puesto rotativo libre cuyo titular
                    -- (preferencia, no barrido — ese campo es solo para
                    -- fijos en C12) sea esta misma persona (§8.5 N1/N2).
                    SELECT TOP (1) @puesto_titular = p.Id
                      FROM Puesto p
                     WHERE p.linea_id = @linea_id AND p.tipo = 'rotativo' AND p.activo = 1
                       AND p.titular_id = @personal_id
                       AND NOT EXISTS (SELECT 1 FROM Asignacion a WHERE a.puesto_id = p.Id AND a.fin IS NULL)
                     ORDER BY p.codigo;

                    IF @puesto_titular IS NOT NULL
                    BEGIN
                        EXEC dbo.sp_ValidarAsignacion @personal_id, @puesto_titular, @usuario_id,
                             @permitir_ceder_perfil = 0,
                             @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;
                        IF @sub_codigo IS NULL
                        BEGIN
                            SET @puesto_id_sugerido = @puesto_titular; SET @nivel = 1; RETURN;
                        END
                        SET @ultimo_codigo = @sub_codigo; SET @ultimo_mensaje = @sub_mensaje;

                        EXEC dbo.sp_ValidarAsignacion @personal_id, @puesto_titular, @usuario_id,
                             @permitir_ceder_perfil = 1,
                             @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;
                        IF @sub_codigo IS NULL
                        BEGIN
                            SET @puesto_id_sugerido = @puesto_titular; SET @nivel = 2; RETURN;
                        END
                        SET @ultimo_codigo = @sub_codigo; SET @ultimo_mensaje = @sub_mensaje;
                    END

                    -- Nivel 3 · cualquier otro puesto rotativo libre y
                    -- compatible de la línea, todas las reglas (§8.5 N3).
                    DECLARE candidatos CURSOR LOCAL FAST_FORWARD FOR
                        SELECT p.Id FROM Puesto p
                         WHERE p.linea_id = @linea_id AND p.tipo = 'rotativo' AND p.activo = 1
                           AND NOT EXISTS (SELECT 1 FROM Asignacion a WHERE a.puesto_id = p.Id AND a.fin IS NULL)
                         ORDER BY p.codigo;

                    DECLARE @candidato INT;
                    OPEN candidatos;
                    FETCH NEXT FROM candidatos INTO @candidato;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        EXEC dbo.sp_ValidarAsignacion @personal_id, @candidato, @usuario_id,
                             @permitir_ceder_perfil = 0,
                             @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;
                        IF @sub_codigo IS NULL
                        BEGIN
                            SET @puesto_id_sugerido = @candidato; SET @nivel = 3;
                            CLOSE candidatos; DEALLOCATE candidatos; RETURN;
                        END
                        SET @ultimo_codigo = @sub_codigo; SET @ultimo_mensaje = @sub_mensaje;
                        FETCH NEXT FROM candidatos INTO @candidato;
                    END
                    CLOSE candidatos; DEALLOCATE candidatos;

                    -- Nivel 4 · mismos candidatos, cediendo el perfil
                    -- preferente (§8.5 N4) — última red antes de rendirse.
                    DECLARE candidatos2 CURSOR LOCAL FAST_FORWARD FOR
                        SELECT p.Id FROM Puesto p
                         WHERE p.linea_id = @linea_id AND p.tipo = 'rotativo' AND p.activo = 1
                           AND NOT EXISTS (SELECT 1 FROM Asignacion a WHERE a.puesto_id = p.Id AND a.fin IS NULL)
                         ORDER BY p.codigo;

                    OPEN candidatos2;
                    FETCH NEXT FROM candidatos2 INTO @candidato;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        EXEC dbo.sp_ValidarAsignacion @personal_id, @candidato, @usuario_id,
                             @permitir_ceder_perfil = 1,
                             @codigo_rechazo = @sub_codigo OUTPUT, @mensaje = @sub_mensaje OUTPUT;
                        IF @sub_codigo IS NULL
                        BEGIN
                            SET @puesto_id_sugerido = @candidato; SET @nivel = 4;
                            CLOSE candidatos2; DEALLOCATE candidatos2; RETURN;
                        END
                        SET @ultimo_codigo = @sub_codigo; SET @ultimo_mensaje = @sub_mensaje;
                        FETCH NEXT FROM candidatos2 INTO @candidato;
                    END
                    CLOSE candidatos2; DEALLOCATE candidatos2;

                    -- Nada aplicó en ningún nivel — nunca silencio (§8.5: "debe decir cuál regla lo impidió").
                    SET @codigo_rechazo = @ultimo_codigo;
                    SET @mensaje = @ultimo_mensaje;
                END;
                """);

            // Sin GRANT propio: E4.7 ya dio `GRANT EXECUTE ON SCHEMA::dbo
            // TO rol_app`, que en SQL Server cubre también los objetos
            // creados después — este procedimiento incluido.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_SugerirPuesto;");
        }
    }
}

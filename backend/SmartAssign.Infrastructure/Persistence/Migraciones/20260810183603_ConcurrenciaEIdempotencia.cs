using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E6.8 (docs/PROGRESO.md): <c>sp_AsignarPersona</c> — el único
    /// camino de escritura hacia <c>Asignacion</c> (04 §7.5), con el
    /// mecanismo exacto de 04 §7.3: bloqueo determinista **puesto antes
    /// que persona** para no interbloquear a dos supervisores
    /// simultáneos, e idempotencia contra el doble toque (§12.4).
    ///
    /// Adaptado del pseudocódigo de 04 §7.3 en dos puntos, ninguno de
    /// negocio:
    ///   1. <c>sp_RegistrarAuditoria</c> se llama con su firma REAL
    ///      (E2.5, 13 parámetros) — el snippet del documento usa una
    ///      forma abreviada que no coincide con el procedimiento que de
    ///      verdad existe. El <c>@rol</c> que esa firma exige se resuelve
    ///      aquí desde <c>Usuario</c>.
    ///   2. <c>OperacionIdempotente.resultado_json</c> se reemplaza por
    ///      columnas tipadas (<c>exitosa</c>, <c>codigo_rechazo</c>,
    ///      <c>mensaje</c>, <c>asignacion_id</c>) — mismo mecanismo
    ///      (clave → resultado a repetir en un reintento), más simple de
    ///      probar y consumir que parsear JSON en cada llamador.
    ///   3. El rechazo se comunica por parámetros OUTPUT y COMMIT, no por
    ///      <c>ROLLBACK; THROW</c> como el snippet del documento — mismo
    ///      estilo que ya usan <c>sp_ValidarAsignacion</c> (E4.6),
    ///      <c>sp_PlanificarLinea</c>/<c>sp_ConfirmarPlanificacion</c>/
    ///      <c>sp_ArrancarTurno</c> (E5) y <c>sp_SugerirPuesto</c> (E6.7):
    ///      todo procedimiento de este esquema real señala rechazo así,
    ///      nunca con una excepción SQL. Además, permite que el rechazo
    ///      quede cacheado en <c>OperacionIdempotente</c> igual que el
    ///      éxito, para que un reintento nunca vuelva a evaluar nada.
    ///
    /// El rechazo **nominal** de B1 ("[Nombre] acaba de ser registrado en
    /// L4 · Puesto 3 por otro supervisor") solo se construye cuando el
    /// código es <c>PUESTO_OCUPADO</c> — el resto de los siete códigos de
    /// <c>sp_ValidarAsignacion</c> mantienen su mensaje genérico ya
    /// existente (E4.6), que sigue siendo correcto para ellos.
    ///
    /// 00 §B12/E2.5: los rechazos se auditan igual que los éxitos —
    /// "los rechazos se auditan igual que los éxitos, es lo que permite
    /// demostrar que una restricción médica sí impidió lo que debía
    /// impedir". Por eso <c>sp_RegistrarAuditoria</c> se llama en ambas
    /// ramas, dentro de la misma transacción atómica.
    /// </summary>
    public partial class ConcurrenciaEIdempotencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperacionIdempotente",
                columns: table => new
                {
                    clave = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    exitosa = table.Column<bool>(type: "bit", nullable: false),
                    codigo_rechazo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    mensaje = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    asignacion_id = table.Column<long>(type: "bigint", nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionIdempotente", x => x.clave);
                });

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_AsignarPersona
                    @personal_id INT, @puesto_id INT, @usuario_id INT,
                    @jornada_linea_id INT, @origen VARCHAR(25),
                    @idempotency_key UNIQUEIDENTIFIER,
                    @ceder_perfil BIT = 0,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT,
                    @asignacion_id BIGINT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL; SET @asignacion_id = NULL;

                    -- Idempotencia (§12.4: bloqueo contra doble toque) —
                    -- fuera de la transacción de escritura a propósito:
                    -- un reintento nunca debe volver a tomar bloqueos.
                    IF EXISTS (SELECT 1 FROM OperacionIdempotente WHERE clave = @idempotency_key)
                    BEGIN
                        SELECT @codigo_rechazo = codigo_rechazo, @mensaje = mensaje, @asignacion_id = asignacion_id
                          FROM OperacionIdempotente WHERE clave = @idempotency_key;
                        RETURN;
                    END

                    DECLARE @rol VARCHAR(15);
                    SELECT @rol = rol FROM Usuario WHERE Id = @usuario_id;

                    DECLARE @linea_id TINYINT;

                    BEGIN TRAN;
                        -- Bloqueo determinista (B1, 04 §7.3): SIEMPRE
                        -- puesto antes que persona — un orden inconsistente
                        -- entre procedimientos produce interbloqueos bajo
                        -- la carga de arranque, el momento de mayor
                        -- concurrencia de la jornada.
                        SELECT @linea_id = linea_id FROM Puesto WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id;
                        SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

                        EXEC dbo.sp_ValidarAsignacion @personal_id, @puesto_id, @usuario_id,
                             @permitir_ceder_perfil = @ceder_perfil, @es_liderazgo_manual = 0,
                             @codigo_rechazo = @codigo_rechazo OUTPUT, @mensaje = @mensaje OUTPUT;

                        IF @codigo_rechazo IS NOT NULL
                        BEGIN
                            -- B1: rechazo NOMINAL cuando alguien más ganó —
                            -- "quién y dónde", nunca un mensaje genérico.
                            IF @codigo_rechazo = 'PUESTO_OCUPADO'
                            BEGIN
                                DECLARE @nombre_ganador NVARCHAR(120), @codigo_linea VARCHAR(5), @nombre_puesto NVARCHAR(120);
                                SELECT TOP (1) @nombre_ganador = per.nombre_completo
                                  FROM Asignacion a JOIN Personal per ON per.Id = a.personal_id
                                 WHERE a.puesto_id = @puesto_id AND a.fin IS NULL
                                 ORDER BY a.Id DESC;
                                SELECT @codigo_linea = l.codigo, @nombre_puesto = p.nombre_puesto
                                  FROM Puesto p JOIN Linea l ON l.Id = p.linea_id
                                 WHERE p.Id = @puesto_id;
                                IF @nombre_ganador IS NOT NULL
                                    SET @mensaje = @nombre_ganador + N' acaba de ser registrado en ' + @codigo_linea + N' · ' + @nombre_puesto + N' por otro supervisor.';
                            END

                            EXEC dbo.sp_RegistrarAuditoria
                                 @usuario_id = @usuario_id, @rol = @rol, @accion = 'ASIGNAR', @entidad = 'Asignacion',
                                 @entidad_id = NULL, @personal_id = @personal_id, @linea_id = @linea_id,
                                 @resultado = 'RECHAZO', @codigo_rechazo = @codigo_rechazo, @device_id = NULL;

                            INSERT INTO OperacionIdempotente (clave, exitosa, codigo_rechazo, mensaje, asignacion_id)
                            VALUES (@idempotency_key, 0, @codigo_rechazo, @mensaje, NULL);

                            COMMIT;
                            RETURN;
                        END

                        INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id, origen, asignado_por, cede_perfil)
                        VALUES (@jornada_linea_id, @puesto_id, @personal_id, @origen, @usuario_id, @ceder_perfil);
                        SET @asignacion_id = SCOPE_IDENTITY();

                        UPDATE Personal SET situacion = 'asignado' WHERE Id = @personal_id;

                        EXEC dbo.sp_RegistrarAuditoria
                             @usuario_id = @usuario_id, @rol = @rol, @accion = 'ASIGNAR', @entidad = 'Asignacion',
                             @entidad_id = @asignacion_id, @personal_id = @personal_id, @linea_id = @linea_id,
                             @resultado = 'OK', @device_id = NULL;

                        INSERT INTO OperacionIdempotente (clave, exitosa, codigo_rechazo, mensaje, asignacion_id)
                        VALUES (@idempotency_key, 1, NULL, NULL, @asignacion_id);
                    COMMIT;
                END;
                """);

            // Sin GRANT propio: E4.7 ya dio `GRANT EXECUTE ON SCHEMA::dbo
            // TO rol_app` (cubre objetos creados después). `INSERT` sobre
            // `OperacionIdempotente` no está en la lista DENY de 04 §7.5
            // (solo Asignacion/Auditoria/Movimiento/RestriccionMedica) —
            // rol_app puede escribirla libremente porque no es una tabla
            // de negocio, es infraestructura del propio bloqueo.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_AsignarPersona;");

            migrationBuilder.DropTable(
                name: "OperacionIdempotente");
        }
    }
}

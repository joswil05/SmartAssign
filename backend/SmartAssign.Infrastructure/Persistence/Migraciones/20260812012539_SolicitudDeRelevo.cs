using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E9.1 (docs/PROGRESO.md): arranca E9 (Motor de relevos).
    /// <c>SolicitudRelevo</c> — 04 §5.3, literal §9.4 paso 1: "Cuando un
    /// puesto rotativo alcanza el nivel de fatiga 'relevo sugerido', el
    /// sistema notifica a todos los supervisores de línea [...] Un
    /// supervisor también puede marcar manualmente un puesto como
    /// 'relevo solicitado' antes de llegar a ese umbral. En ambos casos,
    /// el puesto **no se libera todavía**: el operario sigue produciendo
    /// hasta que llegue su reemplazo." Ninguno de los dos SP de esta UT
    /// toca <c>Asignacion</c> ni <c>Personal.situacion</c> — es
    /// literalmente imposible que lo hagan, ninguno de los dos siquiera
    /// las referencia.
    ///
    /// Solo <c>SolicitudRelevo</c> se crea aquí — <c>RelevoDescartado</c>
    /// (04 §5.3, misma sección) queda deliberadamente fuera, tiene su
    /// propia UT (E9.6, junto a aceptar/rechazar). Tampoco hay `DENY`
    /// sobre esta tabla: 04 §7.5 lista exactamente cuatro tablas
    /// (Asignacion, Movimiento, RestriccionMedica, Auditoria) y
    /// <c>SolicitudRelevo</c> no es una de ellas — no hace falta:
    /// <c>rol_app</c> no tiene ningún GRANT propio de SELECT/INSERT/
    /// UPDATE/DELETE sobre ninguna tabla (solo `GRANT EXECUTE ON
    /// SCHEMA::dbo`, E4.7), así que ya no puede escribir aquí
    /// directamente sin necesidad de un DENY explícito — el
    /// encadenamiento de propiedad (mismo dueño `dbo` en SP y tabla) es
    /// lo que permite a los dos procedimientos de esta UT escribir de
    /// todas formas.
    ///
    /// Dos procedimientos, uno por cada disparador de §9.4 paso 1:
    ///
    /// <c>sp_DetectarFatiga</c> (nombrado así en el catálogo de 04 §7.4,
    /// "recalcula niveles con umbral propio de cada puesto") — barrido
    /// global de todos los rotativos activos cuyo <c>fn_NivelFatiga</c>
    /// (E7.3, ya factor-de-doble-turno-ajustado desde E7.4) sea
    /// "sugerido"/"crítico" y que todavía no tengan una solicitud
    /// abierta (<c>UX_SR_abierta</c>); abre una con
    /// <c>origen='umbral_automatico'</c> por cada uno. Mismo estilo de
    /// job idempotente y sin locking fino que <c>sp_CaducarTransitos</c>
    /// (E8.6) — pensado para correr periódicamente, no en respuesta a
    /// una sola petición de usuario.
    ///
    /// <c>sp_MarcarRelevoSolicitado</c> — el "marcar manualmente" del
    /// mismo párrafo, con bloqueo determinista (mismo patrón que la
    /// familia de Movimiento en E8) porque SÍ es una acción de usuario
    /// concreta. Nivel: si el puesto ya alcanzó "sugerido"/"crítico" de
    /// verdad se usa ese nivel real; si todavía no ("antes de llegar a
    /// ese umbral", literal), se guarda "sugerido" como piso — CK_SR_nivel
    /// no admite "normal", y un marcado manual no puede valer menos
    /// prioridad que el disparo automático que se está anticipando.
    /// Exige puesto rotativo (§9.1: "la fatiga solo aplica a puestos
    /// rotativos") y con ocupante activo — sin nadie al que relevar,
    /// "relevo" no tiene sentido.
    /// </summary>
    public partial class SolicitudDeRelevo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudRelevo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    puesto_id = table.Column<int>(type: "int", nullable: false),
                    jornada_linea_id = table.Column<int>(type: "int", nullable: false),
                    origen = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    nivel = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    exceso_relativo = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    creada_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    resuelta_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    resultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    movimiento_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudRelevo", x => x.Id);
                    table.CheckConstraint("CK_SR_nivel", "nivel IN ('sugerido','critico','maxima')");
                    table.CheckConstraint("CK_SR_origen", "origen IN ('umbral_automatico','manual_supervisor','vacante_critica')");
                    table.CheckConstraint("CK_SR_resultado", "resultado IS NULL OR resultado IN ('cubierta','cancelada','cierre_turno')");
                    table.ForeignKey(
                        name: "FK_SolicitudRelevo_JornadaLinea_jornada_linea_id",
                        column: x => x.jornada_linea_id,
                        principalTable: "JornadaLinea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudRelevo_Movimiento_movimiento_id",
                        column: x => x.movimiento_id,
                        principalTable: "Movimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudRelevo_Puesto_puesto_id",
                        column: x => x.puesto_id,
                        principalTable: "Puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudRelevo_jornada_linea_id",
                table: "SolicitudRelevo",
                column: "jornada_linea_id");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudRelevo_movimiento_id",
                table: "SolicitudRelevo",
                column: "movimiento_id");

            migrationBuilder.CreateIndex(
                name: "UX_SR_abierta",
                table: "SolicitudRelevo",
                column: "puesto_id",
                unique: true,
                filter: "[resuelta_en] IS NULL");

            // ── sp_DetectarFatiga (04 §7.4, §9.1/A4) ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_DetectarFatiga
                    @abiertas INT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @abiertas = 0;

                    DECLARE @puesto_id INT, @linea_id TINYINT, @nivel VARCHAR(12), @exceso DECIMAL(6,2);
                    DECLARE @jornada_linea_id INT;

                    DECLARE cur_fatigados CURSOR LOCAL FAST_FORWARD FOR
                        SELECT p.Id, p.linea_id, dbo.fn_NivelFatiga(p.Id), dbo.fn_ExcesoRelativoFatiga(p.Id)
                          FROM Puesto p
                         WHERE p.tipo = 'rotativo' AND p.activo = 1
                           AND dbo.fn_NivelFatiga(p.Id) IN ('sugerido', 'critico')
                           AND NOT EXISTS (
                               SELECT 1 FROM SolicitudRelevo sr
                                WHERE sr.puesto_id = p.Id AND sr.resuelta_en IS NULL);

                    OPEN cur_fatigados;
                    FETCH NEXT FROM cur_fatigados INTO @puesto_id, @linea_id, @nivel, @exceso;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        SELECT TOP (1) @jornada_linea_id = Id FROM JornadaLinea
                         WHERE linea_id = @linea_id AND cerrado_en IS NULL
                         ORDER BY Id DESC;

                        IF @jornada_linea_id IS NOT NULL
                        BEGIN
                            INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel, exceso_relativo)
                            VALUES (@puesto_id, @jornada_linea_id, 'umbral_automatico', @nivel, @exceso);
                            SET @abiertas += 1;
                        END

                        SET @jornada_linea_id = NULL;
                        FETCH NEXT FROM cur_fatigados INTO @puesto_id, @linea_id, @nivel, @exceso;
                    END

                    CLOSE cur_fatigados;
                    DEALLOCATE cur_fatigados;
                END;
                """);

            // ── sp_MarcarRelevoSolicitado (§9.4 p1, "marcar manualmente") ──
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_MarcarRelevoSolicitado
                    @puesto_id INT, @usuario_id INT,
                    @solicitud_id BIGINT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @solicitud_id = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    DECLARE @tipo VARCHAR(10);
                    DECLARE @linea_id TINYINT;

                    BEGIN TRAN;
                        SELECT @tipo = tipo, @linea_id = linea_id
                          FROM Puesto WITH (UPDLOCK, HOLDLOCK)
                         WHERE Id = @puesto_id;

                        IF @tipo IS NULL
                        BEGIN
                            SET @codigo_rechazo = 'PUESTO_INEXISTENTE';
                            SET @mensaje = 'Ese puesto no existe.';
                            COMMIT;
                            RETURN;
                        END

                        IF @tipo <> 'rotativo'
                        BEGIN
                            SET @codigo_rechazo = 'PUESTO_NO_ROTATIVO';
                            SET @mensaje = 'Solo los puestos rotativos entran en el motor de relevos.';
                            COMMIT;
                            RETURN;
                        END

                        IF NOT EXISTS (SELECT 1 FROM Asignacion WHERE puesto_id = @puesto_id AND fin IS NULL)
                        BEGIN
                            SET @codigo_rechazo = 'PUESTO_SIN_OCUPANTE';
                            SET @mensaje = 'Este puesto no tiene a nadie ocupándolo todavía.';
                            COMMIT;
                            RETURN;
                        END

                        IF EXISTS (SELECT 1 FROM SolicitudRelevo WHERE puesto_id = @puesto_id AND resuelta_en IS NULL)
                        BEGIN
                            SET @codigo_rechazo = 'RELEVO_YA_SOLICITADO';
                            SET @mensaje = 'Este puesto ya tiene un relevo pendiente.';
                            COMMIT;
                            RETURN;
                        END

                        DECLARE @jornada_linea_id INT;
                        SELECT TOP (1) @jornada_linea_id = Id FROM JornadaLinea
                         WHERE linea_id = @linea_id AND cerrado_en IS NULL
                         ORDER BY Id DESC;

                        IF @jornada_linea_id IS NULL
                        BEGIN
                            SET @codigo_rechazo = 'SIN_JORNADA_ABIERTA';
                            SET @mensaje = 'Esta línea no tiene una jornada abierta.';
                            COMMIT;
                            RETURN;
                        END

                        DECLARE @nivel_real VARCHAR(12) = dbo.fn_NivelFatiga(@puesto_id);
                        DECLARE @nivel VARCHAR(12) = CASE WHEN @nivel_real IN ('sugerido', 'critico') THEN @nivel_real ELSE 'sugerido' END;
                        DECLARE @exceso DECIMAL(6,2) = dbo.fn_ExcesoRelativoFatiga(@puesto_id);

                        INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel, exceso_relativo)
                        VALUES (@puesto_id, @jornada_linea_id, 'manual_supervisor', @nivel, @exceso);
                        SET @solicitud_id = SCOPE_IDENTITY();
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_MarcarRelevoSolicitado;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_DetectarFatiga;");

            migrationBuilder.DropTable(
                name: "SolicitudRelevo");
        }
    }
}

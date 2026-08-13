using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E14.6 (docs/PROGRESO.md): "Distribución del APK + verificación
    /// de versión" — 00 §F3, 04 §10.1. Esquema literal de §10.1: el APK
    /// vive en el propio servidor de planta, servido por la misma API —
    /// nunca Play Store, Firebase App Distribution ni MDM (00 §F3, E2 ya
    /// cerrado). `ruta_apk` es un dato de despliegue (dónde puso el
    /// archivo quien publicó la versión) — subir el archivo en sí es un
    /// paso operativo fuera de esta app, ninguna fuente pide un endpoint
    /// de carga; esta UT registra metadatos y sirve lo que ya está en
    /// disco, no construye un mecanismo de subida que nadie pidió (R2).
    ///
    /// `sp_PublicarVersionApp` (nuevo): valida `version_codigo` único,
    /// desmarca la fila vigente anterior y marca la nueva en una sola
    /// transacción — el índice único filtrado `UX_VersionApp_vigente` ya
    /// garantiza que nunca conviven dos, esto solo hace la transición
    /// atómica. Sin parámetro de usuario: el esquema de §10.1 no trae
    /// columna `publicado_por` — no se inventa una que la fuente no pide.
    /// §12.7 (auditoría) es "toda operación que MUEVA A UNA PERSONA" —
    /// publicar una versión no mueve a nadie, mismo criterio ya usado
    /// por `sp_PlanificarLinea`/`sp_ArrancarTurno`, que tampoco auditan.
    /// </summary>
    public partial class DistribucionDelApkYVersionVigente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VersionApp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    version_nombre = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    version_codigo = table.Column<int>(type: "int", nullable: false),
                    ruta_apk = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    version_minima_api = table.Column<int>(type: "int", nullable: false),
                    notas = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    publicada_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    vigente = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionApp", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_VersionApp_codigo",
                table: "VersionApp",
                column: "version_codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_VersionApp_vigente",
                table: "VersionApp",
                column: "vigente",
                unique: true,
                filter: "[vigente] = 1");

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_PublicarVersionApp
                    @version_nombre VARCHAR(20), @version_codigo INT, @ruta_apk NVARCHAR(300),
                    @version_minima_api INT, @notas NVARCHAR(600) = NULL,
                    @version_app_id INT OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @version_app_id = NULL; SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF EXISTS (SELECT 1 FROM VersionApp WHERE version_codigo = @version_codigo)
                    BEGIN
                        SET @codigo_rechazo = 'VERSION_CODIGO_YA_EXISTE';
                        SET @mensaje = 'Ya existe una versión publicada con ese código.';
                        RETURN;
                    END

                    BEGIN TRAN;
                        -- UX_VersionApp_vigente (índice único filtrado) exige
                        -- que la anterior quede desmarcada ANTES de insertar
                        -- la nueva, en la misma transacción — nunca conviven
                        -- dos, ni siquiera brevemente.
                        UPDATE VersionApp SET vigente = 0 WHERE vigente = 1;

                        INSERT INTO VersionApp (version_nombre, version_codigo, ruta_apk, version_minima_api, notas, vigente)
                        VALUES (@version_nombre, @version_codigo, @ruta_apk, @version_minima_api, @notas, 1);
                        SET @version_app_id = SCOPE_IDENTITY();
                    COMMIT;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_PublicarVersionApp;");

            migrationBuilder.DropTable(
                name: "VersionApp");
        }
    }
}

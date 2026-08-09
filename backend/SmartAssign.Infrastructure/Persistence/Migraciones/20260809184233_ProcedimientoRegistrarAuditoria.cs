using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// dbo.sp_RegistrarAuditoria (04 §7.4, §8, §12.7): único camino de
    /// escritura hacia Auditoria. Se llama tanto en éxito como en rechazo
    /// — "los rechazos también se auditan, no solo los éxitos" (04 §8).
    /// </summary>
    public partial class ProcedimientoRegistrarAuditoria : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_RegistrarAuditoria
                    @usuario_id INT,
                    @rol VARCHAR(15),
                    @accion VARCHAR(40),
                    @entidad VARCHAR(40),
                    @entidad_id BIGINT = NULL,
                    @personal_id INT = NULL,
                    @linea_id TINYINT = NULL,
                    @resultado VARCHAR(20),
                    @codigo_rechazo VARCHAR(40) = NULL,
                    @datos_antes NVARCHAR(MAX) = NULL,
                    @datos_despues NVARCHAR(MAX) = NULL,
                    @justificacion_id BIGINT = NULL,
                    @device_id NVARCHAR(120) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;

                    INSERT INTO Auditoria (
                        usuario_id, rol, accion, entidad, entidad_id, personal_id, linea_id,
                        resultado, codigo_rechazo, datos_antes, datos_despues, justificacion_id, device_id)
                    VALUES (
                        @usuario_id, @rol, @accion, @entidad, @entidad_id, @personal_id, @linea_id,
                        @resultado, @codigo_rechazo, @datos_antes, @datos_despues, @justificacion_id, @device_id);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_RegistrarAuditoria;");
        }
    }
}

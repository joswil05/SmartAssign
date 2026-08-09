using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class OrigenDatoSimulado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origen_dato",
                table: "RestriccionMedica",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "simulado");

            migrationBuilder.AddColumn<string>(
                name: "origen_dato",
                table: "Personal",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "real");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RM_origen_dato",
                table: "RestriccionMedica",
                sql: "origen_dato IN ('real','simulado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Personal_origen_dato",
                table: "Personal",
                sql: "origen_dato IN ('real','simulado','simulado_categoria')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RM_origen_dato",
                table: "RestriccionMedica");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Personal_origen_dato",
                table: "Personal");

            migrationBuilder.DropColumn(
                name: "origen_dato",
                table: "RestriccionMedica");

            migrationBuilder.DropColumn(
                name: "origen_dato",
                table: "Personal");
        }
    }
}

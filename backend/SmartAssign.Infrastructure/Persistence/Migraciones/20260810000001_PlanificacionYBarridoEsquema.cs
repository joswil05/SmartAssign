using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class PlanificacionYBarridoEsquema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cerrado_forzado_por",
                table: "JornadaLinea",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "dia_operacion",
                table: "JornadaLinea",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "JornadaLinea",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "planificada");

            migrationBuilder.AddColumn<int>(
                name: "sku_id",
                table: "JornadaLinea",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "supervisor_id",
                table: "JornadaLinea",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "turno_id",
                table: "JornadaLinea",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "Turno",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time", nullable: false),
                    cruza_medianoche = table.Column<bool>(type: "bit", nullable: false, computedColumnSql: "CASE WHEN hora_fin <= hora_inicio THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END", stored: true),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turno", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JornadaLinea_cerrado_forzado_por",
                table: "JornadaLinea",
                column: "cerrado_forzado_por");

            migrationBuilder.CreateIndex(
                name: "IX_JornadaLinea_sku_id",
                table: "JornadaLinea",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "IX_JornadaLinea_supervisor_id",
                table: "JornadaLinea",
                column: "supervisor_id");

            migrationBuilder.CreateIndex(
                name: "IX_JornadaLinea_turno_id",
                table: "JornadaLinea",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Jornada",
                table: "JornadaLinea",
                columns: new[] { "linea_id", "turno_id", "dia_operacion" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Jornada_estado",
                table: "JornadaLinea",
                sql: "estado IN ('planificada','confirmada','arrancada','cerrada')");

            migrationBuilder.CreateIndex(
                name: "IX_Turno_nombre",
                table: "Turno",
                column: "nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_JornadaLinea_SKU_sku_id",
                table: "JornadaLinea",
                column: "sku_id",
                principalTable: "SKU",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JornadaLinea_Turno_turno_id",
                table: "JornadaLinea",
                column: "turno_id",
                principalTable: "Turno",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JornadaLinea_Usuario_cerrado_forzado_por",
                table: "JornadaLinea",
                column: "cerrado_forzado_por",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JornadaLinea_Usuario_supervisor_id",
                table: "JornadaLinea",
                column: "supervisor_id",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JornadaLinea_SKU_sku_id",
                table: "JornadaLinea");

            migrationBuilder.DropForeignKey(
                name: "FK_JornadaLinea_Turno_turno_id",
                table: "JornadaLinea");

            migrationBuilder.DropForeignKey(
                name: "FK_JornadaLinea_Usuario_cerrado_forzado_por",
                table: "JornadaLinea");

            migrationBuilder.DropForeignKey(
                name: "FK_JornadaLinea_Usuario_supervisor_id",
                table: "JornadaLinea");

            migrationBuilder.DropTable(
                name: "Turno");

            migrationBuilder.DropIndex(
                name: "IX_JornadaLinea_cerrado_forzado_por",
                table: "JornadaLinea");

            migrationBuilder.DropIndex(
                name: "IX_JornadaLinea_sku_id",
                table: "JornadaLinea");

            migrationBuilder.DropIndex(
                name: "IX_JornadaLinea_supervisor_id",
                table: "JornadaLinea");

            migrationBuilder.DropIndex(
                name: "IX_JornadaLinea_turno_id",
                table: "JornadaLinea");

            migrationBuilder.DropIndex(
                name: "UQ_Jornada",
                table: "JornadaLinea");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Jornada_estado",
                table: "JornadaLinea");

            migrationBuilder.DropColumn(
                name: "cerrado_forzado_por",
                table: "JornadaLinea");

            migrationBuilder.DropColumn(
                name: "dia_operacion",
                table: "JornadaLinea");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "JornadaLinea");

            migrationBuilder.DropColumn(
                name: "sku_id",
                table: "JornadaLinea");

            migrationBuilder.DropColumn(
                name: "supervisor_id",
                table: "JornadaLinea");

            migrationBuilder.DropColumn(
                name: "turno_id",
                table: "JornadaLinea");
        }
    }
}

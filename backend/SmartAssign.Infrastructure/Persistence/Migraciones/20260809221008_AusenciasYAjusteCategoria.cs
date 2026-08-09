using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class AusenciasYAjusteCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Puesto_categoria",
                table: "Puesto");

            migrationBuilder.CreateTable(
                name: "AusenciaJustificada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    personal_id = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    registrado_por = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AusenciaJustificada", x => x.Id);
                    table.CheckConstraint("CK_Ausencia_tipo", "tipo IN ('vacaciones','permiso','cita_medica','subsidio','accidente_laboral','otro')");
                    table.ForeignKey(
                        name: "FK_AusenciaJustificada_Personal_personal_id",
                        column: x => x.personal_id,
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AusenciaJustificada_Usuario_registrado_por",
                        column: x => x.registrado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Puesto_categoria",
                table: "Puesto",
                sql: "tipo = 'fijo' OR categoria_titular IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AusenciaJustificada_personal_id",
                table: "AusenciaJustificada",
                column: "personal_id");

            migrationBuilder.CreateIndex(
                name: "IX_AusenciaJustificada_registrado_por",
                table: "AusenciaJustificada",
                column: "registrado_por");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AusenciaJustificada");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Puesto_categoria",
                table: "Puesto");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Puesto_categoria",
                table: "Puesto",
                sql: "(tipo = 'fijo' AND categoria_titular IS NOT NULL) OR (tipo = 'rotativo' AND categoria_titular IS NULL)");
        }
    }
}

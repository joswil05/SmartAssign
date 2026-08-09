using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class PersonalYPuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "perfil_requerido",
                table: "Puesto",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "titular_id",
                table: "Puesto",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "umbral_critico_horas",
                table: "Puesto",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Personal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ficha = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre_completo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    sexo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    linea_habitual = table.Column<byte>(type: "tinyint", nullable: true),
                    linea_fisica_actual = table.Column<byte>(type: "tinyint", nullable: true),
                    situacion = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false, defaultValue: "fuera_de_turno"),
                    doble_turno = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personal", x => x.Id);
                    table.CheckConstraint("CK_Personal_categoria", "categoria IN ('operario','operador_a','operador_b','operador_c','averiero','liderazgo')");
                    table.CheckConstraint("CK_Personal_situacion", "situacion IN ('fuera_de_turno','presente_sin_asignar','asignado','en_transito','en_bolson','retirado_temporal','ausente_justificado')");
                    table.ForeignKey(
                        name: "FK_Personal_Linea_linea_fisica_actual",
                        column: x => x.linea_fisica_actual,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personal_Linea_linea_habitual",
                        column: x => x.linea_habitual,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PuestoCapacidad",
                columns: table => new
                {
                    puesto_id = table.Column<int>(type: "int", nullable: false),
                    capacidad_id = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuestoCapacidad", x => new { x.puesto_id, x.capacidad_id });
                    table.ForeignKey(
                        name: "FK_PuestoCapacidad_CapacidadFisica_capacidad_id",
                        column: x => x.capacidad_id,
                        principalTable: "CapacidadFisica",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PuestoCapacidad_Puesto_puesto_id",
                        column: x => x.puesto_id,
                        principalTable: "Puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestriccionMedica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    personal_id = table.Column<int>(type: "int", nullable: false),
                    capacidad_id = table.Column<short>(type: "smallint", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    fuente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    fecha_dictamen = table.Column<DateOnly>(type: "date", nullable: false),
                    observacion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    registrado_por = table.Column<int>(type: "int", nullable: false),
                    registrado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestriccionMedica", x => x.Id);
                    table.CheckConstraint("CK_RM_vigencia", "fecha_fin IS NULL OR fecha_fin >= fecha_inicio");
                    table.ForeignKey(
                        name: "FK_RestriccionMedica_CapacidadFisica_capacidad_id",
                        column: x => x.capacidad_id,
                        principalTable: "CapacidadFisica",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestriccionMedica_Personal_personal_id",
                        column: x => x.personal_id,
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestriccionMedica_Usuario_registrado_por",
                        column: x => x.registrado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Puesto_titular_id",
                table: "Puesto",
                column: "titular_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Puesto_umbrales",
                table: "Puesto",
                sql: "umbral_critico_horas IS NULL OR horas_en_puesto IS NULL OR umbral_critico_horas > horas_en_puesto");

            migrationBuilder.CreateIndex(
                name: "IX_Personal_disponible",
                table: "Personal",
                columns: new[] { "situacion", "linea_fisica_actual" },
                filter: "[activo] = 1")
                .Annotation("SqlServer:Include", new[] { "ficha", "nombre_completo", "categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_Personal_ficha",
                table: "Personal",
                column: "ficha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personal_linea_fisica_actual",
                table: "Personal",
                column: "linea_fisica_actual");

            migrationBuilder.CreateIndex(
                name: "IX_Personal_linea_habitual",
                table: "Personal",
                column: "linea_habitual");

            migrationBuilder.CreateIndex(
                name: "IX_PuestoCapacidad_capacidad_id",
                table: "PuestoCapacidad",
                column: "capacidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_RestriccionMedica_capacidad_id",
                table: "RestriccionMedica",
                column: "capacidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_RestriccionMedica_registrado_por",
                table: "RestriccionMedica",
                column: "registrado_por");

            migrationBuilder.CreateIndex(
                name: "IX_RM_vigentes",
                table: "RestriccionMedica",
                columns: new[] { "personal_id", "capacidad_id" })
                .Annotation("SqlServer:Include", new[] { "fecha_inicio", "fecha_fin" });

            migrationBuilder.AddForeignKey(
                name: "FK_Puesto_Personal_titular_id",
                table: "Puesto",
                column: "titular_id",
                principalTable: "Personal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Puesto_Personal_titular_id",
                table: "Puesto");

            migrationBuilder.DropTable(
                name: "PuestoCapacidad");

            migrationBuilder.DropTable(
                name: "RestriccionMedica");

            migrationBuilder.DropTable(
                name: "Personal");

            migrationBuilder.DropIndex(
                name: "IX_Puesto_titular_id",
                table: "Puesto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Puesto_umbrales",
                table: "Puesto");

            migrationBuilder.DropColumn(
                name: "perfil_requerido",
                table: "Puesto");

            migrationBuilder.DropColumn(
                name: "titular_id",
                table: "Puesto");

            migrationBuilder.DropColumn(
                name: "umbral_critico_horas",
                table: "Puesto");
        }
    }
}

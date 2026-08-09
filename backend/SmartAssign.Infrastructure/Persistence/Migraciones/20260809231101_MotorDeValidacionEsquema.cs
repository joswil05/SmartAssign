using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class MotorDeValidacionEsquema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JornadaLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    linea_id = table.Column<byte>(type: "tinyint", nullable: false),
                    arrancado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ventana_arranque_fin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cerrado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JornadaLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JornadaLinea_Linea_linea_id",
                        column: x => x.linea_id,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JustificacionExcepcion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipo_excepcion = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    motivo_id = table.Column<short>(type: "smallint", nullable: false),
                    texto = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    creada_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JustificacionExcepcion", x => x.Id);
                    table.CheckConstraint("CK_JE_texto", "LEN(LTRIM(RTRIM(texto))) >= 10");
                    table.CheckConstraint("CK_JE_tipo", "tipo_excepcion IN ('movimiento_fuera_de_flujo','saltar_ventana_arranque','forzar_cierre_turno','extraccion_operador_b','forzar_bajo_piso_seguridad','cancelar_transito','asignacion_liderazgo')");
                    table.ForeignKey(
                        name: "FK_JustificacionExcepcion_MotivoExcepcion_motivo_id",
                        column: x => x.motivo_id,
                        principalTable: "MotivoExcepcion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JustificacionExcepcion_Usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Parametro",
                columns: table => new
                {
                    clave = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    valor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    modificado_por = table.Column<int>(type: "int", nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametro", x => x.clave);
                    table.ForeignKey(
                        name: "FK_Parametro_Usuario_modificado_por",
                        column: x => x.modificado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UltimaTareaJornada",
                columns: table => new
                {
                    personal_id = table.Column<int>(type: "int", nullable: false),
                    tipo_actividad_id = table.Column<short>(type: "smallint", nullable: false),
                    puesto_id = table.Column<int>(type: "int", nullable: false),
                    dia_operacion = table.Column<DateOnly>(type: "date", nullable: false),
                    registrado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UltimaTareaJornada", x => x.personal_id);
                    table.ForeignKey(
                        name: "FK_UltimaTareaJornada_Personal_personal_id",
                        column: x => x.personal_id,
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UltimaTareaJornada_Puesto_puesto_id",
                        column: x => x.puesto_id,
                        principalTable: "Puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UltimaTareaJornada_TipoActividad_tipo_actividad_id",
                        column: x => x.tipo_actividad_id,
                        principalTable: "TipoActividad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Asignacion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    jornada_linea_id = table.Column<int>(type: "int", nullable: false),
                    puesto_id = table.Column<int>(type: "int", nullable: false),
                    personal_id = table.Column<int>(type: "int", nullable: false),
                    titular_original_id = table.Column<int>(type: "int", nullable: true),
                    origen = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    inicio = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    fin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    motivo_fin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    asignado_por = table.Column<int>(type: "int", nullable: false),
                    justificacion_id = table.Column<long>(type: "bigint", nullable: true),
                    cede_perfil = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asignacion", x => x.Id);
                    table.CheckConstraint("CK_Asig_fin", "fin IS NULL OR fin >= inicio");
                    table.CheckConstraint("CK_Asig_origen", "origen IN ('barrido_automatico','manual_supervisor','relevo','reasignacion_relevado','extraccion_inversa','intervencion_coordinador','cobertura_vacante_critica')");
                    table.ForeignKey(
                        name: "FK_Asignacion_JornadaLinea_jornada_linea_id",
                        column: x => x.jornada_linea_id,
                        principalTable: "JornadaLinea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignacion_JustificacionExcepcion_justificacion_id",
                        column: x => x.justificacion_id,
                        principalTable: "JustificacionExcepcion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignacion_Personal_personal_id",
                        column: x => x.personal_id,
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignacion_Personal_titular_original_id",
                        column: x => x.titular_original_id,
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignacion_Puesto_puesto_id",
                        column: x => x.puesto_id,
                        principalTable: "Puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignacion_Usuario_asignado_por",
                        column: x => x.asignado_por,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_asignado_por",
                table: "Asignacion",
                column: "asignado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_jornada_linea_id",
                table: "Asignacion",
                column: "jornada_linea_id");

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_justificacion_id",
                table: "Asignacion",
                column: "justificacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_titular_original_id",
                table: "Asignacion",
                column: "titular_original_id");

            migrationBuilder.CreateIndex(
                name: "UX_Asig_personal_activo",
                table: "Asignacion",
                column: "personal_id",
                unique: true,
                filter: "[fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Asig_puesto_activo",
                table: "Asignacion",
                column: "puesto_id",
                unique: true,
                filter: "[fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_JornadaLinea_abierta",
                table: "JornadaLinea",
                column: "linea_id",
                unique: true,
                filter: "[cerrado_en] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JustificacionExcepcion_motivo_id",
                table: "JustificacionExcepcion",
                column: "motivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_JustificacionExcepcion_usuario_id",
                table: "JustificacionExcepcion",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_Parametro_modificado_por",
                table: "Parametro",
                column: "modificado_por");

            migrationBuilder.CreateIndex(
                name: "IX_UltimaTareaJornada_puesto_id",
                table: "UltimaTareaJornada",
                column: "puesto_id");

            migrationBuilder.CreateIndex(
                name: "IX_UltimaTareaJornada_tipo_actividad_id",
                table: "UltimaTareaJornada",
                column: "tipo_actividad_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Asignacion");

            migrationBuilder.DropTable(
                name: "Parametro");

            migrationBuilder.DropTable(
                name: "UltimaTareaJornada");

            migrationBuilder.DropTable(
                name: "JornadaLinea");

            migrationBuilder.DropTable(
                name: "JustificacionExcepcion");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class IdentidadYAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre_completo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    rol = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    origen_identidad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "local"),
                    password_hash = table.Column<byte[]>(type: "varbinary(256)", maxLength: 256, nullable: true),
                    password_salt = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: true),
                    pin_hash = table.Column<byte[]>(type: "varbinary(256)", maxLength: 256, nullable: true),
                    pin_salt = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: true),
                    personal_id = table.Column<int>(type: "int", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    bloqueado_hasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    intentos_fallidos = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                    table.CheckConstraint("CK_Usuario_origen", "origen_identidad IN ('ad','local')");
                    table.CheckConstraint("CK_Usuario_rol", "rol IN ('coordinador','supervisor')");
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    rol = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    accion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    entidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    entidad_id = table.Column<long>(type: "bigint", nullable: true),
                    personal_id = table.Column<int>(type: "int", nullable: true),
                    linea_id = table.Column<byte>(type: "tinyint", nullable: true),
                    resultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    codigo_rechazo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    datos_antes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    datos_despues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    justificacion_id = table.Column<long>(type: "bigint", nullable: true),
                    device_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ocurrido_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                    table.CheckConstraint("CK_Aud_resultado", "resultado IN ('OK','RECHAZO')");
                    table.ForeignKey(
                        name: "FK_Auditoria_Linea_linea_id",
                        column: x => x.linea_id,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auditoria_Usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DispositivoPush",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    push_token = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    plataforma = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "android"),
                    registrado_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    revocado_en = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositivoPush", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispositivoPush_Usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SesionDispositivo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    refresh_token_hash = table.Column<byte[]>(type: "varbinary(256)", maxLength: 256, nullable: false),
                    emitido_en = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    expira_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    revocado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ultima_actividad = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionDispositivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionDispositivo_Usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aud_linea",
                table: "Auditoria",
                columns: new[] { "linea_id", "ocurrido_en" });

            migrationBuilder.CreateIndex(
                name: "IX_Aud_persona",
                table: "Auditoria",
                columns: new[] { "personal_id", "ocurrido_en" });

            migrationBuilder.CreateIndex(
                name: "IX_Aud_rechazo",
                table: "Auditoria",
                columns: new[] { "codigo_rechazo", "ocurrido_en" },
                filter: "[resultado] = 'RECHAZO'");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_usuario_id",
                table: "Auditoria",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_Push_activo",
                table: "DispositivoPush",
                column: "usuario_id",
                filter: "[revocado_en] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_DispositivoPush",
                table: "DispositivoPush",
                column: "device_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sesion_activa",
                table: "SesionDispositivo",
                columns: new[] { "usuario_id", "device_id" },
                filter: "[revocado_en] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_username",
                table: "Usuario",
                column: "username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Linea_Usuario_supervisor_actual",
                table: "Linea",
                column: "supervisor_actual",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Linea_Usuario_supervisor_actual",
                table: "Linea");

            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "DispositivoPush");

            migrationBuilder.DropTable(
                name: "SesionDispositivo");

            migrationBuilder.DropTable(
                name: "Usuario");
        }
    }
}

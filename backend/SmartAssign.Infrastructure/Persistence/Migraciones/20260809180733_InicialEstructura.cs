using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class InicialEstructura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapacidadFisica",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapacidadFisica", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoriaParo",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaParo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Linea",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    es_bolson = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    minimo_operarios = table.Column<short>(type: "smallint", nullable: true),
                    activa_hoy = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    situacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "inactiva"),
                    supervisor_actual = table.Column<int>(type: "int", nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Linea", x => x.Id);
                    table.CheckConstraint("CK_Linea_situacion", "situacion IN ('inactiva','activa','en_arranque','en_produccion','en_paro','en_limpieza')");
                });

            migrationBuilder.CreateTable(
                name: "MotivoExcepcion",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivoExcepcion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotivoRechazoRecepcion",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivoRechazoRecepcion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SKU",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ritmo_teorico_hora = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SKU", x => x.Id);
                    table.CheckConstraint("CK_SKU_ritmo", "ritmo_teorico_hora > 0");
                });

            migrationBuilder.CreateTable(
                name: "TipoActividad",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoActividad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CausaParo",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    categoria_id = table.Column<short>(type: "smallint", nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CausaParo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CausaParo_CategoriaParo_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "CategoriaParo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrioridadLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    linea_id = table.Column<byte>(type: "tinyint", nullable: false),
                    orden = table.Column<byte>(type: "tinyint", nullable: false),
                    vigente_desde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    vigente_hasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cambiado_por = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrioridadLinea", x => x.Id);
                    table.CheckConstraint("CK_Prioridad_orden", "orden BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_PrioridadLinea_Linea_linea_id",
                        column: x => x.linea_id,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProximidadLinea",
                columns: table => new
                {
                    linea_origen = table.Column<byte>(type: "tinyint", nullable: false),
                    orden = table.Column<byte>(type: "tinyint", nullable: false),
                    linea_destino = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProximidadLinea", x => new { x.linea_origen, x.orden });
                    table.CheckConstraint("CK_Proximidad_distinta", "linea_origen <> linea_destino");
                    table.CheckConstraint("CK_Proximidad_orden", "orden BETWEEN 1 AND 9");
                    table.ForeignKey(
                        name: "FK_ProximidadLinea_Linea_linea_destino",
                        column: x => x.linea_destino,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProximidadLinea_Linea_linea_origen",
                        column: x => x.linea_origen,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Puesto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    linea_id = table.Column<byte>(type: "tinyint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre_puesto = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    tipo_actividad_id = table.Column<short>(type: "smallint", nullable: true),
                    categoria_titular = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    sexo_preferente = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    horas_en_puesto = table.Column<short>(type: "smallint", nullable: true),
                    horas_recuperacion = table.Column<short>(type: "smallint", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Puesto", x => x.Id);
                    table.CheckConstraint("CK_Puesto_categoria", "(tipo = 'fijo' AND categoria_titular IS NOT NULL) OR (tipo = 'rotativo' AND categoria_titular IS NULL)");
                    table.CheckConstraint("CK_Puesto_tipo", "tipo IN ('fijo','rotativo')");
                    table.ForeignKey(
                        name: "FK_Puesto_Linea_linea_id",
                        column: x => x.linea_id,
                        principalTable: "Linea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Puesto_TipoActividad_tipo_actividad_id",
                        column: x => x.tipo_actividad_id,
                        principalTable: "TipoActividad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PuestoSKU",
                columns: table => new
                {
                    puesto_id = table.Column<int>(type: "int", nullable: false),
                    sku_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuestoSKU", x => new { x.puesto_id, x.sku_id });
                    table.ForeignKey(
                        name: "FK_PuestoSKU_Puesto_puesto_id",
                        column: x => x.puesto_id,
                        principalTable: "Puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PuestoSKU_SKU_sku_id",
                        column: x => x.sku_id,
                        principalTable: "SKU",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapacidadFisica_codigo",
                table: "CapacidadFisica",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaParo_nombre",
                table: "CategoriaParo",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CausaParo_categoria_id_nombre",
                table: "CausaParo",
                columns: new[] { "categoria_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Linea_codigo",
                table: "Linea",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Linea_bolson",
                table: "Linea",
                column: "es_bolson",
                unique: true,
                filter: "[es_bolson] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Linea_supervisor",
                table: "Linea",
                column: "supervisor_actual",
                unique: true,
                filter: "[supervisor_actual] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MotivoExcepcion_nombre",
                table: "MotivoExcepcion",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MotivoRechazoRecepcion_nombre",
                table: "MotivoRechazoRecepcion",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Prioridad_orden_vigente",
                table: "PrioridadLinea",
                column: "orden",
                unique: true,
                filter: "[vigente_hasta] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Prioridad_vigente",
                table: "PrioridadLinea",
                column: "linea_id",
                unique: true,
                filter: "[vigente_hasta] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProximidadLinea_linea_destino",
                table: "ProximidadLinea",
                column: "linea_destino");

            migrationBuilder.CreateIndex(
                name: "UQ_Proximidad",
                table: "ProximidadLinea",
                columns: new[] { "linea_origen", "linea_destino" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puesto_linea_id_codigo",
                table: "Puesto",
                columns: new[] { "linea_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puesto_tipo_actividad_id",
                table: "Puesto",
                column: "tipo_actividad_id");

            migrationBuilder.CreateIndex(
                name: "IX_PuestoSKU_sku_id",
                table: "PuestoSKU",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "IX_SKU_codigo",
                table: "SKU",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TipoActividad_nombre",
                table: "TipoActividad",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapacidadFisica");

            migrationBuilder.DropTable(
                name: "CausaParo");

            migrationBuilder.DropTable(
                name: "MotivoExcepcion");

            migrationBuilder.DropTable(
                name: "MotivoRechazoRecepcion");

            migrationBuilder.DropTable(
                name: "PrioridadLinea");

            migrationBuilder.DropTable(
                name: "ProximidadLinea");

            migrationBuilder.DropTable(
                name: "PuestoSKU");

            migrationBuilder.DropTable(
                name: "CategoriaParo");

            migrationBuilder.DropTable(
                name: "Puesto");

            migrationBuilder.DropTable(
                name: "SKU");

            migrationBuilder.DropTable(
                name: "Linea");

            migrationBuilder.DropTable(
                name: "TipoActividad");
        }
    }
}

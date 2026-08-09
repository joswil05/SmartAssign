using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class SemillaEstructuralYCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "creado_en",
                table: "Linea",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.InsertData(
                table: "CapacidadFisica",
                columns: new[] { "Id", "activo", "codigo", "nombre" },
                values: new object[,]
                {
                    { (short)1, true, "levantar_carga", "Levantar carga" },
                    { (short)2, true, "bipedestacion_prolongada", "Bipedestación prolongada" },
                    { (short)3, true, "movimiento_repetitivo_mano", "Movimiento repetitivo de mano" },
                    { (short)4, true, "esfuerzo_lumbar", "Esfuerzo lumbar" },
                    { (short)5, true, "exposicion_quimicos", "Exposición a químicos" },
                    { (short)6, true, "trabajo_en_altura", "Trabajo en altura" }
                });

            migrationBuilder.InsertData(
                table: "CategoriaParo",
                columns: new[] { "Id", "activo", "nombre" },
                values: new object[,]
                {
                    { (short)1, true, "Mecánico" },
                    { (short)2, true, "Eléctrico" },
                    { (short)3, true, "Calidad" },
                    { (short)4, true, "Falta de material" }
                });

            migrationBuilder.InsertData(
                table: "Linea",
                columns: new[] { "Id", "codigo", "creado_en", "minimo_operarios", "nombre", "situacion", "supervisor_actual" },
                values: new object[,]
                {
                    { (byte)1, "L1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 1", "inactiva", null },
                    { (byte)2, "L2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 2", "inactiva", null },
                    { (byte)3, "L3", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 3", "inactiva", null },
                    { (byte)4, "L4", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 4", "inactiva", null },
                    { (byte)5, "L5", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 5", "inactiva", null },
                    { (byte)6, "L6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 6", "inactiva", null },
                    { (byte)7, "L7", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 7", "inactiva", null }
                });

            migrationBuilder.InsertData(
                table: "Linea",
                columns: new[] { "Id", "codigo", "creado_en", "es_bolson", "minimo_operarios", "nombre", "situacion", "supervisor_actual" },
                values: new object[] { (byte)8, "L8", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null, "Línea 8", "inactiva", null });

            migrationBuilder.InsertData(
                table: "Linea",
                columns: new[] { "Id", "codigo", "creado_en", "minimo_operarios", "nombre", "situacion", "supervisor_actual" },
                values: new object[,]
                {
                    { (byte)9, "L9", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 9", "inactiva", null },
                    { (byte)10, "L10", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Línea 10", "inactiva", null }
                });

            migrationBuilder.InsertData(
                table: "MotivoExcepcion",
                columns: new[] { "Id", "activo", "nombre" },
                values: new object[,]
                {
                    { (short)1, true, "Acuerdo directo con el trabajador" },
                    { (short)2, true, "Extracción de Operador B por vacante crítica" },
                    { (short)3, true, "Forzar cierre de turno" },
                    { (short)4, true, "Forzar por debajo del piso de seguridad" },
                    { (short)5, true, "Saltar ventana de arranque" },
                    { (short)6, true, "Cancelar tránsito caducado" },
                    { (short)7, true, "Asignación manual de personal de liderazgo" }
                });

            migrationBuilder.InsertData(
                table: "MotivoRechazoRecepcion",
                columns: new[] { "Id", "activo", "nombre" },
                values: new object[,]
                {
                    { (short)1, true, "La persona no se presentó" },
                    { (short)2, true, "Persona incorrecta" },
                    { (short)3, true, "El puesto ya no requiere relevo" },
                    { (short)4, true, "Otro" }
                });

            migrationBuilder.InsertData(
                table: "CausaParo",
                columns: new[] { "Id", "activo", "categoria_id", "nombre" },
                values: new object[,]
                {
                    { (short)1, true, (short)1, "Avería de máquina" },
                    { (short)2, true, (short)1, "Ajuste mecánico" },
                    { (short)3, true, (short)2, "Corte de energía" },
                    { (short)4, true, (short)3, "Producto fuera de especificación" },
                    { (short)5, true, (short)4, "Desabasto de insumo" }
                });

            migrationBuilder.InsertData(
                table: "PrioridadLinea",
                columns: new[] { "Id", "cambiado_por", "linea_id", "orden", "vigente_desde", "vigente_hasta" },
                values: new object[,]
                {
                    { 1, 0, (byte)4, (byte)1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, 0, (byte)1, (byte)2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, 0, (byte)2, (byte)3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 4, 0, (byte)6, (byte)4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 5, 0, (byte)7, (byte)5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 6, 0, (byte)5, (byte)6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 7, 0, (byte)3, (byte)7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 8, 0, (byte)8, (byte)8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 9, 0, (byte)9, (byte)9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 10, 0, (byte)10, (byte)10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.InsertData(
                table: "ProximidadLinea",
                columns: new[] { "linea_origen", "orden", "linea_destino" },
                values: new object[,]
                {
                    { (byte)1, (byte)1, (byte)2 },
                    { (byte)1, (byte)2, (byte)4 },
                    { (byte)1, (byte)3, (byte)9 },
                    { (byte)1, (byte)4, (byte)10 },
                    { (byte)1, (byte)5, (byte)6 },
                    { (byte)1, (byte)6, (byte)3 },
                    { (byte)1, (byte)7, (byte)7 },
                    { (byte)1, (byte)8, (byte)5 },
                    { (byte)1, (byte)9, (byte)8 },
                    { (byte)2, (byte)1, (byte)4 },
                    { (byte)2, (byte)2, (byte)1 },
                    { (byte)2, (byte)3, (byte)7 },
                    { (byte)2, (byte)4, (byte)9 },
                    { (byte)2, (byte)5, (byte)10 },
                    { (byte)2, (byte)6, (byte)3 },
                    { (byte)2, (byte)7, (byte)6 },
                    { (byte)2, (byte)8, (byte)5 },
                    { (byte)2, (byte)9, (byte)8 },
                    { (byte)3, (byte)1, (byte)10 },
                    { (byte)3, (byte)2, (byte)9 },
                    { (byte)3, (byte)3, (byte)6 },
                    { (byte)3, (byte)4, (byte)7 },
                    { (byte)3, (byte)5, (byte)4 },
                    { (byte)3, (byte)6, (byte)2 },
                    { (byte)3, (byte)7, (byte)1 },
                    { (byte)3, (byte)8, (byte)5 },
                    { (byte)3, (byte)9, (byte)8 },
                    { (byte)4, (byte)1, (byte)2 },
                    { (byte)4, (byte)2, (byte)1 },
                    { (byte)4, (byte)3, (byte)7 },
                    { (byte)4, (byte)4, (byte)9 },
                    { (byte)4, (byte)5, (byte)10 },
                    { (byte)4, (byte)6, (byte)6 },
                    { (byte)4, (byte)7, (byte)3 },
                    { (byte)4, (byte)8, (byte)5 },
                    { (byte)4, (byte)9, (byte)8 },
                    { (byte)5, (byte)1, (byte)1 },
                    { (byte)5, (byte)2, (byte)2 },
                    { (byte)5, (byte)3, (byte)4 },
                    { (byte)5, (byte)4, (byte)7 },
                    { (byte)5, (byte)5, (byte)9 },
                    { (byte)5, (byte)6, (byte)10 },
                    { (byte)5, (byte)7, (byte)6 },
                    { (byte)5, (byte)8, (byte)3 },
                    { (byte)5, (byte)9, (byte)8 },
                    { (byte)6, (byte)1, (byte)3 },
                    { (byte)6, (byte)2, (byte)10 },
                    { (byte)6, (byte)3, (byte)9 },
                    { (byte)6, (byte)4, (byte)7 },
                    { (byte)6, (byte)5, (byte)4 },
                    { (byte)6, (byte)6, (byte)2 },
                    { (byte)6, (byte)7, (byte)1 },
                    { (byte)6, (byte)8, (byte)5 },
                    { (byte)6, (byte)9, (byte)8 },
                    { (byte)7, (byte)1, (byte)9 },
                    { (byte)7, (byte)2, (byte)10 },
                    { (byte)7, (byte)3, (byte)6 },
                    { (byte)7, (byte)4, (byte)3 },
                    { (byte)7, (byte)5, (byte)4 },
                    { (byte)7, (byte)6, (byte)2 },
                    { (byte)7, (byte)7, (byte)1 },
                    { (byte)7, (byte)8, (byte)5 },
                    { (byte)7, (byte)9, (byte)8 },
                    { (byte)9, (byte)1, (byte)3 },
                    { (byte)9, (byte)2, (byte)10 },
                    { (byte)9, (byte)3, (byte)6 },
                    { (byte)9, (byte)4, (byte)7 },
                    { (byte)9, (byte)5, (byte)4 },
                    { (byte)9, (byte)6, (byte)2 },
                    { (byte)9, (byte)7, (byte)1 },
                    { (byte)9, (byte)8, (byte)5 },
                    { (byte)9, (byte)9, (byte)8 },
                    { (byte)10, (byte)1, (byte)9 },
                    { (byte)10, (byte)2, (byte)3 },
                    { (byte)10, (byte)3, (byte)6 },
                    { (byte)10, (byte)4, (byte)7 },
                    { (byte)10, (byte)5, (byte)4 },
                    { (byte)10, (byte)6, (byte)2 },
                    { (byte)10, (byte)7, (byte)1 },
                    { (byte)10, (byte)8, (byte)5 },
                    { (byte)10, (byte)9, (byte)8 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)1);

            migrationBuilder.DeleteData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)2);

            migrationBuilder.DeleteData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)3);

            migrationBuilder.DeleteData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)4);

            migrationBuilder.DeleteData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)5);

            migrationBuilder.DeleteData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)6);

            migrationBuilder.DeleteData(
                table: "CausaParo",
                keyColumn: "Id",
                keyValue: (short)1);

            migrationBuilder.DeleteData(
                table: "CausaParo",
                keyColumn: "Id",
                keyValue: (short)2);

            migrationBuilder.DeleteData(
                table: "CausaParo",
                keyColumn: "Id",
                keyValue: (short)3);

            migrationBuilder.DeleteData(
                table: "CausaParo",
                keyColumn: "Id",
                keyValue: (short)4);

            migrationBuilder.DeleteData(
                table: "CausaParo",
                keyColumn: "Id",
                keyValue: (short)5);

            migrationBuilder.DeleteData(
                table: "MotivoExcepcion",
                keyColumn: "Id",
                keyValue: (short)1);

            migrationBuilder.DeleteData(
                table: "MotivoExcepcion",
                keyColumn: "Id",
                keyValue: (short)2);

            migrationBuilder.DeleteData(
                table: "MotivoExcepcion",
                keyColumn: "Id",
                keyValue: (short)3);

            migrationBuilder.DeleteData(
                table: "MotivoExcepcion",
                keyColumn: "Id",
                keyValue: (short)4);

            migrationBuilder.DeleteData(
                table: "MotivoExcepcion",
                keyColumn: "Id",
                keyValue: (short)5);

            migrationBuilder.DeleteData(
                table: "MotivoExcepcion",
                keyColumn: "Id",
                keyValue: (short)6);

            migrationBuilder.DeleteData(
                table: "MotivoExcepcion",
                keyColumn: "Id",
                keyValue: (short)7);

            migrationBuilder.DeleteData(
                table: "MotivoRechazoRecepcion",
                keyColumn: "Id",
                keyValue: (short)1);

            migrationBuilder.DeleteData(
                table: "MotivoRechazoRecepcion",
                keyColumn: "Id",
                keyValue: (short)2);

            migrationBuilder.DeleteData(
                table: "MotivoRechazoRecepcion",
                keyColumn: "Id",
                keyValue: (short)3);

            migrationBuilder.DeleteData(
                table: "MotivoRechazoRecepcion",
                keyColumn: "Id",
                keyValue: (short)4);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "PrioridadLinea",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)1, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)2, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)3, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)4, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)5, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)6, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)7, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)9, (byte)9 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)1 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)2 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)3 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)4 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)5 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)6 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)7 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)8 });

            migrationBuilder.DeleteData(
                table: "ProximidadLinea",
                keyColumns: new[] { "linea_origen", "orden" },
                keyValues: new object[] { (byte)10, (byte)9 });

            migrationBuilder.DeleteData(
                table: "CategoriaParo",
                keyColumn: "Id",
                keyValue: (short)1);

            migrationBuilder.DeleteData(
                table: "CategoriaParo",
                keyColumn: "Id",
                keyValue: (short)2);

            migrationBuilder.DeleteData(
                table: "CategoriaParo",
                keyColumn: "Id",
                keyValue: (short)3);

            migrationBuilder.DeleteData(
                table: "CategoriaParo",
                keyColumn: "Id",
                keyValue: (short)4);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)1);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)2);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)3);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)4);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)5);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)6);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)7);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)8);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)9);

            migrationBuilder.DeleteData(
                table: "Linea",
                keyColumn: "Id",
                keyValue: (byte)10);

            migrationBuilder.AlterColumn<DateTime>(
                name: "creado_en",
                table: "Linea",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");
        }
    }
}

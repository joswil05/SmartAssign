using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E9.4 (docs/PROGRESO.md): <c>vw_SolicitudRelevo_L8</c> —
    /// "Capa 1" del aislamiento de datos de 04 §6.3, literal: "el
    /// supervisor de L8 necesita ver puestos ajenos (§9.4 p2) pero no a
    /// las personas (D1). La vista expone exactamente los campos
    /// permitidos." Copiada del `CREATE VIEW` de 04 §6.3 con un único
    /// ajuste, ninguno de negocio: <c>p.perfil_preferente</c> del
    /// documento es <c>p.sexo_preferente</c> en el esquema real — el
    /// propio comentario de <c>Puesto.SexoPreferente</c> ya documenta
    /// desde E3 que esa columna ES "el perfil preferente del §7.3" con
    /// otro nombre; se conserva el alias <c>perfil_preferente</c> en la
    /// salida para no romper el contrato documentado.
    ///
    /// Deliberadamente **solo Capa 1**. 04 §6.3 describe tres capas y
    /// dice explícitamente que la RLS (Capa 3) es "red de seguridad ante
    /// un fallo de la Capa 2", y que la Capa 2 ("filtro obligatorio de
    /// alcance... aplicado en el repositorio") vive en la aplicación —
    /// fuera del LEE de esta UT (00 §D1, 04 §6.3), que no toca capa de
    /// aplicación. La RLS de <c>Puesto</c> (E4.3) sigue intacta y
    /// filtrando por línea para cualquier consulta que NO pase por esta
    /// vista — es justo lo que hace de la vista el "único camino
    /// sancionado" hacia puestos ajenos; abrir esa ruta para el
    /// supervisor real de L8 es trabajo de un endpoint futuro, no de
    /// este objeto de base de datos. Esta UT prueba la vista bajo
    /// contexto de coordinador (mismo criterio que el resto de objetos
    /// de alcance de planta de esta sesión: <c>sp_DetectarFatiga</c>,
    /// <c>fn_PrioridadRelevo</c>).
    ///
    /// <c>D1</c>, lista negativa verificada por construcción: la vista
    /// nunca selecciona <c>personal_id</c> ni ninguna columna de
    /// <c>Personal</c> — es imposible que filtre nombre, ficha o
    /// restricciones médicas de nadie porque esas tablas ni se
    /// mencionan en su <c>FROM</c>.
    /// </summary>
    public partial class VistaSolicitudRelevoL8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW dbo.vw_SolicitudRelevo_L8 AS
                SELECT
                    sr.Id                AS solicitud_id,
                    l.codigo             AS linea_codigo,
                    p.codigo             AS puesto_codigo,
                    p.tipo               AS puesto_tipo,
                    sr.nivel,
                    sr.exceso_relativo,
                    sr.creada_en,
                    p.sexo_preferente    AS perfil_preferente,
                    (SELECT STRING_AGG(cf.nombre, ', ')
                       FROM PuestoCapacidad pc
                       JOIN CapacidadFisica cf ON cf.Id = pc.capacidad_id
                      WHERE pc.puesto_id = p.Id)  AS capacidades_exigidas
                FROM SolicitudRelevo sr
                JOIN Puesto p ON p.Id = sr.puesto_id
                JOIN Linea  l ON l.Id = p.linea_id
                WHERE sr.resuelta_en IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_SolicitudRelevo_L8;");
        }
    }
}

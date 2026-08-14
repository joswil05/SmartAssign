using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAssign.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// UT-E14.7 (docs/PROGRESO.md): "Carga de datos reales + purga de lo
    /// simulado" — 07 §4.3, §4.4, §9.
    ///
    /// 07 §4.4 exige dos cosas y hasta ahora solo estaba la primera a
    /// medias: "las filas simuladas llevan marca de origen y hay una
    /// prueba que falla si aparece una sola en la base de producción".
    /// `Personal` y `RestriccionMedica` traían `origen_dato` desde E3.5,
    /// pero `SembradorAdversario` también fabrica filas en `Puesto`
    /// (SIM-P01..SIM-P07) y en `AusenciaJustificada` (la ausencia que
    /// fuerza la vacante crítica de C1) — invisibles para cualquier
    /// verificación. Esta migración cierra esa mitad y añade el mecanismo
    /// que faltaba entero: verificar y purgar.
    ///
    /// Y una tercera que nadie había marcado: las seis filas de
    /// `CapacidadFisica` van a producción por migración (`HasData`) y son
    /// un placeholder que espera a H6 — la sesión con Enfermería. La
    /// columna nace en 'simulado' aquí, al revés que en las otras tablas,
    /// porque ese vocabulario lo escribió el desarrollo. La purga NO lo
    /// borra: se reemplaza, no se quita, o la regla médica (§7.2) se queda
    /// sin nada que comparar.
    ///
    /// `sp_VerificarSinDatosSimulados` es la prueba de §4.4 hecha
    /// ejecutable. `sp_PurgarDatosSimulados` borra lo fabricado y
    /// devuelve a 'operario' las categorías que 00 §G1 re-etiquetó —
    /// literal de G1: el subconjunto se saca DE los OPERARIO, así que
    /// volver a 'operario' es exacto, no una elección.
    ///
    /// Ambos exigen alcance de coordinador. No es decoración: `Puesto`
    /// vive bajo RLS (04 §6.3) y sin ese contexto el SESSION_CONTEXT
    /// nunca se fija, así que el SELECT no vería las filas simuladas de
    /// las demás líneas — un verificador que "cierra en falso" declarando
    /// limpia una base sucia es peor que no tenerlo. Comprobado: sin la
    /// guarda, una base con un puesto fabricado devuelve
    /// `filas_simuladas = 0` y ningún rechazo.
    /// </summary>
    public partial class MarcaDeOrigenCompletaYPurgaDeSimulados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origen_dato",
                table: "Puesto",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "real");

            migrationBuilder.AddColumn<string>(
                name: "origen_dato",
                table: "CapacidadFisica",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "simulado");

            migrationBuilder.AddColumn<string>(
                name: "origen_dato",
                table: "AusenciaJustificada",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "real");

            migrationBuilder.UpdateData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)1,
                column: "origen_dato",
                value: "simulado");

            migrationBuilder.UpdateData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)2,
                column: "origen_dato",
                value: "simulado");

            migrationBuilder.UpdateData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)3,
                column: "origen_dato",
                value: "simulado");

            migrationBuilder.UpdateData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)4,
                column: "origen_dato",
                value: "simulado");

            migrationBuilder.UpdateData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)5,
                column: "origen_dato",
                value: "simulado");

            migrationBuilder.UpdateData(
                table: "CapacidadFisica",
                keyColumn: "Id",
                keyValue: (short)6,
                column: "origen_dato",
                value: "simulado");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Puesto_origen_dato",
                table: "Puesto",
                sql: "origen_dato IN ('real','simulado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CapacidadFisica_origen_dato",
                table: "CapacidadFisica",
                sql: "origen_dato IN ('real','simulado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ausencia_origen_dato",
                table: "AusenciaJustificada",
                sql: "origen_dato IN ('real','simulado')");

            // ── La prueba de 07 §4.4, hecha ejecutable ──────────────────
            // Cuenta TODA fila cuyo origen no sea 'real' (incluye el
            // 'simulado_categoria' de 00 §G1: es una persona real con una
            // categoría fabricada, y G1 la excluye de producción igual).
            // Devuelve el desglose completo, con los ceros incluidos, para
            // que se vea qué tablas se vigilaron y no solo cuáles fallaron.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_VerificarSinDatosSimulados
                    @filas_simuladas INT OUTPUT,
                    @filas_placeholder INT OUTPUT,
                    @detalle NVARCHAR(MAX) OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @filas_simuladas = NULL; SET @filas_placeholder = NULL; SET @detalle = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF SESSION_CONTEXT(N'rol') IS NULL
                       OR CAST(SESSION_CONTEXT(N'rol') AS NVARCHAR(20)) <> N'coordinador'
                    BEGIN
                        SET @codigo_rechazo = 'ALCANCE_INSUFICIENTE';
                        SET @mensaje = N'Puesto vive bajo RLS (04 §6.3): sin alcance de coordinador esta verificación no vería las filas simuladas de las demás líneas y declararía limpia una base sucia. Fija SESSION_CONTEXT rol=coordinador antes de llamarla.';
                        RETURN;
                    END

                    DECLARE @conteo TABLE (tabla NVARCHAR(50), filas INT);
                    INSERT INTO @conteo (tabla, filas)
                    SELECT 'Personal',            COUNT(*) FROM Personal            WHERE origen_dato <> 'real'
                    UNION ALL
                    SELECT 'RestriccionMedica',   COUNT(*) FROM RestriccionMedica   WHERE origen_dato <> 'real'
                    UNION ALL
                    SELECT 'Puesto',              COUNT(*) FROM Puesto              WHERE origen_dato <> 'real'
                    UNION ALL
                    SELECT 'AusenciaJustificada', COUNT(*) FROM AusenciaJustificada WHERE origen_dato <> 'real'
                    UNION ALL
                    -- Sin marca propia: su origen ES el del puesto al que cuelgan.
                    SELECT 'PuestoCapacidad', COUNT(*)
                      FROM PuestoCapacidad pc JOIN Puesto p ON p.Id = pc.puesto_id
                     WHERE p.origen_dato <> 'real'
                    UNION ALL
                    SELECT 'PuestoSKU', COUNT(*)
                      FROM PuestoSKU ps JOIN Puesto p ON p.Id = ps.puesto_id
                     WHERE p.origen_dato <> 'real';

                    SELECT @filas_simuladas = SUM(filas) FROM @conteo;
                    SET @detalle = (SELECT tabla, filas FROM @conteo ORDER BY tabla FOR JSON PATH);

                    -- Aparte del recuento anterior, y a propósito: esto no
                    -- se purga, se REEMPLAZA. El vocabulario de capacidades
                    -- físicas que se sembró para poder construir la regla
                    -- médica lo escribió el desarrollo, no Enfermería (H6).
                    -- Mientras siga activo, la base decide sobre
                    -- restricciones reales con palabras que nadie del área
                    -- médica acordó — 00 §G2 y 07 §4.3 lo dejan dicho, pero
                    -- hasta ahora nada lo comprobaba. Solo cuentan las
                    -- activas: desactivar las inventadas y añadir las reales
                    -- es el camino limpio, y conserva las referencias
                    -- históricas que ya cuelguen de ellas.
                    SELECT @filas_placeholder = COUNT(*)
                      FROM CapacidadFisica WHERE origen_dato <> 'real' AND activo = 1;

                    IF @filas_simuladas > 0
                    BEGIN
                        SET @codigo_rechazo = 'HAY_DATOS_SIMULADOS';
                        SET @mensaje = N'La base contiene ' + CAST(@filas_simuladas AS NVARCHAR(10))
                                     + N' fila(s) simulada(s). Ninguna puede llegar a producción (00 §G1, §G2).'
                                     + CASE WHEN @filas_placeholder > 0
                                            THEN N' Además, el vocabulario de capacidades físicas sigue siendo el placeholder de desarrollo (H6).'
                                            ELSE N'' END;
                    END
                    ELSE IF @filas_placeholder > 0
                    BEGIN
                        SET @codigo_rechazo = 'CATALOGO_PLACEHOLDER_PENDIENTE';
                        SET @mensaje = N'Sin filas simuladas, pero el vocabulario de capacidades físicas sigue siendo el placeholder de desarrollo ('
                                     + CAST(@filas_placeholder AS NVARCHAR(10))
                                     + N' activas). Falta H6: la sesión con Enfermería (00 §G2).';
                    END
                END;
                """);

            // ── La purga ────────────────────────────────────────────────
            // No hace cascada sobre historia operativa: si algo operativo
            // ya apunta a una fila simulada, RECHAZA con la lista exacta
            // de quién apunta y desde dónde — mismo criterio que
            // sp_CerrarTurno (§1.3, §12.4: nunca un rechazo genérico).
            // Borrar la asignación, el movimiento o el relevo que la
            // referencia sería reescribir historia para poder limpiar
            // datos de prueba, justo al revés de lo que hay que proteger.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_PurgarDatosSimulados
                    @filas_purgadas INT OUTPUT,
                    @detalle NVARCHAR(MAX) OUTPUT,
                    @bloqueos NVARCHAR(MAX) OUTPUT,
                    @codigo_rechazo VARCHAR(40) OUTPUT,
                    @mensaje NVARCHAR(400) OUTPUT
                AS
                -- Manifiesto de cobertura, leído por PurgaDeDatosSimuladosTests.
                -- Toda columna del esquema que apunte a Personal o a Puesto
                -- tiene que estar aquí con una decisión explícita:
                --   bloqueo = si apunta a una fila simulada, la purga se niega
                --   borrado = la purga borra la fila que apunta
                -- La prueba compara este manifiesto contra sys.columns y
                -- sys.foreign_keys y falla en los DOS sentidos: si aparece una
                -- tabla nueva sin decidir, y si queda una entrada obsoleta.
                -- Una tabla añadida después no puede colarse en silencio.
                --
                -- COBERTURA: Asignacion.personal_id = bloqueo
                -- COBERTURA: Asignacion.titular_original_id = bloqueo
                -- COBERTURA: Asignacion.puesto_id = bloqueo
                -- COBERTURA: Auditoria.personal_id = bloqueo
                -- COBERTURA: AusenciaJustificada.personal_id = bloqueo
                -- COBERTURA: Movimiento.personal_id = bloqueo
                -- COBERTURA: Movimiento.puesto_destino_id = bloqueo
                -- COBERTURA: Puesto.titular_id = bloqueo
                -- COBERTURA: PuestoCapacidad.puesto_id = borrado
                -- COBERTURA: PuestoSKU.puesto_id = borrado
                -- COBERTURA: RelevoDescartado.personal_id = bloqueo
                -- COBERTURA: RelevoDescartado.puesto_id = bloqueo
                -- COBERTURA: RestriccionMedica.personal_id = bloqueo
                -- COBERTURA: SolicitudRelevo.puesto_id = bloqueo
                -- COBERTURA: UltimaTareaJornada.personal_id = bloqueo
                -- COBERTURA: UltimaTareaJornada.puesto_id = bloqueo
                -- COBERTURA: Usuario.personal_id = bloqueo
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET @filas_purgadas = NULL; SET @detalle = NULL; SET @bloqueos = NULL;
                    SET @codigo_rechazo = NULL; SET @mensaje = NULL;

                    IF SESSION_CONTEXT(N'rol') IS NULL
                       OR CAST(SESSION_CONTEXT(N'rol') AS NVARCHAR(20)) <> N'coordinador'
                    BEGIN
                        SET @codigo_rechazo = 'ALCANCE_INSUFICIENTE';
                        SET @mensaje = N'Puesto vive bajo RLS (04 §6.3): sin alcance de coordinador la purga no vería —ni borraría— las filas simuladas de las demás líneas, y dejaría la base a medio limpiar creyéndola limpia.';
                        RETURN;
                    END

                    DECLARE @bloq TABLE (tabla NVARCHAR(50), columna NVARCHAR(60), apunta_a NVARCHAR(10), filas INT);
                    INSERT INTO @bloq (tabla, columna, apunta_a, filas)
                    -- Referencias a una PERSONA fabricada (la que se borra).
                    SELECT 'Asignacion', 'personal_id', 'Personal', COUNT(*)
                      FROM Asignacion a JOIN Personal p ON p.Id = a.personal_id WHERE p.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'Asignacion', 'titular_original_id', 'Personal', COUNT(*)
                      FROM Asignacion a JOIN Personal p ON p.Id = a.titular_original_id WHERE p.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'Movimiento', 'personal_id', 'Personal', COUNT(*)
                      FROM Movimiento m JOIN Personal p ON p.Id = m.personal_id WHERE p.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'RelevoDescartado', 'personal_id', 'Personal', COUNT(*)
                      FROM RelevoDescartado r JOIN Personal p ON p.Id = r.personal_id WHERE p.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'UltimaTareaJornada', 'personal_id', 'Personal', COUNT(*)
                      FROM UltimaTareaJornada u JOIN Personal p ON p.Id = u.personal_id WHERE p.origen_dato = 'simulado'
                    UNION ALL
                    -- Sin FK que lo impida (a propósito: 04 §7.5 le niega
                    -- DELETE a la aplicación, la traza sobrevive a la
                    -- persona). Borrar la persona dejaría una auditoría
                    -- apuntando al vacío, así que aquí es bloqueo duro.
                    SELECT 'Auditoria', 'personal_id', 'Personal', COUNT(*)
                      FROM Auditoria x JOIN Personal p ON p.Id = x.personal_id WHERE p.origen_dato = 'simulado'
                    UNION ALL
                    -- Tampoco tiene FK (ver Usuario.PersonalId: quedó sin
                    -- restricción porque Personal no existía todavía).
                    SELECT 'Usuario', 'personal_id', 'Personal', COUNT(*)
                      FROM Usuario x JOIN Personal p ON p.Id = x.personal_id WHERE p.origen_dato = 'simulado'
                    UNION ALL
                    -- Un puesto REAL cuyo titular es una persona fabricada:
                    -- ponerle NULL sería inventar un cambio sobre dato real.
                    SELECT 'Puesto', 'titular_id', 'Personal', COUNT(*)
                      FROM Puesto q JOIN Personal p ON p.Id = q.titular_id
                     WHERE p.origen_dato = 'simulado' AND q.origen_dato = 'real'
                    UNION ALL
                    -- Estas dos SÍ se borran cuando son simuladas, pero una
                    -- fila marcada 'real' que cuelgue de una persona
                    -- fabricada es una contradicción del dato: reventaría la
                    -- FK a mitad de la purga. Mejor un rechazo con nombre
                    -- que un error de restricción sin explicación.
                    SELECT 'RestriccionMedica', 'personal_id', 'Personal', COUNT(*)
                      FROM RestriccionMedica r JOIN Personal p ON p.Id = r.personal_id
                     WHERE p.origen_dato = 'simulado' AND r.origen_dato = 'real'
                    UNION ALL
                    SELECT 'AusenciaJustificada', 'personal_id', 'Personal', COUNT(*)
                      FROM AusenciaJustificada j JOIN Personal p ON p.Id = j.personal_id
                     WHERE p.origen_dato = 'simulado' AND j.origen_dato = 'real'
                    UNION ALL
                    -- Referencias a un PUESTO fabricado.
                    SELECT 'Asignacion', 'puesto_id', 'Puesto', COUNT(*)
                      FROM Asignacion a JOIN Puesto q ON q.Id = a.puesto_id WHERE q.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'Movimiento', 'puesto_destino_id', 'Puesto', COUNT(*)
                      FROM Movimiento m JOIN Puesto q ON q.Id = m.puesto_destino_id WHERE q.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'RelevoDescartado', 'puesto_id', 'Puesto', COUNT(*)
                      FROM RelevoDescartado r JOIN Puesto q ON q.Id = r.puesto_id WHERE q.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'SolicitudRelevo', 'puesto_id', 'Puesto', COUNT(*)
                      FROM SolicitudRelevo s JOIN Puesto q ON q.Id = s.puesto_id WHERE q.origen_dato = 'simulado'
                    UNION ALL
                    SELECT 'UltimaTareaJornada', 'puesto_id', 'Puesto', COUNT(*)
                      FROM UltimaTareaJornada u JOIN Puesto q ON q.Id = u.puesto_id WHERE q.origen_dato = 'simulado'
                    UNION ALL
                    -- La re-etiqueta de 00 §G1 vuelve a 'operario'. Si esa
                    -- persona está ocupando un puesto AHORA bajo la
                    -- categoría fabricada, devolverle la real dejaría en
                    -- pie una asignación que el motor nunca habría
                    -- permitido: se rechaza en vez de arreglarlo a ciegas.
                    SELECT 'Asignacion', 'personal_id (abierta, categoría simulada)', 'Personal', COUNT(*)
                      FROM Asignacion a JOIN Personal p ON p.Id = a.personal_id
                     WHERE p.origen_dato = 'simulado_categoria' AND a.fin IS NULL;

                    DELETE FROM @bloq WHERE filas = 0;

                    IF EXISTS (SELECT 1 FROM @bloq)
                    BEGIN
                        SET @bloqueos = (SELECT tabla, columna, apunta_a, filas FROM @bloq
                                         ORDER BY tabla, columna FOR JSON PATH);
                        SET @codigo_rechazo = 'PURGA_BLOQUEADA';
                        SET @mensaje = N'Hay datos operativos apuntando a filas simuladas. La purga no reescribe historia: revisa la lista de bloqueos.';
                        RETURN;
                    END

                    DECLARE @cuenta TABLE (tabla NVARCHAR(50), filas INT);

                    BEGIN TRAN;
                        DECLARE @n INT;

                        DELETE FROM RestriccionMedica WHERE origen_dato <> 'real';
                        SET @n = @@ROWCOUNT; INSERT INTO @cuenta VALUES ('RestriccionMedica', @n);

                        DELETE FROM AusenciaJustificada WHERE origen_dato <> 'real';
                        SET @n = @@ROWCOUNT; INSERT INTO @cuenta VALUES ('AusenciaJustificada', @n);

                        DELETE pc FROM PuestoCapacidad pc
                          JOIN Puesto p ON p.Id = pc.puesto_id WHERE p.origen_dato = 'simulado';
                        SET @n = @@ROWCOUNT; INSERT INTO @cuenta VALUES ('PuestoCapacidad', @n);

                        DELETE ps FROM PuestoSKU ps
                          JOIN Puesto p ON p.Id = ps.puesto_id WHERE p.origen_dato = 'simulado';
                        SET @n = @@ROWCOUNT; INSERT INTO @cuenta VALUES ('PuestoSKU', @n);

                        DELETE FROM Puesto WHERE origen_dato = 'simulado';
                        SET @n = @@ROWCOUNT; INSERT INTO @cuenta VALUES ('Puesto', @n);

                        DELETE FROM Personal WHERE origen_dato = 'simulado';
                        SET @n = @@ROWCOUNT; INSERT INTO @cuenta VALUES ('Personal', @n);

                        -- 00 §G1, literal: "los operadores B y operadores C
                        -- sácalos de los operarios". El valor de vuelta no
                        -- se elige, se sabe.
                        UPDATE Personal SET categoria = 'operario', origen_dato = 'real'
                         WHERE origen_dato = 'simulado_categoria';
                        SET @n = @@ROWCOUNT; INSERT INTO @cuenta VALUES ('Personal (categoría revertida)', @n);

                        -- CapacidadFisica NO se toca, y es deliberado: ese
                        -- catálogo se reemplaza cuando llegue H6, no se
                        -- borra. Sin vocabulario, la regla médica (§7.2) se
                        -- queda sin nada que comparar y RestriccionMedica /
                        -- PuestoCapacidad pierden su referencia.
                        -- sp_VerificarSinDatosSimulados lo reporta aparte.
                    COMMIT;

                    SELECT @filas_purgadas = SUM(filas) FROM @cuenta;
                    SET @detalle = (SELECT tabla, filas FROM @cuenta ORDER BY tabla FOR JSON PATH);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_PurgarDatosSimulados;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_VerificarSinDatosSimulados;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Puesto_origen_dato",
                table: "Puesto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CapacidadFisica_origen_dato",
                table: "CapacidadFisica");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ausencia_origen_dato",
                table: "AusenciaJustificada");

            migrationBuilder.DropColumn(
                name: "origen_dato",
                table: "Puesto");

            migrationBuilder.DropColumn(
                name: "origen_dato",
                table: "CapacidadFisica");

            migrationBuilder.DropColumn(
                name: "origen_dato",
                table: "AusenciaJustificada");
        }
    }
}

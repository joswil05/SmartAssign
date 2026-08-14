using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.DatosSimulados;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.DatosSimulados;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Semillas;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E14.7 (docs/PROGRESO.md): "Carga de datos reales + purga de lo
/// simulado" — 07 §4.3, §4.4, §9.
///
/// 07 §4.4 pide literalmente "una prueba que <b>falla si aparece una sola
/// [fila simulada] en la base de producción</b>", y §9 la vuelve a nombrar
/// como la mitigación del riesgo "lo simulado llega a producción". Esta
/// clase es esa prueba, en las dos direcciones que la hacen valer algo:
/// que una base limpia se declare limpia, y —lo que de verdad importa—
/// que una sucia NO se declare limpia.
/// </summary>
public class PurgaDeDatosSimuladosTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    /// <summary>
    /// Puesto vive bajo RLS (04 §6.3). Sin este contexto ni la propia
    /// verificación vería las filas que busca — ver la guarda
    /// ALCANCE_INSUFICIENTE de los dos procedimientos.
    /// </summary>
    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await ctx.Database.OpenConnectionAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
    }

    private int _usuarioId;

    public async Task InitializeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
        await ComoCoordinadorAsync(ctx);

        var usuario = new Usuario
        {
            Username = "u_purga", NombreCompleto = "Usuario Prueba",
            Rol = "coordinador", OrigenIdentidad = "local", Activo = true,
        };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        _usuarioId = usuario.Id;
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    /// <summary>Padrón real mínimo, para que las re-etiquetas de 00 §G1 tengan algo real sobre lo que actuar.</summary>
    private async Task SembrarPadronRealAsync()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);

        var contador = 1;
        foreach (var lineaId in new byte[] { 1, 2, 4, 6, 8 })
        {
            for (var i = 0; i < 6; i++)
            {
                ctx.Personas.Add(new Personal
                {
                    Ficha = $"R{contador:D4}", NombreCompleto = $"Operario Real {contador}",
                    Categoria = "operario", LineaHabitual = lineaId,
                });
                contador++;
            }
            ctx.Personas.Add(new Personal
            {
                Ficha = $"A{lineaId:D3}", NombreCompleto = $"Operador A de L{lineaId}",
                Categoria = "operador_a", LineaHabitual = lineaId,
            });
            ctx.Puestos.Add(new Puesto
            {
                LineaId = lineaId, Codigo = $"REAL-{lineaId:D2}", NombrePuesto = $"Puesto real de L{lineaId}",
                Tipo = "rotativo", HorasEnPuesto = 4,
            });
        }
        await ctx.SaveChangesAsync();
    }

    private async Task SembrarAdversariaAsync()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        await new SembradorAdversario(ctx).SembrarAsync(_usuarioId);
    }

    private async Task<ResultadoVerificacionSimulados> VerificarAsync()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        return await new ServicioDatosSimulados(ctx).VerificarAsync();
    }

    private async Task<ResultadoPurgaSimulados> PurgarAsync()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        return await new ServicioDatosSimulados(ctx).PurgarAsync();
    }

    // ── 07 §4.4: la prueba que falla si aparece UNA SOLA fila simulada ──

    [Fact]
    public async Task Una_base_recien_migrada_no_tiene_ninguna_fila_fabricada()
    {
        // Migraciones + semilla estructural y de catálogo: exactamente lo
        // que se despliega en planta. Nada más ha corrido sobre ella.
        var resultado = await VerificarAsync();

        resultado.FilasSimuladas.Should().Be(0);
        resultado.SinFilasSimuladas.Should().BeTrue();
    }

    [Fact]
    public async Task Pero_esa_misma_base_TODAVIA_no_esta_lista_para_produccion_por_H6()
    {
        // El hueco que esta UT encontró: las seis capacidades físicas que
        // se siembran por migración las escribió el desarrollo, no
        // Enfermería (ver DatosCatalogo, que ya lo decía en un comentario).
        // Iban a producción sin que nada lo señalara, y ahí decidirían
        // sobre restricciones médicas reales.
        var resultado = await VerificarAsync();

        resultado.EstaLimpia.Should().BeFalse();
        resultado.CodigoRechazo.Should().Be("CATALOGO_PLACEHOLDER_PENDIENTE");
        resultado.FilasPlaceholder.Should().Be(6);
        resultado.Mensaje.Should().Contain("H6");
    }

    [Fact]
    public async Task Con_el_vocabulario_real_de_Enfermeria_la_base_si_queda_lista()
    {
        // H6 cumplida: el Coordinador desactiva el vocabulario inventado y
        // registra el acordado con Enfermería, marcado 'real'.
        await using (var ctx = CrearContexto())
        {
            await ComoCoordinadorAsync(ctx);
            await ctx.Database.ExecuteSqlRawAsync("UPDATE CapacidadFisica SET activo = 0;");
            ctx.CapacidadesFisicas.Add(new CapacidadFisica
            {
                Codigo = "acordada_con_enfermeria",
                Nombre = "Capacidad del vocabulario real",
                OrigenDato = "real",
            });
            await ctx.SaveChangesAsync();
        }

        var resultado = await VerificarAsync();

        resultado.CodigoRechazo.Should().BeNull();
        resultado.FilasPlaceholder.Should().Be(0);
        resultado.EstaLimpia.Should().BeTrue();
    }

    [Fact]
    public async Task El_importador_real_tampoco_fabrica_filas()
    {
        // El camino real de H5: padrón y puestos cargados como 'real'.
        await SembrarPadronRealAsync();

        var resultado = await VerificarAsync();

        resultado.FilasSimuladas.Should().Be(0);
        resultado.SinFilasSimuladas.Should().BeTrue();
    }

    [Fact]
    public async Task La_verificacion_declara_SUCIA_una_base_con_la_semilla_adversaria()
    {
        // El sentido que de verdad importa: si esto pasara en verde, la
        // prueba de §4.4 sería decorativa y dejaría llegar lo simulado.
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        var resultado = await VerificarAsync();

        resultado.EstaLimpia.Should().BeFalse();
        resultado.SinFilasSimuladas.Should().BeFalse();
        resultado.CodigoRechazo.Should().Be("HAY_DATOS_SIMULADOS");
        resultado.FilasSimuladas.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task La_verificacion_ve_las_CUATRO_tablas_marcadas_y_las_dos_derivadas()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        var resultado = await VerificarAsync();
        var porTabla = resultado.Detalle.ToDictionary(d => d.Tabla, d => d.Filas);

        // Personal y RestriccionMedica ya estaban marcadas desde E3.5.
        porTabla["Personal"].Should().BeGreaterThan(0, "SIM-0001..SIM-0006 más las re-etiquetas de 00 §G1");
        porTabla["RestriccionMedica"].Should().BeGreaterThan(0, "los escenarios 1-4 de 07 §4.4");

        // Estas dos son lo que E14.7 añade: la semilla fabricaba filas aquí
        // desde E3.5 y ninguna verificación podía verlas.
        porTabla["Puesto"].Should().BeGreaterThan(0, "SIM-P01..SIM-P07");
        porTabla["AusenciaJustificada"].Should().BeGreaterThan(0, "la ausencia que fuerza la vacante crítica (C1)");

        // Sin marca propia: su origen es el del puesto del que cuelgan.
        porTabla["PuestoCapacidad"].Should().BeGreaterThan(0, "la capacidad exigida por SIM-P01");
        porTabla.Should().ContainKey("PuestoSKU", "se vigila aunque hoy salga en cero");
    }

    [Fact]
    public async Task La_verificacion_se_niega_sin_alcance_de_coordinador_en_vez_de_cerrar_en_falso()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        // Conexión cruda, sin SESSION_CONTEXT: el filtro de RLS sobre
        // Puesto no dejaría ver las filas simuladas de las demás líneas.
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();

        var parametros = new DynamicParameters();
        parametros.Add("filas_simuladas", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("filas_placeholder", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("detalle", dbType: DbType.String, size: -1, direction: ParameterDirection.Output);
        parametros.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        parametros.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync("dbo.sp_VerificarSinDatosSimulados", parametros,
            commandType: CommandType.StoredProcedure);

        parametros.Get<string?>("codigo_rechazo").Should().Be("ALCANCE_INSUFICIENTE");
        parametros.Get<int?>("filas_simuladas").Should().BeNull(
            "un recuento parcial sería peor que ninguno: diría 'limpia' sobre una base sucia");
    }

    // ── La purga ────────────────────────────────────────────────────────

    [Fact]
    public async Task La_purga_deja_la_base_sin_una_sola_fila_fabricada()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();
        (await VerificarAsync()).SinFilasSimuladas.Should().BeFalse("punto de partida: sucia");

        var purga = await PurgarAsync();

        purga.CodigoRechazo.Should().BeNull();
        purga.FilasPurgadas.Should().BeGreaterThan(0);

        var despues = await VerificarAsync();
        despues.SinFilasSimuladas.Should().BeTrue();

        // Y sigue sin estar lista para producción: la purga borra filas, no
        // trae el vocabulario de Enfermería. Dos problemas, dos arreglos.
        despues.EstaLimpia.Should().BeFalse();
        despues.CodigoRechazo.Should().Be("CATALOGO_PLACEHOLDER_PENDIENTE");
    }

    [Fact]
    public async Task La_purga_no_borra_el_vocabulario_de_capacidades_fisicas()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        await PurgarAsync();

        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);

        // Borrarlo dejaría a la regla médica (§7.2) sin nada que comparar.
        // Se reemplaza cuando llegue H6; no se quita.
        (await ctx.CapacidadesFisicas.CountAsync()).Should().Be(6);
    }

    [Fact]
    public async Task La_purga_devuelve_a_operario_las_categorias_re_etiquetadas_sin_borrar_a_la_persona()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        await using (var ctx = CrearContexto())
        {
            await ComoCoordinadorAsync(ctx);
            var reetiquetadas = await ctx.Personas
                .Where(p => p.OrigenDato == "simulado_categoria")
                .Select(p => p.Ficha).ToListAsync();
            reetiquetadas.Should().NotBeEmpty("00 §G1 saca los Operadores B/C de los OPERARIO");

            await new ServicioDatosSimulados(ctx).PurgarAsync();
        }

        await using var verificacion = CrearContexto();
        await ComoCoordinadorAsync(verificacion);

        // 00 §G1, literal: "los operadores B y operadores C sácalos de los
        // operarios" — el valor de vuelta se sabe, no se elige.
        (await verificacion.Personas.CountAsync(p => p.Categoria == "operador_b")).Should().Be(0);
        (await verificacion.Personas.CountAsync(p => p.Categoria == "operador_c")).Should().Be(0);
        (await verificacion.Personas.CountAsync(p => p.OrigenDato == "simulado_categoria")).Should().Be(0);

        // Y siguen existiendo: son personas reales del padrón, solo tenían
        // la categoría fabricada.
        (await verificacion.Personas.CountAsync(p => p.Ficha.StartsWith("R"))).Should().Be(30);
    }

    [Fact]
    public async Task La_purga_no_toca_ninguna_fila_real()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        await PurgarAsync();

        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);

        (await ctx.Personas.CountAsync()).Should().Be(35, "30 operarios + 5 Operadores A, todos reales");
        (await ctx.Puestos.CountAsync()).Should().Be(5, "un puesto real por línea; los SIM-P se fueron");
        (await ctx.Puestos.AllAsync(p => p.OrigenDato == "real")).Should().BeTrue();
        (await ctx.Personas.AllAsync(p => p.OrigenDato == "real")).Should().BeTrue();
    }

    [Fact]
    public async Task La_purga_se_niega_con_la_lista_exacta_si_algo_operativo_apunta_a_una_fila_simulada()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        // UltimaTareaJornada (00 §B6) apuntando a una persona y a un puesto
        // fabricados: borrarlos reescribiría el historial de no repetición.
        await using (var ctx = CrearContexto())
        {
            await ComoCoordinadorAsync(ctx);
            var personaSimulada = await ctx.Personas.FirstAsync(p => p.OrigenDato == "simulado");
            var puestoSimulado = await ctx.Puestos.FirstAsync(p => p.OrigenDato == "simulado" && p.TipoActividadId != null);

            ctx.UltimasTareasJornada.Add(new UltimaTareaJornada
            {
                PersonalId = personaSimulada.Id,
                PuestoId = puestoSimulado.Id,
                TipoActividadId = puestoSimulado.TipoActividadId!.Value,
                DiaOperacion = DateOnly.FromDateTime(DateTime.UtcNow),
            });
            await ctx.SaveChangesAsync();
        }

        var purga = await PurgarAsync();

        purga.CodigoRechazo.Should().Be("PURGA_BLOQUEADA");
        purga.Bloqueos.Should().NotBeEmpty();
        purga.Bloqueos.Should().Contain(b => b.Tabla == "UltimaTareaJornada" && b.Columna == "personal_id" && b.Filas == 1);
        purga.Bloqueos.Should().Contain(b => b.Tabla == "UltimaTareaJornada" && b.Columna == "puesto_id" && b.Filas == 1);
        purga.Bloqueos.Should().OnlyContain(b => b.Filas > 0, "un bloqueo de cero filas no bloquea nada");

        // Y no borró nada por el camino: el rechazo es total, no parcial.
        (await VerificarAsync()).SinFilasSimuladas.Should().BeFalse();
    }

    [Fact]
    public async Task La_purga_se_niega_si_una_persona_re_etiquetada_esta_ocupando_un_puesto_ahora_mismo()
    {
        await SembrarPadronRealAsync();
        await SembrarAdversariaAsync();

        // Devolverle 'operario' mientras ocupa un puesto dejaría en pie una
        // asignación que el motor nunca habría permitido bajo la categoría
        // real (00 §B12: la compatibilidad de categoría no cede nunca).
        await using (var ctx = CrearContexto())
        {
            await ComoCoordinadorAsync(ctx);
            var reetiquetada = await ctx.Personas.FirstAsync(p => p.OrigenDato == "simulado_categoria");
            var puestoReal = await ctx.Puestos.FirstAsync(p => p.OrigenDato == "real");
            var jornada = await CrearJornadaAsync(ctx, puestoReal.LineaId);

            ctx.Asignaciones.Add(new Asignacion
            {
                JornadaLineaId = jornada,
                PuestoId = puestoReal.Id,
                PersonalId = reetiquetada.Id,
                Origen = "manual_supervisor",
                Inicio = DateTime.UtcNow,
                Fin = null, // abierta: está ocupándolo ahora
                AsignadoPor = _usuarioId,
            });
            await ctx.SaveChangesAsync();
        }

        var purga = await PurgarAsync();

        purga.CodigoRechazo.Should().Be("PURGA_BLOQUEADA");
        purga.Bloqueos.Should().Contain(b => b.Tabla == "Asignacion" && b.Columna.Contains("categoría simulada"));
    }

    private async Task<int> CrearJornadaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        // Turno se siembra VACÍO a propósito (07 §2.1 R2: los parámetros de
        // negocio no se inventan) — esta prueba crea el suyo.
        var turno = await ctx.Turnos.FirstOrDefaultAsync();
        if (turno is null)
        {
            turno = new Turno
            {
                Nombre = "Turno de prueba",
                HoraInicio = new TimeOnly(6, 0),
                HoraFin = new TimeOnly(14, 0),
            };
            ctx.Turnos.Add(turno);
            await ctx.SaveChangesAsync();
        }

        var jornada = new JornadaLinea
        {
            LineaId = lineaId,
            TurnoId = turno.Id,
            DiaOperacion = DateOnly.FromDateTime(DateTime.UtcNow),
            Estado = "arrancada",
            SupervisorId = _usuarioId,
        };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    // ── La cobertura no puede quedarse obsoleta en silencio ─────────────

    [Fact]
    public async Task Toda_columna_que_apunte_a_Personal_o_Puesto_tiene_una_decision_explicita_en_la_purga()
    {
        // Una tabla nueva que referencie a Personal o a Puesto y que nadie
        // añada al procedimiento haría que la purga borrase su padre y
        // dejase la referencia colgando —o peor, que reventase a mitad—.
        // Esta prueba lee el esquema VIVO y el cuerpo REAL del
        // procedimiento, así que falla en los dos sentidos: cobertura que
        // falta, y cobertura que sobra.
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();

        var enElEsquema = (await conexion.QueryAsync<(string Tabla, string Columna)>("""
            SELECT DISTINCT OBJECT_NAME(fk.parent_object_id)                        AS Tabla,
                            COL_NAME(fkc.parent_object_id, fkc.parent_column_id)    AS Columna
              FROM sys.foreign_keys fk
              JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
             WHERE OBJECT_NAME(fk.referenced_object_id) IN ('Personal','Puesto')
            UNION
            -- Sin FK a propósito (Auditoria: 04 §7.5; Usuario: Personal no
            -- existía cuando se creó). Son justo las peligrosas: nada las
            -- protege salvo esta lista.
            SELECT t.name, c.name
              FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
             WHERE c.name IN ('personal_id','puesto_id')
            """)).Select(f => $"{f.Tabla}.{f.Columna}").ToHashSet();

        var cuerpo = await conexion.ExecuteScalarAsync<string>(
            "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.sp_PurgarDatosSimulados'));");

        var declaradas = Regex.Matches(cuerpo!, @"COBERTURA:\s*(\S+)\s*=\s*(bloqueo|borrado)")
            .Select(m => m.Groups[1].Value).ToHashSet();

        declaradas.Should().BeEquivalentTo(enElEsquema,
            "el manifiesto COBERTURA de sp_PurgarDatosSimulados y el esquema real tienen que decir lo mismo");
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.2 (docs/PROGRESO.md): <c>fn_PrioridadRelevo</c> — el orden de
/// la cola de relevos pendientes, literal 00 §B3: crítico antes que
/// sugerido, luego mayor exceso relativo, luego FIFO. Mismo patrón de
/// base descartable que el resto de la suite.
/// </summary>
public class OrdenDeColaDeRelevoTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InitializeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    // ═══ Helpers de datos ═══

    private static async Task<int> CrearPuestoAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba", Tipo = "rotativo",
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task<int> JornadaAbiertaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    /// <summary>Inserta una SolicitudRelevo directamente, con control total de nivel/exceso/antigüedad — el propio orden es lo que se prueba.</summary>
    private async Task InsertarSolicitudAsync(
        int puestoId, int jornadaLineaId, string nivel, decimal? exceso, int minutosDeAntiguedad, string origen = "umbral_automatico")
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel, exceso_relativo, creada_en)
            VALUES (@puesto_id, @jornada_linea_id, @origen, @nivel, @exceso, DATEADD(MINUTE, -@minutos, SYSUTCDATETIME()));
            """;
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@origen", origen);
        cmd.Parameters.AddWithValue("@nivel", nivel);
        cmd.Parameters.AddWithValue("@exceso", (object?)exceso ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@minutos", minutosDeAntiguedad);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int?> PrioridadAsync(string nivel)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT dbo.fn_PrioridadRelevo(@nivel)";
        cmd.Parameters.AddWithValue("@nivel", nivel);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is DBNull or null ? null : Convert.ToInt32(resultado);
    }

    /// <summary>La cola tal como la consumirían E9.4/E9.5: el ORDER BY que compone fn_PrioridadRelevo, exceso_relativo y creada_en.</summary>
    private async Task<List<string>> ColaOrdenadaPorCodigoDePuestoAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            SELECT p.codigo
              FROM SolicitudRelevo sr
              JOIN Puesto p ON p.Id = sr.puesto_id
             WHERE sr.resuelta_en IS NULL
             ORDER BY dbo.fn_PrioridadRelevo(sr.nivel), sr.exceso_relativo DESC, sr.creada_en ASC;
            """;
        var resultado = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) resultado.Add(reader.GetString(0));
        return resultado;
    }

    [Theory]
    [InlineData("maxima", 1)]
    [InlineData("critico", 2)]
    [InlineData("sugerido", 3)]
    public async Task Cada_nivel_tiene_su_rango_exacto(string nivel, int rangoEsperado)
    {
        (await PrioridadAsync(nivel)).Should().Be(rangoEsperado);
    }

    [Fact]
    public async Task Critico_siempre_antes_que_sugerido_sin_importar_el_exceso_relativo()
    {
        // 00 §B3, criterio 1: nivel antes que exceso.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var sugeridoAlto = await CrearPuestoAsync(ctx, lineaId: 4);
        var criticoBajo = await CrearPuestoAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(sugeridoAlto, jornada, "sugerido", exceso: 250m, minutosDeAntiguedad: 5);
        await InsertarSolicitudAsync(criticoBajo, jornada, "critico", exceso: 101m, minutosDeAntiguedad: 1);

        var cola = await ColaOrdenadaPorCodigoDePuestoAsync();

        cola.Should().Equal([
            (await ctx.Puestos.AsNoTracking().Where(p => p.Id == criticoBajo).Select(p => p.Codigo).SingleAsync()),
            (await ctx.Puestos.AsNoTracking().Where(p => p.Id == sugeridoAlto).Select(p => p.Codigo).SingleAsync()),
        ]);
    }

    [Fact]
    public async Task Dentro_del_mismo_nivel_gana_el_mayor_exceso_relativo()
    {
        // 00 §B3, criterio 2.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var excesoBajo = await CrearPuestoAsync(ctx, lineaId: 4);
        var excesoAlto = await CrearPuestoAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(excesoBajo, jornada, "sugerido", exceso: 105m, minutosDeAntiguedad: 1);
        await InsertarSolicitudAsync(excesoAlto, jornada, "sugerido", exceso: 180m, minutosDeAntiguedad: 5);

        var cola = await ColaOrdenadaPorCodigoDePuestoAsync();

        cola.Should().Equal([
            (await ctx.Puestos.AsNoTracking().Where(p => p.Id == excesoAlto).Select(p => p.Codigo).SingleAsync()),
            (await ctx.Puestos.AsNoTracking().Where(p => p.Id == excesoBajo).Select(p => p.Codigo).SingleAsync()),
        ]);
    }

    [Fact]
    public async Task Con_nivel_y_exceso_iguales_gana_la_mas_antigua_FIFO()
    {
        // 00 §B3, criterio 3.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var reciente = await CrearPuestoAsync(ctx, lineaId: 4);
        var antigua = await CrearPuestoAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(reciente, jornada, "sugerido", exceso: 150m, minutosDeAntiguedad: 2);
        await InsertarSolicitudAsync(antigua, jornada, "sugerido", exceso: 150m, minutosDeAntiguedad: 30);

        var cola = await ColaOrdenadaPorCodigoDePuestoAsync();

        cola.Should().Equal([
            (await ctx.Puestos.AsNoTracking().Where(p => p.Id == antigua).Select(p => p.Codigo).SingleAsync()),
            (await ctx.Puestos.AsNoTracking().Where(p => p.Id == reciente).Select(p => p.Codigo).SingleAsync()),
        ]);
    }

    [Fact]
    public async Task La_cola_completa_de_cinco_solicitudes_respeta_los_tres_criterios_a_la_vez()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);

        var pMaxima = await CrearPuestoAsync(ctx, lineaId: 4);
        var pCriticoAlto = await CrearPuestoAsync(ctx, lineaId: 4);
        var pCriticoBajo = await CrearPuestoAsync(ctx, lineaId: 4);
        var pSugeridoAntiguo = await CrearPuestoAsync(ctx, lineaId: 4);
        var pSugeridoReciente = await CrearPuestoAsync(ctx, lineaId: 4);

        // Insertados deliberadamente FUERA de su orden final esperado.
        // Los dos "sugerido" empatan en exceso a propósito, para que el
        // criterio 3 (FIFO) sea el que de verdad los desempate.
        await InsertarSolicitudAsync(pSugeridoReciente, jornada, "sugerido", 150m, minutosDeAntiguedad: 1);
        await InsertarSolicitudAsync(pCriticoBajo, jornada, "critico", 101m, minutosDeAntiguedad: 3);
        await InsertarSolicitudAsync(pSugeridoAntiguo, jornada, "sugerido", 150m, minutosDeAntiguedad: 60);
        await InsertarSolicitudAsync(pMaxima, jornada, "maxima", null, minutosDeAntiguedad: 1, origen: "vacante_critica");
        await InsertarSolicitudAsync(pCriticoAlto, jornada, "critico", 250m, minutosDeAntiguedad: 2);

        var cola = await ColaOrdenadaPorCodigoDePuestoAsync();

        var codigos = await ctx.Puestos.AsNoTracking()
            .Where(p => new[] { pMaxima, pCriticoAlto, pCriticoBajo, pSugeridoAntiguo, pSugeridoReciente }.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Codigo);

        cola.Should().Equal([
            codigos[pMaxima],
            codigos[pCriticoAlto],
            codigos[pCriticoBajo],
            codigos[pSugeridoAntiguo],
            codigos[pSugeridoReciente],
        ]);
    }
}

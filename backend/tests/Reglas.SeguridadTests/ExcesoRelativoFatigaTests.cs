using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E7.2 (docs/PROGRESO.md): <c>fn_ExcesoRelativoFatiga</c> (00 §A4,
/// §B3) — "70 minutos en un puesto cuyo umbral es 60 es peor que 70
/// minutos en uno cuyo umbral es 120": el ordenamiento por fatiga nunca
/// usa minutos absolutos. Mismo patrón de base descartable que
/// <see cref="RelojDeFatigaTests"/>.
/// </summary>
public class ExcesoRelativoFatigaTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    /// <summary>
    /// JornadaLinea también tiene RLS (04 §6.3, E4.7) — sin contexto de
    /// coordinador, la propia prueba no vería la jornada que acaba de
    /// crear al reutilizarla entre puestos de la misma línea.
    /// </summary>
    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != System.Data.ConnectionState.Open) await conexion.OpenAsync();
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

    // ═══ Helpers de datos (mismo patrón que RelojDeFatigaTests) ═══

    private static async Task<int> CrearPuestoAsync(
        SmartAssignDbContext ctx, string tipo = "rotativo", bool activo = true, short? horasEnPuesto = null)
    {
        var puesto = new Puesto
        {
            LineaId = 4, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba",
            Tipo = tipo, Activo = activo, HorasEnPuesto = horasEnPuesto,
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx)
    {
        var u = new Usuario
        {
            Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba",
            Rol = "coordinador", OrigenIdentidad = "local", Activo = true,
        };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    /// <summary>
    /// UX_JornadaLinea_abierta (E5) permite a lo sumo una jornada abierta
    /// por línea — varias asignaciones de la misma prueba en L4 comparten
    /// la misma jornada en vez de crear una por llamada.
    /// </summary>
    private static async Task<int> JornadaAbiertaDeL4Async(SmartAssignDbContext ctx)
    {
        var existente = await ctx.JornadasLinea.Where(j => j.LineaId == 4 && j.CerradoEn == null).Select(j => j.Id).SingleOrDefaultAsync();
        if (existente != 0) return existente;

        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();

        var jornada = new JornadaLinea { LineaId = 4, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    private static async Task AsignarDesdeHaceAsync(SmartAssignDbContext ctx, int puestoId, int minutosAtras)
    {
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba", Categoria = "operario" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();

        var jornadaId = await JornadaAbiertaDeL4Async(ctx);

        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaId, PuestoId = puestoId, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddMinutes(-minutosAtras), AsignadoPor = usuario,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SetParametroAsync(SmartAssignDbContext ctx, string clave, string valor)
    {
        ctx.Parametros.Add(new Parametro { Clave = clave, Valor = valor, Tipo = "int", Descripcion = "prueba" });
        await ctx.SaveChangesAsync();
    }

    private async Task<decimal?> ExcesoRelativoAsync(int puestoId)
    {
        // fn_ExcesoRelativoFatiga -> fn_MinutosEnPuesto/fn_UmbralFatigaSugeridoMinutos
        // -> Puesto, con RLS (mismo criterio que RelojDeFatigaTests).
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var ctxCmd = conexion.CreateCommand())
        {
            ctxCmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await ctxCmd.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT dbo.fn_ExcesoRelativoFatiga(@p)";
        cmd.Parameters.AddWithValue("@p", puestoId);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is DBNull or null ? null : (decimal)resultado;
    }

    [Fact]
    public async Task Setenta_minutos_contra_umbral_60_es_peor_que_setenta_minutos_contra_umbral_120()
    {
        // 00 §A4, el ejemplo normativo exacto: mismos minutos absolutos,
        // orden invertido según el umbral propio de cada puesto.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puestoUmbralBajo = await CrearPuestoAsync(ctx, horasEnPuesto: 1); // 60 min
        var puestoUmbralAlto = await CrearPuestoAsync(ctx, horasEnPuesto: 2); // 120 min
        await AsignarDesdeHaceAsync(ctx, puestoUmbralBajo, minutosAtras: 70);
        await AsignarDesdeHaceAsync(ctx, puestoUmbralAlto, minutosAtras: 70);

        var excesoBajo = await ExcesoRelativoAsync(puestoUmbralBajo);
        var excesoAlto = await ExcesoRelativoAsync(puestoUmbralAlto);

        excesoBajo.Should().BeGreaterThan(excesoAlto!.Value, "70 min sobre un umbral de 60 es peor que 70 min sobre un umbral de 120");
        excesoBajo.Should().BeApproximately(116.67m, 0.01m);
        excesoAlto.Should().BeApproximately(58.33m, 0.01m);
    }

    [Fact]
    public async Task Exactamente_en_el_umbral_sugerido_da_cien_por_ciento()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 1); // 60 min
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 60);

        (await ExcesoRelativoAsync(puesto)).Should().BeApproximately(100m, 0.01m);
    }

    [Fact]
    public async Task Antes_de_llegar_al_umbral_el_porcentaje_avanza_de_forma_continua_por_debajo_de_cien()
    {
        // 03 §2 / §9.1: "la barra se llena progresivamente desde el minuto
        // cero" — no solo aparece al cruzar el umbral.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 1); // 60 min
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 30);

        (await ExcesoRelativoAsync(puesto)).Should().BeApproximately(50m, 0.01m);
    }

    [Fact]
    public async Task Un_fijo_no_tiene_exceso_relativo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, tipo: "fijo", horasEnPuesto: 1);
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 70);

        (await ExcesoRelativoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Un_rotativo_sin_nadie_asignado_no_tiene_exceso_relativo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 1);

        (await ExcesoRelativoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Sin_umbral_sugerido_ni_propio_ni_de_planta_no_hay_porcentaje_que_calcular()
    {
        // R2: nunca un porcentaje sin denominador real.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx); // sin horas_en_puesto
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 70);

        (await ExcesoRelativoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Sin_umbral_propio_usa_el_valor_de_planta_como_denominador()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx); // sin horas_en_puesto
        await SetParametroAsync(ctx, "fatiga_sugerido_default_min", "50");
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 25);

        (await ExcesoRelativoAsync(puesto)).Should().BeApproximately(50m, 0.01m);
    }
}

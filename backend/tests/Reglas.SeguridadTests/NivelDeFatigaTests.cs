using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E7.3 (docs/PROGRESO.md): <c>fn_NivelFatiga</c> — los tres niveles
/// de §9.1 (normal / sugerido / crítico), "los operadores en puestos
/// fijos no entran en este cálculo" (§9.1, §5.1) y "la fatiga es
/// propiedad del puesto ocupado, no de la categoría de la persona"
/// (00 §A7). Mismo patrón de base descartable que
/// <see cref="RelojDeFatigaTests"/>/<see cref="ExcesoRelativoFatigaTests"/>.
/// </summary>
public class NivelDeFatigaTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

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

    // ═══ Helpers de datos ═══

    private static async Task<int> CrearPuestoAsync(
        SmartAssignDbContext ctx, string tipo = "rotativo", short? horasEnPuesto = null, short? umbralCriticoHoras = null)
    {
        var puesto = new Puesto
        {
            LineaId = 4, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba",
            Tipo = tipo, HorasEnPuesto = horasEnPuesto, UmbralCriticoHoras = umbralCriticoHoras,
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

    private static async Task AsignarDesdeHaceAsync(SmartAssignDbContext ctx, int puestoId, int minutosAtras, string categoria = "operario")
    {
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba", Categoria = categoria };
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

    private async Task<string?> NivelFatigaAsync(int puestoId)
    {
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var ctxCmd = conexion.CreateCommand())
        {
            ctxCmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await ctxCmd.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT dbo.fn_NivelFatiga(@p)";
        cmd.Parameters.AddWithValue("@p", puestoId);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is DBNull or null ? null : (string)resultado;
    }

    [Fact]
    public async Task Recien_asignado_y_lejos_de_ambos_umbrales_es_normal()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 2, umbralCriticoHoras: 3); // 120/180 min
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 10);

        (await NivelFatigaAsync(puesto)).Should().Be("normal");
    }

    [Fact]
    public async Task Al_cruzar_el_umbral_sugerido_pasa_a_sugerido()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 2, umbralCriticoHoras: 3); // 120/180 min
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 130);

        (await NivelFatigaAsync(puesto)).Should().Be("sugerido");
    }

    [Fact]
    public async Task Al_cruzar_el_umbral_critico_pasa_a_critico_no_se_queda_en_sugerido()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 2, umbralCriticoHoras: 3); // 120/180 min
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 200);

        (await NivelFatigaAsync(puesto)).Should().Be("critico");
    }

    [Fact]
    public async Task Exactamente_en_el_umbral_ya_cuenta_como_alcanzado()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 2, umbralCriticoHoras: 3); // 120/180 min
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 180);

        (await NivelFatigaAsync(puesto)).Should().Be("critico");
    }

    [Fact]
    public async Task Un_fijo_nunca_tiene_nivel_de_fatiga_aunque_lleve_horas_ocupado()
    {
        // §9.1/§5.1: "los operadores en puestos fijos no entran en este cálculo".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, tipo: "fijo", horasEnPuesto: 1, umbralCriticoHoras: 2);
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 500);

        (await NivelFatigaAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Un_rotativo_sin_nadie_asignado_no_tiene_nivel()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 1, umbralCriticoHoras: 2);

        (await NivelFatigaAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Sin_ningun_umbral_calibrado_no_hay_nivel_que_afirmar()
    {
        // R2: sin umbral propio ni de planta, ni siquiera "normal" se afirma.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx); // sin horas_en_puesto ni umbral_critico_horas
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 10);

        (await NivelFatigaAsync(puesto)).Should().BeNull();
    }

    [Theory]
    [InlineData("operario")]
    [InlineData("operador_b")]
    [InlineData("operador_c")]
    public async Task La_fatiga_es_del_puesto_no_de_la_categoria_de_quien_lo_ocupa(string categoria)
    {
        // 00 §A7: "cualquiera que ocupe un puesto rotativo acumula fatiga
        // y puede ser relevado — operario, Operador B u Operador C."
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, horasEnPuesto: 1); // 60 min
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 90, categoria);

        (await NivelFatigaAsync(puesto)).Should().Be("sugerido", "el nivel depende del puesto, nunca de quién lo ocupa");
    }
}

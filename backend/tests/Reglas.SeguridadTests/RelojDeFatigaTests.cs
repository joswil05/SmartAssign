using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E7.1 (docs/PROGRESO.md): <c>fn_MinutosEnPuesto</c> (fuente §9.1) y
/// <c>fn_UmbralFatigaSugeridoMinutos</c>/<c>fn_UmbralFatigaCriticoMinutos</c>
/// (00 §A4) — el reloj de fatiga y la resolución de su umbral propio,
/// contra la base real (mismo patrón que <see cref="SugerenciaDePuestoTests"/>).
/// </summary>
public class RelojDeFatigaTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

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
        SmartAssignDbContext ctx, string tipo = "rotativo", bool activo = true,
        short? horasEnPuesto = null, short? umbralCriticoHoras = null)
    {
        var puesto = new Puesto
        {
            LineaId = 4, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba",
            Tipo = tipo, Activo = activo, HorasEnPuesto = horasEnPuesto, UmbralCriticoHoras = umbralCriticoHoras,
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

    /// <summary>Deja a alguien ocupando el puesto desde hace exactamente <paramref name="minutosAtras"/> minutos.</summary>
    private static async Task AsignarDesdeHaceAsync(SmartAssignDbContext ctx, int puestoId, int minutosAtras)
    {
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba", Categoria = "operario" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();

        var jornada = new JornadaLinea { LineaId = 4, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();

        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada.Id, PuestoId = puestoId, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddMinutes(-minutosAtras), AsignadoPor = usuario,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SetParametroAsync(SmartAssignDbContext ctx, string clave, string valor)
    {
        ctx.Parametros.Add(new Parametro { Clave = clave, Valor = valor, Tipo = "int", Descripcion = "prueba" });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación de las funciones escalares ═══

    private async Task<int?> FnIntAsync(string nombreFuncion, int puestoId)
    {
        // fn_MinutosEnPuesto/fn_UmbralFatiga* leen Puesto, que tiene RLS
        // (04 §6.3) — sin contexto de coordinador la fila es invisible y
        // @tipo/@horas quedan NULL, no "fijo"/0, con lo que las
        // comparaciones IF fallan en silencio (UNKNOWN, no FALSE). Mismo
        // defecto real que ya apareció en E6.8 (ServicioAsignacion).
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var ctxCmd = conexion.CreateCommand())
        {
            ctxCmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await ctxCmd.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = $"SELECT dbo.{nombreFuncion}(@p)";
        cmd.Parameters.AddWithValue("@p", puestoId);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is DBNull or null ? null : (int)resultado;
    }

    private Task<int?> MinutosEnPuestoAsync(int puestoId) => FnIntAsync("fn_MinutosEnPuesto", puestoId);
    private Task<int?> UmbralSugeridoAsync(int puestoId) => FnIntAsync("fn_UmbralFatigaSugeridoMinutos", puestoId);
    private Task<int?> UmbralCriticoAsync(int puestoId) => FnIntAsync("fn_UmbralFatigaCriticoMinutos", puestoId);

    // ═══ fn_MinutosEnPuesto (§9.1) ═══

    [Fact]
    public async Task Un_fijo_nunca_tiene_reloj_de_fatiga_aunque_tenga_alguien_asignado()
    {
        // §9.1: "los operadores en puestos fijos no entran en este cálculo".
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "fijo");
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 500);

        (await MinutosEnPuestoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Un_rotativo_sin_nadie_asignado_no_tiene_reloj()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "rotativo");

        (await MinutosEnPuestoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Un_rotativo_inactivo_no_tiene_reloj_aunque_tenga_alguien_asignado()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "rotativo", activo: false);
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 30);

        (await MinutosEnPuestoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Un_rotativo_ocupado_mide_los_minutos_reales_desde_asignacion_inicio()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "rotativo");
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 90);

        var minutos = await MinutosEnPuestoAsync(puesto);
        minutos.Should().BeInRange(89, 91, "el reloj corre entre crear la fila y consultar la función");
    }

    // ═══ fn_UmbralFatigaSugeridoMinutos / fn_UmbralFatigaCriticoMinutos (00 §A4) ═══

    [Fact]
    public async Task Un_fijo_nunca_tiene_umbral_de_fatiga_ni_propio_ni_de_planta()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "fijo", horasEnPuesto: 2, umbralCriticoHoras: 3);
        await SetParametroAsync(ctx, "fatiga_sugerido_default_min", "60");
        await SetParametroAsync(ctx, "fatiga_critico_default_min", "90");

        (await UmbralSugeridoAsync(puesto)).Should().BeNull();
        (await UmbralCriticoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Umbral_propio_del_puesto_se_convierte_de_horas_a_minutos()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "rotativo", horasEnPuesto: 2, umbralCriticoHoras: 3);

        (await UmbralSugeridoAsync(puesto)).Should().Be(120);
        (await UmbralCriticoAsync(puesto)).Should().Be(180);
    }

    [Fact]
    public async Task Sin_umbral_propio_ni_parametro_de_planta_la_regla_no_aplica_todavia()
    {
        // 00 §A4 / R2: nunca un umbral inventado — la ausencia es un estado válido.
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "rotativo");

        (await UmbralSugeridoAsync(puesto)).Should().BeNull();
        (await UmbralCriticoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Sin_umbral_propio_cae_al_valor_de_planta_en_parametro()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "rotativo");
        await SetParametroAsync(ctx, "fatiga_sugerido_default_min", "60");
        await SetParametroAsync(ctx, "fatiga_critico_default_min", "90");

        (await UmbralSugeridoAsync(puesto)).Should().Be(60);
        (await UmbralCriticoAsync(puesto)).Should().Be(90);
    }

    [Fact]
    public async Task El_umbral_propio_del_puesto_gana_siempre_sobre_el_valor_de_planta()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, tipo: "rotativo", horasEnPuesto: 3, umbralCriticoHoras: 4);
        await SetParametroAsync(ctx, "fatiga_sugerido_default_min", "1");
        await SetParametroAsync(ctx, "fatiga_critico_default_min", "1");

        (await UmbralSugeridoAsync(puesto)).Should().Be(180);
        (await UmbralCriticoAsync(puesto)).Should().Be(240);
    }
}

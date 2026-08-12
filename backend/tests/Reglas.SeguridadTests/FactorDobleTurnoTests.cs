using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E7.4 (docs/PROGRESO.md): <c>fn_MinutosEnPuestoEfectivos</c> (00
/// §B7) — el factor de doble turno acelera la aparición de "sugerido" y
/// "crítico" para esa persona, con valor por defecto 1.0 (el único
/// default real de todo el motor de fatiga). Verifica también que
/// <c>fn_ExcesoRelativoFatiga</c>/<c>fn_NivelFatiga</c> (E7.2/E7.3) ya
/// leen el reloj efectivo, no el crudo. Mismo patrón de base descartable
/// que el resto de la suite de fatiga.
/// </summary>
public class FactorDobleTurnoTests : IAsyncLifetime
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

    private static async Task AsignarDesdeHaceAsync(SmartAssignDbContext ctx, int puestoId, int minutosAtras, bool dobleTurno = false)
    {
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = "operario", DobleTurno = dobleTurno,
        };
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
        ctx.Parametros.Add(new Parametro { Clave = clave, Valor = valor, Tipo = "decimal", Descripcion = "prueba" });
        await ctx.SaveChangesAsync();
    }

    private async Task<int?> MinutosEfectivosAsync(int puestoId)
    {
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var ctxCmd = conexion.CreateCommand())
        {
            ctxCmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await ctxCmd.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT dbo.fn_MinutosEnPuestoEfectivos(@p)";
        cmd.Parameters.AddWithValue("@p", puestoId);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is DBNull or null ? null : (int)resultado;
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
    public async Task Sin_doble_turno_el_efectivo_es_igual_al_crudo_aunque_haya_un_factor_configurado()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx);
        await SetParametroAsync(ctx, "factor_doble_turno", "2.0");
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 40, dobleTurno: false);

        (await MinutosEfectivosAsync(puesto)).Should().BeInRange(39, 41);
    }

    [Fact]
    public async Task Doble_turno_sin_parametro_sembrado_usa_el_default_de_uno_punto_cero()
    {
        // 00 §B7: el único default real del motor de fatiga.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx);
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 40, dobleTurno: true);

        (await MinutosEfectivosAsync(puesto)).Should().BeInRange(39, 41);
    }

    [Fact]
    public async Task Doble_turno_con_factor_configurado_multiplica_el_reloj()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx);
        await SetParametroAsync(ctx, "factor_doble_turno", "2.0");
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 40, dobleTurno: true);

        (await MinutosEfectivosAsync(puesto)).Should().BeInRange(78, 82, "40 min reales * factor 2.0");
    }

    [Fact]
    public async Task Un_fijo_no_tiene_reloj_efectivo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, tipo: "fijo");
        await SetParametroAsync(ctx, "factor_doble_turno", "3.0");
        await AsignarDesdeHaceAsync(ctx, puesto, minutosAtras: 40, dobleTurno: true);

        (await MinutosEfectivosAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Un_rotativo_sin_nadie_asignado_no_tiene_reloj_efectivo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx);

        (await MinutosEfectivosAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task El_factor_de_doble_turno_adelanta_el_nivel_de_sugerido_frente_a_alguien_sin_doble_turno()
    {
        // 00 §B7: "acelera la aparición de sugerido y crítico para esa
        // persona" — mismo puesto, mismos minutos reales, distinto nivel.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        await SetParametroAsync(ctx, "factor_doble_turno", "2.0");

        var puestoNormal = await CrearPuestoAsync(ctx, horasEnPuesto: 1); // umbral 60 min
        var puestoDobleTurno = await CrearPuestoAsync(ctx, horasEnPuesto: 1);
        await AsignarDesdeHaceAsync(ctx, puestoNormal, minutosAtras: 40, dobleTurno: false);
        await AsignarDesdeHaceAsync(ctx, puestoDobleTurno, minutosAtras: 40, dobleTurno: true); // efectivo ~80 min

        (await NivelFatigaAsync(puestoNormal)).Should().Be("normal");
        (await NivelFatigaAsync(puestoDobleTurno)).Should().Be("sugerido");
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.3 (docs/PROGRESO.md): <c>fn_TextoAvisoFatiga</c> — el contenido
/// exacto del aviso de fatiga, literal 00 §D2: "ninguna identidad de
/// persona, ni en el aviso ni al abrirlo". Mismo patrón de base
/// descartable que el resto de la suite de fatiga (E7).
/// </summary>
public class AvisoDeFatigaSinIdentidadTests : IAsyncLifetime
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

    private static async Task<int> CrearPuestoAsync(
        SmartAssignDbContext ctx, byte lineaId, string nombrePuesto, string tipo = "rotativo",
        short? horasEnPuesto = 1, short? umbralCriticoHoras = null)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = nombrePuesto, Tipo = tipo, HorasEnPuesto = horasEnPuesto, UmbralCriticoHoras = umbralCriticoHoras,
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

    private static async Task<string> OcuparPuestoAsync(
        SmartAssignDbContext ctx, int puestoId, byte lineaId, int usuarioId, int minutosAtras, bool dobleTurno = false)
    {
        var jornadaId = await JornadaAbiertaAsync(ctx, lineaId);
        var persona = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Nombre Secreto De Prueba",
            Categoria = "operario", DobleTurno = dobleTurno,
        };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaId, PuestoId = puestoId, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddMinutes(-minutosAtras), AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
        return persona.NombreCompleto;
    }

    private static async Task SetParametroAsync(SmartAssignDbContext ctx, string clave, string valor)
    {
        ctx.Parametros.Add(new Parametro { Clave = clave, Valor = valor, Tipo = "decimal", Descripcion = "prueba" });
        await ctx.SaveChangesAsync();
    }

    private async Task<string?> TextoAvisoAsync(int puestoId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT dbo.fn_TextoAvisoFatiga(@p)";
        cmd.Parameters.AddWithValue("@p", puestoId);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is DBNull or null ? null : (string)resultado;
    }

    /// <summary>El minuto en el texto viene de un DATEDIFF contra SYSUTCDATETIME() en el momento de la consulta —
    /// bajo la suite completa en paralelo puede correr uno o dos minutos reales desde que se sembró
    /// el "hace N minutos" en C#. Mismo criterio de tolerancia que ExcesoRelativoFatigaTests (E7.2).</summary>
    private static int ExtraerMinuto(string texto) =>
        int.Parse(System.Text.RegularExpressions.Regex.Match(texto, @"(\d+) min$").Groups[1].Value);

    [Fact]
    public async Task Reproduce_el_ejemplo_normativo_exacto_de_00_D2()
    {
        // 00 §D2, literal: "L4 · Puesto 3 — relevo sugerido · 62 min".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, nombrePuesto: "Puesto 3", horasEnPuesto: 1); // umbral: 60 min
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 62);

        var texto = await TextoAvisoAsync(puesto);

        texto.Should().StartWith("L4 · Puesto 3 — relevo sugerido · ").And.EndWith(" min");
        ExtraerMinuto(texto!).Should().BeInRange(59, 65);
    }

    [Fact]
    public async Task Nivel_critico_dice_relevo_critico()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 1, nombrePuesto: "Puesto 7", horasEnPuesto: 1, umbralCriticoHoras: 3); // crítico: 180 min
        await OcuparPuestoAsync(ctx, puesto, lineaId: 1, usuario, minutosAtras: 300);

        var texto = await TextoAvisoAsync(puesto);

        texto.Should().StartWith("L1 · Puesto 7 — relevo crítico · ").And.EndWith(" min");
        ExtraerMinuto(texto!).Should().BeInRange(297, 303);
    }

    [Fact]
    public async Task Un_puesto_sin_fatiga_todavia_no_genera_aviso()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, nombrePuesto: "Puesto 1", horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 5);

        (await TextoAvisoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task Un_puesto_fijo_nunca_genera_aviso_de_fatiga()
    {
        // §9.1: "la fatiga solo aplica a puestos rotativos".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, nombrePuesto: "Puesto Fijo", tipo: "fijo", horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 500);

        (await TextoAvisoAsync(puesto)).Should().BeNull();
    }

    [Fact]
    public async Task El_aviso_nunca_incluye_el_nombre_de_la_persona()
    {
        // 00 §D2, literal: "ninguna identidad de persona, ni en el aviso ni al abrirlo".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, nombrePuesto: "Puesto 5", horasEnPuesto: 1);
        var nombreOcupante = await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 70);

        var texto = await TextoAvisoAsync(puesto);

        texto.Should().NotBeNull();
        texto.Should().NotContain(nombreOcupante);
        texto.Should().NotContain("Secreto");
    }

    [Fact]
    public async Task El_minuto_del_aviso_es_el_reloj_crudo_no_el_efectivo_de_doble_turno()
    {
        // 00 §D2 + E7.4: el mismo "62 min" que motivó separar fn_MinutosEnPuesto
        // de fn_MinutosEnPuestoEfectivos — el aviso siempre usa el literal.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        await SetParametroAsync(ctx, "factor_doble_turno", "2.0");
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, nombrePuesto: "Puesto 9", horasEnPuesto: 1);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario, minutosAtras: 62, dobleTurno: true); // efectivo sería ~124

        var texto = await TextoAvisoAsync(puesto);

        // Efectivo sería ~124 (factor 2.0) — el crudo se queda cerca de 62,
        // muy lejos de eso, sin exigir el minuto exacto bajo carga.
        ExtraerMinuto(texto!).Should().BeInRange(59, 65);
    }
}

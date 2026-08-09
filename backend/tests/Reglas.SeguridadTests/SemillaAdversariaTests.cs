using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Semillas;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E3.5 (docs/PROGRESO.md): "una prueba por escenario confirma que
/// existe en la semilla" — los 16 escenarios de 07 §4.4, uno por
/// método. Sobre una base descartable con datos reales mínimos (para que
/// las re-etiquetas de Operador B/C de 00 §G1 tengan algo real sobre lo
/// que actuar) + la semilla adversaria completa.
/// </summary>
public class SemillaAdversariaTests : IAsyncLifetime
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

    private int _usuarioId;

    public async Task InitializeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
        await ComoCoordinadorAsync(ctx);

        var usuario = new Usuario { Username = "u_semilla", NombreCompleto = "Usuario Prueba", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        _usuarioId = usuario.Id;

        // Suficiente padrón real mínimo para que G1 (Operador B/C) y B5
        // (piso de seguridad) tengan operarios reales sobre los que actuar
        // en cada línea que la semilla toca.
        var lineas = new byte[] { 1, 2, 4, 6, 8 };
        var contadorFicha = 1;
        foreach (var lineaId in lineas)
        {
            for (var i = 0; i < 6; i++)
            {
                ctx.Personas.Add(new Personal
                {
                    Ficha = $"R{contadorFicha:D4}",
                    NombreCompleto = $"Operario Real {contadorFicha}",
                    Categoria = "operario",
                    LineaHabitual = lineaId,
                });
                contadorFicha++;
            }
            // Al menos un Operador A real por línea, para el escenario 14/15 (titular ausente).
            ctx.Personas.Add(new Personal
            {
                Ficha = $"A{lineaId:D3}",
                NombreCompleto = $"Operador A de L{lineaId}",
                Categoria = "operador_a",
                LineaHabitual = lineaId,
            });
        }
        await ctx.SaveChangesAsync();

        var sembrador = new SembradorAdversario(ctx);
        await sembrador.SembrarAsync(_usuarioId);
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    private async Task<SmartAssignDbContext> ContextoCoordinadorAsync()
    {
        var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task Escenario_01_restriccion_vigente_choca_con_puesto_habitual()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var persona = await ctx.Personas.SingleAsync(p => p.Ficha == "SIM-0001");
        var restriccion = await ctx.RestriccionesMedicas.SingleAsync(r => r.PersonalId == persona.Id);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        restriccion.FechaInicio.Should().BeOnOrBefore(hoy);
        (restriccion.FechaFin is null || restriccion.FechaFin >= hoy).Should().BeTrue("debe estar vigente hoy");
        restriccion.OrigenDato.Should().Be("simulado");
    }

    [Fact]
    public async Task Escenario_02_restriccion_caducada_no_debe_bloquear()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var persona = await ctx.Personas.SingleAsync(p => p.Ficha == "SIM-0002");
        var restriccion = await ctx.RestriccionesMedicas.SingleAsync(r => r.PersonalId == persona.Id);

        restriccion.FechaFin.Should().NotBeNull();
        restriccion.FechaFin!.Value.Should().BeBefore(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task Escenario_03_restriccion_permanente_no_tiene_fecha_fin()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var persona = await ctx.Personas.SingleAsync(p => p.Ficha == "SIM-0003");
        var restriccion = await ctx.RestriccionesMedicas.SingleAsync(r => r.PersonalId == persona.Id);

        restriccion.FechaFin.Should().BeNull("00 §C14: NULL = permanente");
    }

    [Fact]
    public async Task Escenario_04_restriccion_futura_todavia_no_vigente()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var persona = await ctx.Personas.SingleAsync(p => p.Ficha == "SIM-0004");
        var restriccion = await ctx.RestriccionesMedicas.SingleAsync(r => r.PersonalId == persona.Id);

        restriccion.FechaInicio.Should().BeAfter(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task Escenario_05_puesto_con_fatiga_sugerida_propia()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var puesto = await ctx.Puestos.SingleAsync(p => p.Codigo == "SIM-P01");
        puesto.HorasEnPuesto.Should().Be((short)2);
    }

    [Fact]
    public async Task Escenario_06_umbral_critico_valido_mayor_que_sugerido()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var puesto = await ctx.Puestos.SingleAsync(p => p.Codigo == "SIM-P02");
        puesto.UmbralCriticoHoras.Should().BeGreaterThan(puesto.HorasEnPuesto!.Value);
    }

    [Fact]
    public async Task Escenario_07_girar_botellas_con_24h_de_recuperacion()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var puesto = await ctx.Puestos.Include(p => p.TipoActividad).SingleAsync(p => p.Codigo == "SIM-P03");
        puesto.TipoActividad!.Nombre.Should().Be("Girar botellas");
        puesto.HorasRecuperacion.Should().Be((short)24, "00 §A12");
    }

    [Fact]
    public async Task Escenario_08_limpieza_con_48h_de_recuperacion()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var puesto = await ctx.Puestos.Include(p => p.TipoActividad).SingleAsync(p => p.Codigo == "SIM-P04");
        puesto.TipoActividad!.Nombre.Should().Be("Limpieza");
        puesto.HorasRecuperacion.Should().Be((short)48, "00 §A12");
    }

    [Fact]
    public async Task Escenario_09_sexo_de_la_persona_no_coincide_con_el_preferente_del_puesto()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var persona = await ctx.Personas.SingleAsync(p => p.Ficha == "SIM-0005");
        var puesto = await ctx.Puestos.SingleAsync(p => p.Codigo == "SIM-P05");

        persona.Sexo.Should().NotBeNull();
        puesto.SexoPreferente.Should().NotBeNull();
        persona.Sexo!.Should().NotBe(puesto.SexoPreferente!.ToLowerInvariant());
    }

    [Fact]
    public async Task Escenario_10_sexo_nulo_no_se_evalua()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var persona = await ctx.Personas.SingleAsync(p => p.Ficha == "SIM-0006");
        persona.Sexo.Should().BeNull("00 §A13/§7.3: nulo significa 'no evaluar'");
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    [InlineData((byte)8)]
    public async Task Escenario_11_hay_al_menos_un_operador_b_simulado_por_linea_activa(byte lineaId)
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var hay = await ctx.Personas.AnyAsync(p =>
            p.LineaHabitual == lineaId && p.Categoria == "operador_b" && p.OrigenDato == "simulado_categoria");
        hay.Should().BeTrue($"00 §G1: debe haber al menos un Operador B disponible en L{lineaId}");
    }

    [Fact]
    public async Task Escenario_12_hay_al_menos_un_operador_c_simulado()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var hay = await ctx.Personas.AnyAsync(p => p.Categoria == "operador_c" && p.OrigenDato == "simulado_categoria");
        hay.Should().BeTrue("00 §G1");
    }

    [Fact]
    public async Task Escenario_13_L6_no_tiene_ningun_operador_b_re_etiquetado()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var hay = await ctx.Personas.AnyAsync(p => p.LineaHabitual == 6 && p.Categoria == "operador_b");
        hay.Should().BeFalse("déficit deliberado — fuerza vacante crítica en E10");
    }

    [Fact]
    public async Task Escenario_14_titular_ausente_en_L1_con_suplente_operador_b_en_la_misma_linea()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var puesto = await ctx.Puestos.Include(p => p.Titular).SingleAsync(p => p.Codigo == "SIM-P06");

        puesto.Titular.Should().NotBeNull();
        puesto.Titular!.Situacion.Should().Be("ausente_justificado");

        var haySuplente = await ctx.Personas.AnyAsync(p => p.LineaHabitual == 1 && p.Categoria == "operador_b");
        haySuplente.Should().BeTrue();
    }

    [Fact]
    public async Task Escenario_15_titular_ausente_en_L6_sin_suplente_operador_b_en_su_linea()
    {
        await using var ctx = await ContextoCoordinadorAsync();
        var puesto = await ctx.Puestos.Include(p => p.Titular).SingleAsync(p => p.Codigo == "SIM-P07");

        puesto.Titular.Should().NotBeNull();
        puesto.Titular!.Situacion.Should().Be("ausente_justificado");

        var haySuplente = await ctx.Personas.AnyAsync(p => p.LineaHabitual == 6 && p.Categoria == "operador_b");
        haySuplente.Should().BeFalse("00 §G1/§C15: sin Operador B en su línea");
    }

    [Fact]
    public async Task Escenario_16_una_linea_en_su_piso_minimo_y_otra_por_encima()
    {
        await using var ctx = await ContextoCoordinadorAsync();

        var lineaEnMinimo = await ctx.Lineas.SingleAsync(l => l.Id == 2);
        var asignadosL2 = await ctx.Personas.CountAsync(p => p.LineaFisicaActual == 2 && p.Situacion == "asignado");
        lineaEnMinimo.MinimoOperarios.Should().Be((short)3);
        asignadosL2.Should().Be(3, "00 §B5: exactamente en el mínimo");

        var lineaPorEncima = await ctx.Lineas.SingleAsync(l => l.Id == 4);
        var asignadosL4 = await ctx.Personas.CountAsync(p => p.LineaFisicaActual == 4 && p.Situacion == "asignado");
        lineaPorEncima.MinimoOperarios.Should().Be((short)3);
        asignadosL4.Should().BeGreaterThan((int)lineaPorEncima.MinimoOperarios!.Value, "00 §B5: una persona por encima del mínimo");
    }

    [Fact]
    public async Task Ninguna_fila_simulada_queda_marcada_como_real()
    {
        // 07 §4.4: "hay una prueba que falla si aparece una sola en la
        // base de producción" — aquí, su equivalente estructural: toda
        // fila que la semilla creó debe llevar la marca de origen
        // correcta, nunca 'real'.
        await using var ctx = await ContextoCoordinadorAsync();

        var personasSimuladas = await ctx.Personas.Where(p => p.Ficha.StartsWith("SIM-")).ToListAsync();
        personasSimuladas.Should().NotBeEmpty();
        personasSimuladas.Should().OnlyContain(p => p.OrigenDato == "simulado");

        var restricciones = await ctx.RestriccionesMedicas.ToListAsync();
        restricciones.Should().NotBeEmpty();
        restricciones.Should().OnlyContain(r => r.OrigenDato == "simulado");

        var reetiquetados = await ctx.Personas.Where(p => p.OrigenDato == "simulado_categoria").ToListAsync();
        reetiquetados.Should().NotBeEmpty();
        reetiquetados.Should().OnlyContain(p => p.Categoria == "operador_b" || p.Categoria == "operador_c");
    }
}

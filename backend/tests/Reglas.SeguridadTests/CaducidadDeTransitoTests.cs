using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E8.6 (docs/PROGRESO.md): <c>sp_CaducarTransitos</c> — cierra E8.
/// Literal 00 §B11: "la caducidad convierte un cuelgue silencioso en una
/// alerta accionable, sin mover a nadie". Mismo patrón de base
/// descartable que el resto de la familia de E8.
/// </summary>
public class CaducidadDeTransitoTests : IAsyncLifetime
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

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, byte lineaFisicaActual)
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = "operario", LineaFisicaActual = lineaFisicaActual, Situacion = "en_transito",
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
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

    /// <summary>Inserta un Movimiento con hora_salida controlada — sp_DespacharPersona siempre usa SYSUTCDATETIME(), no permite "hace N minutos".</summary>
    private async Task<long> InsertarMovimientoAsync(
        SmartAssignDbContext ctx, int personalId, int usuarioId, int minutosAtras, string estado = "en_transito")
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, estado, hora_salida, hora_llegada, despachado_por)
            OUTPUT INSERTED.Id
            VALUES (@personal_id, 4, 8, 'relevo', @estado,
                    DATEADD(MINUTE, -@minutos, SYSUTCDATETIME()),
                    CASE WHEN @estado = 'en_transito' THEN NULL ELSE SYSUTCDATETIME() END,
                    @usuario_id);
            """;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@estado", estado);
        cmd.Parameters.AddWithValue("@minutos", minutosAtras);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var resultado = await cmd.ExecuteScalarAsync();
        return (long)resultado!;
    }

    private static async Task SetParametroAsync(SmartAssignDbContext ctx, string clave, string valor)
    {
        ctx.Parametros.Add(new Parametro { Clave = clave, Valor = valor, Tipo = "int", Descripcion = "prueba" });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación del SP ═══

    private async Task<int> CaducarTransitosAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CaducarTransitos";
        cmd.CommandType = CommandType.StoredProcedure;
        var pCaducados = new SqlParameter("@caducados", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCaducados);
        await cmd.ExecuteNonQueryAsync();
        return (int)pCaducados.Value;
    }

    [Fact]
    public async Task Un_transito_reciente_no_caduca()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var movimiento = await InsertarMovimientoAsync(ctx, persona, usuario, minutosAtras: 5); // < 15 min default

        var caducados = await CaducarTransitosAsync();

        caducados.Should().Be(0);
        var fila = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == movimiento);
        fila.CaducadoEn.Should().BeNull();
        fila.Estado.Should().Be("en_transito");
    }

    [Fact]
    public async Task Un_transito_que_supera_el_umbral_por_defecto_de_15_minutos_caduca()
    {
        // 00 §B11: "valor inicial provisional: 15 minutos" — sin Parametro sembrado.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var movimiento = await InsertarMovimientoAsync(ctx, persona, usuario, minutosAtras: 16);
        var antes = DateTime.UtcNow;

        var caducados = await CaducarTransitosAsync();

        caducados.Should().Be(1);
        var fila = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == movimiento);
        fila.CaducadoEn.Should().NotBeNull().And.BeOnOrAfter(antes.AddSeconds(-2));
    }

    [Fact]
    public async Task Caducar_no_mueve_a_nadie_estado_y_situacion_quedan_intactos()
    {
        // 00 §B11, literal: "sin mover a nadie" — ni estado ni situación cambian.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var movimiento = await InsertarMovimientoAsync(ctx, persona, usuario, minutosAtras: 30);

        await CaducarTransitosAsync();

        var fila = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == movimiento);
        fila.Estado.Should().Be("en_transito", "caducar no resuelve el tránsito, solo lo marca");

        var personaTras = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona);
        personaTras.Situacion.Should().Be("en_transito", "sigue caminando — nadie decidió nada por ella todavía");
    }

    [Fact]
    public async Task Un_umbral_configurado_por_parametro_reemplaza_al_default_de_15()
    {
        await using var ctx = CrearContexto();
        await SetParametroAsync(ctx, "duracion_maxima_transito", "30");
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var movimiento = await InsertarMovimientoAsync(ctx, persona, usuario, minutosAtras: 20); // >15 (default) pero <30 (configurado)

        var caducados = await CaducarTransitosAsync();

        caducados.Should().Be(0, "con el parámetro en 30, 20 minutos todavía no alcanzan");
        var fila = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == movimiento);
        fila.CaducadoEn.Should().BeNull();
    }

    [Fact]
    public async Task Un_movimiento_ya_resuelto_nunca_caduca_sin_importar_cuanto_tiempo_paso()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var movimiento = await InsertarMovimientoAsync(ctx, persona, usuario, minutosAtras: 999, estado: "recibido");

        var caducados = await CaducarTransitosAsync();

        caducados.Should().Be(0);
        var fila = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == movimiento);
        fila.CaducadoEn.Should().BeNull();
    }

    [Fact]
    public async Task Correr_la_caducidad_dos_veces_no_recuenta_lo_ya_caducado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        await InsertarMovimientoAsync(ctx, persona, usuario, minutosAtras: 20);
        (await CaducarTransitosAsync()).Should().Be(1);

        var segunda = await CaducarTransitosAsync();

        segunda.Should().Be(0, "ya quedó marcado — sp_CaducarTransitos es idempotente por diseño (caducado_en IS NULL)");
    }

    [Fact]
    public async Task Varios_transitos_vencidos_a_la_vez_se_cuentan_todos()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var vencido1 = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var vencido2 = await CrearPersonaAsync(ctx, lineaFisicaActual: 1);
        var aTiempo = await CrearPersonaAsync(ctx, lineaFisicaActual: 2);
        await InsertarMovimientoAsync(ctx, vencido1, usuario, minutosAtras: 20);
        await InsertarMovimientoAsync(ctx, vencido2, usuario, minutosAtras: 45);
        await InsertarMovimientoAsync(ctx, aTiempo, usuario, minutosAtras: 3);

        var caducados = await CaducarTransitosAsync();

        caducados.Should().Be(2);
    }
}

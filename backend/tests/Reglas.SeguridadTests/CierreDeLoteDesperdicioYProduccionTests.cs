using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E11.6 (docs/PROGRESO.md): desperdicio + producción + justificación
/// sobre umbral, al cierre de lote (§11.3, 00 §C4, HU-F2). Ninguna de las
/// tablas que toca <c>sp_CerrarLote</c> (<c>Lote</c>, <c>Desperdicio</c>,
/// <c>Parametro</c>) lleva RLS — solo <c>Puesto</c>/<c>JornadaLinea</c> la
/// llevan — así que estas pruebas no necesitan `SESSION_CONTEXT`.
/// </summary>
public class CierreDeLoteDesperdicioYProduccionTests : IAsyncLifetime
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

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx)
    {
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = "supervisor", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    /// <summary>Lote abierto, listo para cerrarse — mismo atajo que <c>JornadaConSkuYLoteAsync</c> de E11.5, sin recorrer todo el pipeline de arranque.</summary>
    private static async Task<int> LoteAbiertoAsync(SmartAssignDbContext ctx, byte lineaId = 4)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100, Activo = true };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();

        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1), SkuId = sku.Id, Estado = "arrancada", ArrancadoEn = DateTime.UtcNow };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();

        var lote = new Lote { JornadaLineaId = jornada.Id, SkuId = sku.Id, Numero = 1 };
        ctx.Lotes.Add(lote);
        await ctx.SaveChangesAsync();
        return lote.Id;
    }

    private static async Task SembrarUmbralAsync(SmartAssignDbContext ctx, decimal pct)
    {
        ctx.Parametros.Add(new Parametro
        {
            Clave = "umbral_desperdicio_justificacion_pct",
            Valor = pct.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Tipo = "decimal",
            Descripcion = "prueba"
        });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación de sp_CerrarLote ═══

    private record ResultadoCerrarLote(int? DesperdicioId, string? Codigo, string? Mensaje);

    private async Task<ResultadoCerrarLote> CerrarLoteAsync(
        int loteId, decimal produccionReal, decimal danoOrigen, decimal danoProceso, string? justificacion, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CerrarLote";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@lote_id", loteId);
        cmd.Parameters.AddWithValue("@produccion_real", produccionReal);
        cmd.Parameters.AddWithValue("@dano_origen", danoOrigen);
        cmd.Parameters.AddWithValue("@dano_proceso", danoProceso);
        cmd.Parameters.AddWithValue("@justificacion", (object?)justificacion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pDesperdicio = new SqlParameter("@desperdicio_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pDesperdicio);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoCerrarLote(pDesperdicio.Value as int?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Cerrar_el_lote_guarda_la_produccion_real_y_lo_marca_cerrado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 5, danoProceso: 3, justificacion: null, usuario);

        resultado.Codigo.Should().BeNull();
        var loteDb = await ctx.Lotes.AsNoTracking().SingleAsync(l => l.Id == lote);
        loteDb.ProduccionReal.Should().Be(500m);
        loteDb.CerradoEn.Should().NotBeNull();
    }

    [Fact]
    public async Task Cerrar_el_lote_crea_el_registro_de_desperdicio_con_las_dos_causas_separadas()
    {
        // §11.3, literal: "separado en dos causas" — cada una en su propia columna.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 12.5m, danoProceso: 7.25m, justificacion: null, usuario);

        resultado.DesperdicioId.Should().NotBeNull();
        var desperdicio = await ctx.Desperdicios.AsNoTracking().SingleAsync(d => d.Id == resultado.DesperdicioId);
        desperdicio.LoteId.Should().Be(lote);
        desperdicio.DanoOrigen.Should().Be(12.5m);
        desperdicio.DanoProceso.Should().Be(7.25m);
        desperdicio.RegistradoPor.Should().Be(usuario);
        desperdicio.Justificacion.Should().BeNull();
    }

    [Fact]
    public async Task Sin_umbral_configurado_el_daño_de_proceso_nunca_exige_justificacion()
    {
        // "a definir" en 04 §9 — sin sembrar, no hay con qué comparar (R2, mismo
        // criterio que fn_ExcesoRelativoFatiga con un umbral de fatiga sin definir).
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        // 100 % del desperdicio es daño de proceso, y aun así pasa.
        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 0, danoProceso: 50, justificacion: null, usuario);

        resultado.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task Con_umbral_configurado_y_el_dano_de_proceso_por_encima_sin_justificacion_se_rechaza()
    {
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, 20m);
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        // 90 % del desperdicio total es proceso — por encima del 20 % configurado.
        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 10, danoProceso: 90, justificacion: null, usuario);

        resultado.Codigo.Should().Be("JUSTIFICACION_REQUERIDA");
        resultado.DesperdicioId.Should().BeNull();

        var loteDb = await ctx.Lotes.AsNoTracking().SingleAsync(l => l.Id == lote);
        loteDb.CerradoEn.Should().BeNull("el rechazo no debe dejar el lote a medio cerrar");
        (await ctx.Desperdicios.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_umbral_configurado_y_el_dano_de_proceso_por_encima_con_justificacion_se_permite_y_la_guarda_tal_cual()
    {
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, 20m);
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 10, danoProceso: 90,
            justificacion: "Rodillo descalibrado desde el turno anterior, mantenimiento ya avisado.", usuario);

        resultado.Codigo.Should().BeNull();
        var desperdicio = await ctx.Desperdicios.AsNoTracking().SingleAsync(d => d.Id == resultado.DesperdicioId);
        desperdicio.Justificacion.Should().Be("Rodillo descalibrado desde el turno anterior, mantenimiento ya avisado.");
    }

    [Fact]
    public async Task Una_justificacion_de_solo_espacios_no_cuenta_como_justificacion_real()
    {
        // Mismo criterio que CK_Paro_descripcion (E11.1): LTRIM/RTRIM antes de medir.
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, 20m);
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 10, danoProceso: 90, justificacion: "   ", usuario);

        resultado.Codigo.Should().Be("JUSTIFICACION_REQUERIDA");
    }

    [Fact]
    public async Task Con_umbral_configurado_y_el_dano_de_proceso_por_debajo_no_exige_justificacion()
    {
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, 20m);
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        // 10 % del desperdicio total es proceso — por debajo del 20 % configurado.
        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 90, danoProceso: 10, justificacion: null, usuario);

        resultado.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task El_porcentaje_se_calcula_sobre_el_desperdicio_total_no_sobre_la_produccion()
    {
        // Decisión confirmada con el cliente (ver docstring de la migración):
        // el denominador es daño_origen + daño_proceso, nunca producción_real.
        // Con producción real enorme, el % de desperdicio "sobre producción"
        // sería insignificante — pero sobre el desperdicio total sigue siendo 100 %.
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, 20m);
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        var resultado = await CerrarLoteAsync(lote, produccionReal: 1_000_000, danoOrigen: 0, danoProceso: 1, justificacion: null, usuario);

        resultado.Codigo.Should().Be("JUSTIFICACION_REQUERIDA");
    }

    [Fact]
    public async Task Un_lote_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var resultado = await CerrarLoteAsync(999_999, 500, 0, 0, null, usuario);

        resultado.Codigo.Should().Be("LOTE_INEXISTENTE");
    }

    [Fact]
    public async Task Un_lote_ya_cerrado_no_se_puede_cerrar_de_nuevo()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);
        await CerrarLoteAsync(lote, 500, 5, 3, null, usuario);

        var segundoIntento = await CerrarLoteAsync(lote, 100, 1, 1, null, usuario);

        segundoIntento.Codigo.Should().Be("LOTE_YA_CERRADO");
        (await ctx.Desperdicios.CountAsync()).Should().Be(1, "el segundo intento no debe crear un segundo registro");
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(500, -1, 0)]
    [InlineData(500, 0, -1)]
    public async Task Valores_negativos_se_rechazan(decimal produccion, decimal danoOrigen, decimal danoProceso)
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        var resultado = await CerrarLoteAsync(lote, produccion, danoOrigen, danoProceso, null, usuario);

        resultado.Codigo.Should().Be("VALORES_NEGATIVOS");
    }

    [Fact]
    public async Task Sin_ningun_desperdicio_el_porcentaje_es_cero_y_no_exige_justificacion()
    {
        // 0/0 no debe reventar con división por cero.
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, 20m);
        var usuario = await CrearUsuarioAsync(ctx);
        var lote = await LoteAbiertoAsync(ctx);

        var resultado = await CerrarLoteAsync(lote, produccionReal: 500, danoOrigen: 0, danoProceso: 0, justificacion: null, usuario);

        resultado.Codigo.Should().BeNull();
    }
}

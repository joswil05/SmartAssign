using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E11.7 (docs/PROGRESO.md): eficiencia calculada en el servidor
/// (§11.4, 00 §C4, HU-F3). <c>sp_CalcularEficiencia</c> lee
/// <c>JornadaLinea</c>, que sí lleva RLS — la conexión de prueba necesita
/// `SESSION_CONTEXT('rol', ...)` antes de llamarlo, mismo patrón
/// recurrente ya visto en E11.2/E11.4 (sin contexto, la lectura no
/// devuelve filas y todo aguas abajo sale NULL, no un error explícito).
/// </summary>
public class EficienciaCalculadaEnElServidorTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private async Task<SqlConnection> AbrirComoCoordinadorAsync()
    {
        var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
        return conexion;
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

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx)
    {
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = "supervisor", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    /// <summary>
    /// Jornada-línea arrancada hace <paramref name="minutosArranque"/> minutos, con un SKU
    /// de ritmo teórico conocido. Devuelve <c>arrancadoEn</c> tal cual se escribió — releerlo
    /// después por EF chocaría con RLS (JornadaLinea la lleva, y `CrearContexto()` no fija
    /// `SESSION_CONTEXT`, a diferencia de <see cref="AbrirComoCoordinadorAsync"/>).
    /// </summary>
    private static async Task<(int jornadaId, int skuId, DateTime arrancadoEn)> JornadaArrancadaAsync(
        SmartAssignDbContext ctx, byte lineaId, int minutosArranque, decimal ritmoTeoricoHora = 100m, DateTime? cerradoEn = null)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = ritmoTeoricoHora, Activo = true };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();

        var arrancadoEn = DateTime.UtcNow.AddMinutes(-minutosArranque);
        var jornada = new JornadaLinea
        {
            LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1), SkuId = sku.Id,
            Estado = cerradoEn is null ? "arrancada" : "cerrada", ArrancadoEn = arrancadoEn, CerradoEn = cerradoEn
        };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return (jornada.Id, sku.Id, arrancadoEn);
    }

    private static async Task<int> LoteCerradoAsync(SmartAssignDbContext ctx, int jornadaId, int skuId, short numero, decimal produccionReal)
    {
        var lote = new Lote { JornadaLineaId = jornadaId, SkuId = skuId, Numero = numero, CerradoEn = DateTime.UtcNow, ProduccionReal = produccionReal };
        ctx.Lotes.Add(lote);
        await ctx.SaveChangesAsync();
        return lote.Id;
    }

    private static async Task<int> LoteAbiertoAsync(SmartAssignDbContext ctx, int jornadaId, int skuId, short numero)
    {
        var lote = new Lote { JornadaLineaId = jornadaId, SkuId = skuId, Numero = numero };
        ctx.Lotes.Add(lote);
        await ctx.SaveChangesAsync();
        return lote.Id;
    }

    private static async Task AvanceAsync(SmartAssignDbContext ctx, int loteId, int usuarioId, decimal cantidad)
    {
        ctx.ProduccionAvances.Add(new ProduccionAvance { LoteId = loteId, RegistradoPor = usuarioId, Cantidad = cantidad });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Paro sembrado directo (no vía sp_RegistrarParo) — para controlar `inicio`/`fin` exactos que la prueba necesita.</summary>
    private static async Task ParoAsync(SmartAssignDbContext ctx, int jornadaId, int usuarioId, DateTime inicio, DateTime? fin)
    {
        ctx.Paros.Add(new Paro
        {
            JornadaLineaId = jornadaId, CategoriaId = 1, CausaId = 1, Descripcion = "Paro de prueba",
            Inicio = inicio, Fin = fin, RegistradoPor = usuarioId
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SembrarUmbralAsync(SmartAssignDbContext ctx, string clave, decimal pct)
    {
        ctx.Parametros.Add(new Parametro
        {
            Clave = clave, Valor = pct.ToString(System.Globalization.CultureInfo.InvariantCulture), Tipo = "decimal", Descripcion = "prueba"
        });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación de sp_CalcularEficiencia ═══

    private record ResultadoEficiencia(
        decimal? EficienciaPct, string? Tramo, decimal? ProduccionReal, int? TiempoEfectivoMin,
        decimal? RitmoTeorico, DateTime? UltimaActualizacion, string? Codigo, string? Mensaje);

    private async Task<ResultadoEficiencia> CalcularEficienciaAsync(int jornadaLineaId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CalcularEficiencia";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        var pEficiencia = new SqlParameter("@eficiencia_pct", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 9, Scale = 4 };
        var pTramo = new SqlParameter("@tramo", SqlDbType.VarChar, 10) { Direction = ParameterDirection.Output };
        var pProduccion = new SqlParameter("@produccion_real", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 14, Scale = 2 };
        var pTiempo = new SqlParameter("@tiempo_efectivo_marcha_min", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pRitmo = new SqlParameter("@ritmo_teorico_hora", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 10, Scale = 2 };
        var pUltima = new SqlParameter("@ultima_actualizacion_produccion", SqlDbType.DateTime2) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pEficiencia);
        cmd.Parameters.Add(pTramo);
        cmd.Parameters.Add(pProduccion);
        cmd.Parameters.Add(pTiempo);
        cmd.Parameters.Add(pRitmo);
        cmd.Parameters.Add(pUltima);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoEficiencia(
            pEficiencia.Value as decimal?, pTramo.Value as string, pProduccion.Value as decimal?,
            pTiempo.Value as int?, pRitmo.Value as decimal?, pUltima.Value as DateTime?,
            pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Una_jornada_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();

        var resultado = await CalcularEficienciaAsync(999_999);

        resultado.Codigo.Should().Be("JORNADA_INEXISTENTE");
    }

    [Fact]
    public async Task Una_jornada_que_no_ha_arrancado_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU", RitmoTeoricoHora = 100, Activo = true };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = 4, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1), SkuId = sku.Id, Estado = "confirmada" };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();

        var resultado = await CalcularEficienciaAsync(jornada.Id);

        resultado.Codigo.Should().Be("JORNADA_NO_ARRANCADA");
    }

    [Fact]
    public async Task Sin_tiempo_efectivo_transcurrido_la_eficiencia_es_nula_no_cero()
    {
        // §12.4 (HU-F3, "honestidad del dato"): dividir entre cero no es un dato.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _, arrancadoEn) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 30);
        // Un paro abierto desde el mismísimo arranque consume TODO el tiempo transcurrido.
        await ParoAsync(ctx, jornada, usuario, arrancadoEn, fin: null);

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.Codigo.Should().BeNull();
        resultado.TiempoEfectivoMin.Should().Be(0);
        resultado.EficienciaPct.Should().BeNull();
        resultado.Tramo.Should().BeNull();
    }

    [Fact]
    public async Task La_produccion_de_lotes_ya_cerrados_se_suma()
    {
        await using var ctx = CrearContexto();
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 120, ritmoTeoricoHora: 100m);
        await LoteCerradoAsync(ctx, jornada, sku, 1, produccionReal: 50);
        await LoteCerradoAsync(ctx, jornada, sku, 2, produccionReal: 30);

        var resultado = await CalcularEficienciaAsync(jornada);

        // 2h × 100/h = 200 teórico; 80 real → 40 %.
        resultado.ProduccionReal.Should().Be(80m);
        resultado.TiempoEfectivoMin.Should().Be(120);
        resultado.RitmoTeorico.Should().Be(100m);
        resultado.EficienciaPct.Should().Be(40m);
    }

    [Fact]
    public async Task Los_avances_del_lote_todavia_abierto_se_suman_a_la_produccion()
    {
        // 00 §C4, literal: "para la lectura en vivo, registra avances parciales".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 100m);
        await LoteCerradoAsync(ctx, jornada, sku, 1, produccionReal: 50);
        var loteAbierto = await LoteAbiertoAsync(ctx, jornada, sku, 2);
        await AvanceAsync(ctx, loteAbierto, usuario, 10);
        await AvanceAsync(ctx, loteAbierto, usuario, 15);

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.ProduccionReal.Should().Be(75m); // 50 (cerrado) + 10 + 15 (avances)
    }

    [Fact]
    public async Task Los_avances_de_un_lote_ya_cerrado_no_se_cuentan_una_segunda_vez()
    {
        // Evita duplicar: la producción "oficial" del lote cerrado es Lote.produccion_real,
        // no la suma de sus avances (que pudo haber sido distinta al conteo final).
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 100m);
        var loteCerrado = await LoteCerradoAsync(ctx, jornada, sku, 1, produccionReal: 50);
        await AvanceAsync(ctx, loteCerrado, usuario, 999); // avance "huérfano" de un lote ya cerrado

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.ProduccionReal.Should().Be(50m);
    }

    [Fact]
    public async Task Un_paro_cerrado_descuenta_su_duracion_del_tiempo_efectivo()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _, arrancadoEn) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 120);
        var inicioParo = arrancadoEn.AddMinutes(10);
        await ParoAsync(ctx, jornada, usuario, inicioParo, fin: inicioParo.AddMinutes(20));

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.TiempoEfectivoMin.Should().Be(100); // 120 transcurridos − 20 de paro
    }

    [Fact]
    public async Task Un_paro_todavia_abierto_descuenta_hasta_ahora()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 30);
        await ParoAsync(ctx, jornada, usuario, DateTime.UtcNow.AddMinutes(-10), fin: null);

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.TiempoEfectivoMin.Should().Be(20); // 30 transcurridos − ~10 de paro abierto
    }

    [Fact]
    public async Task Una_jornada_ya_cerrada_no_sigue_acumulando_tiempo_efectivo()
    {
        // Arrancó hace 3h, cerró hace 1h — el turno "duró" 2h, no 3h hasta ahora.
        await using var ctx = CrearContexto();
        var (jornada, _, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 180, cerradoEn: DateTime.UtcNow.AddHours(-1));

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.TiempoEfectivoMin.Should().Be(120);
    }

    [Fact]
    public async Task El_ritmo_teorico_devuelto_es_el_del_sku_vigente_de_la_jornada()
    {
        await using var ctx = CrearContexto();
        var (jornada, _, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 137.5m);

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.RitmoTeorico.Should().Be(137.5m);
    }

    [Fact]
    public async Task Sin_ningun_umbral_configurado_el_tramo_no_se_clasifica()
    {
        await using var ctx = CrearContexto();
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 100m);
        await LoteCerradoAsync(ctx, jornada, sku, 1, produccionReal: 90);

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.EficienciaPct.Should().NotBeNull();
        resultado.Tramo.Should().BeNull();
    }

    [Fact]
    public async Task Por_debajo_del_umbral_aceptable_es_critico()
    {
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, "eficiencia_umbral_aceptable_pct", 60m);
        await SembrarUmbralAsync(ctx, "eficiencia_umbral_optimo_pct", 90m);
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 100m);
        await LoteCerradoAsync(ctx, jornada, sku, 1, produccionReal: 50); // 50 %

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.Tramo.Should().Be("critico");
    }

    [Fact]
    public async Task En_o_por_encima_del_umbral_optimo_es_optimo()
    {
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, "eficiencia_umbral_aceptable_pct", 60m);
        await SembrarUmbralAsync(ctx, "eficiencia_umbral_optimo_pct", 90m);
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 100m);
        await LoteCerradoAsync(ctx, jornada, sku, 1, produccionReal: 95); // 95 %

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.Tramo.Should().Be("optimo");
    }

    [Fact]
    public async Task Entre_los_dos_umbrales_es_aceptable()
    {
        await using var ctx = CrearContexto();
        await SembrarUmbralAsync(ctx, "eficiencia_umbral_aceptable_pct", 60m);
        await SembrarUmbralAsync(ctx, "eficiencia_umbral_optimo_pct", 90m);
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 100m);
        await LoteCerradoAsync(ctx, jornada, sku, 1, produccionReal: 75); // 75 %

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.Tramo.Should().Be("aceptable");
    }

    [Fact]
    public async Task La_ultima_actualizacion_de_produccion_es_el_registro_mas_reciente()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, sku, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60, ritmoTeoricoHora: 100m);
        var loteAbierto = await LoteAbiertoAsync(ctx, jornada, sku, 1);
        await AvanceAsync(ctx, loteAbierto, usuario, 10);

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.UltimaActualizacion.Should().NotBeNull();
        resultado.UltimaActualizacion!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Sin_ningun_registro_de_produccion_la_ultima_actualizacion_es_nula()
    {
        await using var ctx = CrearContexto();
        var (jornada, _, _) = await JornadaArrancadaAsync(ctx, 4, minutosArranque: 60);

        var resultado = await CalcularEficienciaAsync(jornada);

        resultado.UltimaActualizacion.Should().BeNull();
        resultado.ProduccionReal.Should().Be(0m);
    }
}

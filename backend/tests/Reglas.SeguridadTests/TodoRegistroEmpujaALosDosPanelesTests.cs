using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E11.8 (docs/PROGRESO.md), cierra E11 (8/8): "todo registro empuja a
/// los dos paneles" — 00 §C4. La difusión en vivo (SignalR + bandeja de
/// salida transaccional, C4 punto 1) es F10, no F9 — 06_ROADMAP.md lo dice
/// explícito: *"No incluye: Difusión en vivo (es F10)"*. E12.1/E12.3 no
/// existen todavía.
///
/// Lo que esta suite prueba es la garantía que SÍ es de esta etapa (C4
/// punto 3, criterio de salida de F9): **"el cálculo vive en el servidor
/// — los dos paneles nunca divergen"**. Cada prueba usa una conexión
/// NUEVA para leer, sin ningún estado compartido con la escritura, para
/// demostrar que no hay caché en ningún punto del camino — el supervisor
/// y el Coordinador, consultando en cualquier momento, ven exactamente
/// lo mismo porque preguntan al mismo cálculo sobre la misma base.
/// </summary>
public class TodoRegistroEmpujaALosDosPanelesTests : IAsyncLifetime
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

    /// <summary>Jornada arrancada (para sp_CalcularEficiencia) y todavía abierta (para sp_RegistrarParo/sp_CerrarLote).</summary>
    private static async Task<(int jornadaId, int skuId)> JornadaArrancadaYAbiertaAsync(SmartAssignDbContext ctx, byte lineaId, int minutosArranque = 60)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100m, Activo = true };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();

        var jornada = new JornadaLinea
        {
            LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1), SkuId = sku.Id,
            Estado = "arrancada", ArrancadoEn = DateTime.UtcNow.AddMinutes(-minutosArranque)
        };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        ctx.Lotes.Add(new Lote { JornadaLineaId = jornada.Id, SkuId = sku.Id, Numero = 1 });
        await ctx.SaveChangesAsync();
        return (jornada.Id, sku.Id);
    }

    private static async Task<(int personalId, int puestoId)> OcuparPuestoRotativoAsync(SmartAssignDbContext ctx, byte lineaId, int jornadaId, int usuarioId)
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba", Tipo = "rotativo" };
        ctx.Puestos.Add(puesto);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operario", Situacion = "asignado" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion { JornadaLineaId = jornadaId, PuestoId = puesto.Id, PersonalId = persona.Id, Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId });
        await ctx.SaveChangesAsync();
        return (persona.Id, puesto.Id);
    }

    // ═══ Invocación de sp_RegistrarParo (E11.1/E11.2) ═══

    private async Task<string?> RegistrarParoAsync(int jornadaLineaId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RegistrarParo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@categoria_id", (short)1);
        cmd.Parameters.AddWithValue("@causa_id", (short)1);
        cmd.Parameters.AddWithValue("@descripcion", "Descripción real del paro observado por el supervisor.");
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@paro_id", SqlDbType.Int) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@rotativos_liberados", SqlDbType.Int) { Direction = ParameterDirection.Output });
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return pCodigo.Value as string;
    }

    // ═══ Invocación de sp_CerrarLote (E11.6) ═══

    private async Task<int?> CerrarLoteAsync(int loteId, decimal produccionReal, decimal danoOrigen, decimal danoProceso, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CerrarLote";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@lote_id", loteId);
        cmd.Parameters.AddWithValue("@produccion_real", produccionReal);
        cmd.Parameters.AddWithValue("@dano_origen", danoOrigen);
        cmd.Parameters.AddWithValue("@dano_proceso", danoProceso);
        cmd.Parameters.AddWithValue("@justificacion", DBNull.Value);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pDesperdicio = new SqlParameter("@desperdicio_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pDesperdicio);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return pDesperdicio.Value as int?;
    }

    // ═══ Invocación de sp_CalcularEficiencia (E11.7/E11.8) — SIEMPRE con conexión nueva ═══

    private record ResultadoEficiencia(decimal? ProduccionReal, int? TiempoEfectivoMin, int? ParosAcumuladosMin, string? Codigo);

    private async Task<ResultadoEficiencia> CalcularEficienciaAsync(int jornadaLineaId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync(); // conexión NUEVA en cada llamada — a propósito.
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CalcularEficiencia";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.Add(new SqlParameter("@eficiencia_pct", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 9, Scale = 4 });
        cmd.Parameters.Add(new SqlParameter("@tramo", SqlDbType.VarChar, 10) { Direction = ParameterDirection.Output });
        var pProduccion = new SqlParameter("@produccion_real", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 14, Scale = 2 };
        var pTiempo = new SqlParameter("@tiempo_efectivo_marcha_min", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(new SqlParameter("@ritmo_teorico_hora", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 10, Scale = 2 });
        cmd.Parameters.Add(new SqlParameter("@ultima_actualizacion_produccion", SqlDbType.DateTime2) { Direction = ParameterDirection.Output });
        var pParos = new SqlParameter("@paros_acumulados_min", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(pProduccion);
        cmd.Parameters.Add(pTiempo);
        cmd.Parameters.Add(pParos);
        cmd.Parameters.Add(pCodigo);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoEficiencia(pProduccion.Value as decimal?, pTiempo.Value as int?, pParos.Value as int?, pCodigo.Value as string);
    }

    [Fact]
    public async Task Registrar_un_paro_expone_el_tiempo_de_paro_acumulado_del_turno_al_instante()
    {
        // 00 §C4: "tiempo de paro acumulado del turno" — uno de los cinco indicadores del panel.
        // El paro se siembra con `inicio` 10 min en el pasado (no vía sp_RegistrarParo, que lo
        // pondría en SYSUTCDATETIME() — a t=0 recién abierto, DATEDIFF(MINUTE,...) da 0 minutos
        // acumulados de verdad, no es un error, es el redondeo esperado) — para una prueba
        // determinista, no atada al reloj de pared. Escribe por EF, lee por el SP: dos caminos
        // de acceso distintos viendo la misma verdad, sin nada compartido entre ambos.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaYAbiertaAsync(ctx, 4, minutosArranque: 60);

        var antes = await CalcularEficienciaAsync(jornada);
        antes.ParosAcumuladosMin.Should().Be(0);

        ctx.Paros.Add(new Paro
        {
            JornadaLineaId = jornada, CategoriaId = 1, CausaId = 1, Descripcion = "Paro de prueba",
            Inicio = DateTime.UtcNow.AddMinutes(-10), RegistradoPor = usuario
        });
        await ctx.SaveChangesAsync();

        var despues = await CalcularEficienciaAsync(jornada); // conexión nueva — no hay "la misma sesión ya lo sabía"
        despues.ParosAcumuladosMin.Should().Be(10);
        despues.TiempoEfectivoMin.Should().Be(antes.TiempoEfectivoMin - 10);
    }

    [Fact]
    public async Task Registrar_un_paro_que_libera_un_rotativo_genera_un_movimiento_visible_al_instante_en_una_conexion_nueva()
    {
        // 00 §C4: "movimientos de personal" es uno de los cuatro tipos de registro que debe reflejarse.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaYAbiertaAsync(ctx, 4, minutosArranque: 30);
        var (ocupante, _) = await OcuparPuestoRotativoAsync(ctx, 4, jornada, usuario);

        await RegistrarParoAsync(jornada, usuario);

        // Lectura con un DbContext nuevo — nada compartido con la escritura.
        await using var lector = CrearContexto();
        var movimiento = await lector.Movimientos.AsNoTracking().SingleOrDefaultAsync(m => m.PersonalId == ocupante);
        movimiento.Should().NotBeNull("el rotativo liberado por el paro debe generar su propio tránsito, visible de inmediato");
        movimiento!.Motivo.Should().Be("paro");
        movimiento.LineaDestino.Should().Be((byte)8);
    }

    [Fact]
    public async Task Cerrar_un_lote_con_desperdicio_y_produccion_se_refleja_al_instante_en_sp_CalcularEficiencia()
    {
        // 00 §C4: "desperdicio" y "producción" son dos de los cuatro tipos de registro.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, sku) = await JornadaArrancadaYAbiertaAsync(ctx, 4, minutosArranque: 60);
        var lote = await ctx.Lotes.AsNoTracking().SingleAsync(l => l.JornadaLineaId == jornada);

        var antes = await CalcularEficienciaAsync(jornada);
        antes.ProduccionReal.Should().Be(0m);

        var desperdicioId = await CerrarLoteAsync(lote.Id, produccionReal: 42, danoOrigen: 3, danoProceso: 1, usuario);
        desperdicioId.Should().NotBeNull();

        var despues = await CalcularEficienciaAsync(jornada);
        despues.ProduccionReal.Should().Be(42m);

        // El desperdicio por causa (otro indicador de C4) es consulta directa — sin agregador nuevo.
        await using var lector = CrearContexto();
        var desperdicio = await lector.Desperdicios.AsNoTracking().SingleAsync(d => d.Id == desperdicioId);
        desperdicio.DanoOrigen.Should().Be(3m);
        desperdicio.DanoProceso.Should().Be(1m);
    }

    [Fact]
    public async Task Dos_conexiones_distintas_consultando_la_misma_jornada_obtienen_exactamente_el_mismo_numero()
    {
        // C4 punto 3, criterio de salida de F9: "el cálculo vive en el servidor — los
        // dos paneles nunca divergen". Simula al supervisor y al Coordinador
        // preguntando por separado, cada uno con su propia conexión.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaYAbiertaAsync(ctx, 4, minutosArranque: 90);
        var lote = await ctx.Lotes.AsNoTracking().SingleAsync(l => l.JornadaLineaId == jornada);
        await CerrarLoteAsync(lote.Id, produccionReal: 77, danoOrigen: 0, danoProceso: 0, usuario);
        await RegistrarParoAsync(jornada, usuario);

        var comoSupervisor = await CalcularEficienciaAsync(jornada);
        var comoCoordinador = await CalcularEficienciaAsync(jornada);

        comoSupervisor.Should().BeEquivalentTo(comoCoordinador);
    }
}

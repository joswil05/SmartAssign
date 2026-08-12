using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E11.5 (docs/PROGRESO.md): <c>Lote</c> (00 §C5) + cambio de SKU
/// (§11.2). Mismo patrón de base descartable que el resto de la suite.
/// </summary>
public class LoteYCambioDeSkuTests : IAsyncLifetime
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
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<byte> CrearTurnoAsync(SmartAssignDbContext ctx)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        return turno.Id;
    }

    private static async Task<int> CrearSkuAsync(SmartAssignDbContext ctx, bool activo = true)
    {
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100, Activo = activo };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();
        return sku.Id;
    }

    /// <summary>Jornada-línea ya "arrancada" con su SKU inicial y el lote numero=1 que sp_ArrancarTurno habría abierto — atajo para probar sp_CambiarSKU sin recorrer todo el pipeline de planificación.</summary>
    private static async Task<(int jornadaLineaId, int skuInicialId)> JornadaConSkuYLoteAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var turno = await CrearTurnoAsync(ctx);
        var sku = await CrearSkuAsync(ctx);
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1), SkuId = sku, Estado = "arrancada", ArrancadoEn = DateTime.UtcNow };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        ctx.Lotes.Add(new Lote { JornadaLineaId = jornada.Id, SkuId = sku, Numero = 1 });
        await ctx.SaveChangesAsync();
        return (jornada.Id, sku);
    }

    private static async Task<int> CrearPuestoAsync(SmartAssignDbContext ctx, byte lineaId, string tipo = "rotativo")
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba", Tipo = tipo };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task VincularSkuAsync(SmartAssignDbContext ctx, int puestoId, int skuId)
    {
        ctx.PuestosSku.Add(new PuestoSku { PuestoId = puestoId, SkuId = skuId });
        await ctx.SaveChangesAsync();
    }

    private static async Task<int> OcuparAsync(SmartAssignDbContext ctx, int puestoId, int jornadaLineaId, int usuarioId)
    {
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operario", Situacion = "asignado" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion { JornadaLineaId = jornadaLineaId, PuestoId = puestoId, PersonalId = persona.Id, Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId });
        await ctx.SaveChangesAsync();
        return persona.Id;
    }

    // ═══ Pipeline completo de arranque (E5.x) — solo para el test de sp_ArrancarTurno ═══

    private static async Task<(int? JornadaLineaId, string? Codigo)> PlanificarLineaAsync(
        SqlConnection conexion, byte lineaId, byte turnoId, DateOnly dia, int? skuId, int? supervisorId, int usuarioId)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_PlanificarLinea";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@sku_id", (object?)skuId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@supervisor_id", (object?)supervisorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pId = new SqlParameter("@jornada_linea_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        await cmd.ExecuteNonQueryAsync();
        return (pId.Value as int?, pCodigo.Value as string);
    }

    private static async Task ConfirmarPlanificacionAsync(SqlConnection conexion, byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ConfirmarPlanificacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@lineas_sin_supervisor", SqlDbType.VarChar, 200) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ArrancarTurnoAsync(SqlConnection conexion, byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ArrancarTurno";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        await cmd.ExecuteNonQueryAsync();
        return pCodigo.Value as string;
    }

    // ═══ Invocación de sp_CambiarSKU ═══

    private record ResultadoCambiarSku(int? LoteNuevoId, int? Activados, int? Desactivados, string? Codigo, string? Mensaje);

    private async Task<ResultadoCambiarSku> CambiarSkuAsync(int jornadaLineaId, int skuNuevoId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CambiarSKU";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@sku_nuevo_id", skuNuevoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pLote = new SqlParameter("@lote_nuevo_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pActivados = new SqlParameter("@puestos_activados", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pDesactivados = new SqlParameter("@puestos_desactivados", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pLote);
        cmd.Parameters.Add(pActivados);
        cmd.Parameters.Add(pDesactivados);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoCambiarSku(pLote.Value as int?, pActivados.Value as int?, pDesactivados.Value as int?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Al_arrancar_el_turno_se_abre_un_lote_numero_1_para_cada_linea()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx);
        var sku = await CrearSkuAsync(ctx);
        var dia = new DateOnly(2026, 8, 10);

        await using (var conexion = await AbrirComoCoordinadorAsync())
        {
            var (jornadaLineaId, _) = await PlanificarLineaAsync(conexion, lineaId: 1, turno, dia, sku, supervisorId: usuario, usuario);
            await ConfirmarPlanificacionAsync(conexion, turno, dia, usuario);
            var codigo = await ArrancarTurnoAsync(conexion, turno, dia, usuario);

            codigo.Should().BeNull();

            var lote = await ctx.Lotes.AsNoTracking().SingleAsync(l => l.JornadaLineaId == jornadaLineaId);
            lote.Numero.Should().Be((short)1);
            lote.SkuId.Should().Be(sku);
            lote.CerradoEn.Should().BeNull();
        }
    }

    [Fact]
    public async Task Cambiar_de_sku_cierra_el_lote_anterior_y_abre_uno_nuevo_con_numero_incrementado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, skuInicial) = await JornadaConSkuYLoteAsync(ctx, lineaId: 4);
        var skuNuevo = await CrearSkuAsync(ctx);

        var resultado = await CambiarSkuAsync(jornada, skuNuevo, usuario);

        resultado.Codigo.Should().BeNull();
        resultado.LoteNuevoId.Should().NotBeNull();

        var loteAnterior = await ctx.Lotes.AsNoTracking().SingleAsync(l => l.SkuId == skuInicial);
        loteAnterior.CerradoEn.Should().NotBeNull();

        var loteNuevo = await ctx.Lotes.AsNoTracking().SingleAsync(l => l.Id == resultado.LoteNuevoId);
        loteNuevo.Numero.Should().Be((short)2);
        loteNuevo.SkuId.Should().Be(skuNuevo);
        loteNuevo.CerradoEn.Should().BeNull();
    }

    [Fact]
    public async Task Un_puesto_que_el_sku_nuevo_si_requiere_pasa_de_fuera_de_operacion_a_libre()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaConSkuYLoteAsync(ctx, lineaId: 4);
        var skuNuevo = await CrearSkuAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, 4);
        await VincularSkuAsync(ctx, puesto, skuNuevo); // solo declara el SKU nuevo, no el inicial

        var resultado = await CambiarSkuAsync(jornada, skuNuevo, usuario);

        resultado.Activados.Should().Be(1);
        resultado.Desactivados.Should().Be(0);
    }

    [Fact]
    public async Task Un_puesto_que_el_sku_nuevo_ya_no_requiere_con_ocupante_pasa_a_fuera_de_operacion_y_libera_a_la_L8()
    {
        // §11.2, literal: "si tenían ocupante, esa persona va a la L8".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, skuInicial) = await JornadaConSkuYLoteAsync(ctx, lineaId: 4);
        var puesto = await CrearPuestoAsync(ctx, 4);
        await VincularSkuAsync(ctx, puesto, skuInicial); // solo declara el SKU inicial — el nuevo lo excluye
        var ocupante = await OcuparAsync(ctx, puesto, jornada, usuario);
        var skuNuevo = await CrearSkuAsync(ctx);

        var resultado = await CambiarSkuAsync(jornada, skuNuevo, usuario);

        resultado.Activados.Should().Be(0);
        resultado.Desactivados.Should().Be(1);

        var asignacion = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.PersonalId == ocupante);
        asignacion.Fin.Should().NotBeNull();
        asignacion.MotivoFin.Should().Be("cambio_sku");

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.PersonalId == ocupante);
        movimiento.Motivo.Should().Be("cambio_sku");
        movimiento.LineaDestino.Should().Be((byte)8);
        movimiento.Estado.Should().Be("en_transito");

        var ocupanteDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == ocupante);
        ocupanteDb.Situacion.Should().Be("en_transito");
    }

    [Fact]
    public async Task Un_puesto_sin_ninguna_fila_de_PuestoSKU_nunca_cambia_de_estado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaConSkuYLoteAsync(ctx, lineaId: 4);
        await CrearPuestoAsync(ctx, 4); // sin ninguna fila PuestoSKU — siempre disponible
        var skuNuevo = await CrearSkuAsync(ctx);

        var resultado = await CambiarSkuAsync(jornada, skuNuevo, usuario);

        resultado.Activados.Should().Be(0);
        resultado.Desactivados.Should().Be(0);
    }

    [Fact]
    public async Task La_linea_termina_activa_tras_el_cambio_de_sku()
    {
        // 00 §C5, literal: "pasando la línea por En limpieza" — al terminar vuelve a operar.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaConSkuYLoteAsync(ctx, lineaId: 4);
        var skuNuevo = await CrearSkuAsync(ctx);

        await CambiarSkuAsync(jornada, skuNuevo, usuario);

        var linea = await ctx.Lineas.AsNoTracking().SingleAsync(l => l.Id == 4);
        linea.Situacion.Should().Be("activa");
    }

    [Fact]
    public async Task Un_sku_inexistente_o_inactivo_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaConSkuYLoteAsync(ctx, lineaId: 4);
        var skuInactivo = await CrearSkuAsync(ctx, activo: false);

        var resultado = await CambiarSkuAsync(jornada, skuInactivo, usuario);

        resultado.Codigo.Should().Be("SKU_INEXISTENTE");
        resultado.LoteNuevoId.Should().BeNull();
    }

    [Fact]
    public async Task Una_jornada_no_abierta_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var skuNuevo = await CrearSkuAsync(ctx);

        var resultado = await CambiarSkuAsync(jornadaLineaId: 999_999, skuNuevo, usuario);

        resultado.Codigo.Should().Be("JORNADA_NO_ABIERTA");
    }
}

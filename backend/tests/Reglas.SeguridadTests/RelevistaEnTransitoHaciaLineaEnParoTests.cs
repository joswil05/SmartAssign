using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E11.4 (docs/PROGRESO.md): 00 §C9 — paro con un relevista en
/// tránsito hacia esa línea. Mismo patrón de base descartable que el
/// resto de la suite.
/// </summary>
public class RelevistaEnTransitoHaciaLineaEnParoTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SqlConnection conexion)
    {
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

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx)
    {
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> JornadaAbiertaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var existente = await ctx.JornadasLinea.Where(j => j.LineaId == lineaId && j.CerradoEn == null).Select(j => j.Id).SingleOrDefaultAsync();
        if (existente != 0) return existente;

        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, byte lineaFisicaActual, string situacion = "presente_sin_asignar")
    {
        var p = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba", Categoria = "operario", LineaFisicaActual = lineaFisicaActual, Situacion = situacion };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>El ocupante actual del puesto rotativo hacia el que camina el relevista — a quien vino a relevar.</summary>
    private static async Task<(int personalId, int puestoId)> OcuparRotativoAsync(SmartAssignDbContext ctx, byte lineaId, int jornadaId, int usuarioId)
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"R{Guid.NewGuid():N}"[..15], NombrePuesto = "Rotativo de prueba", Tipo = "rotativo" };
        ctx.Puestos.Add(puesto);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante relevado", Categoria = "operario", Situacion = "asignado" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion { JornadaLineaId = jornadaId, PuestoId = puesto.Id, PersonalId = persona.Id, Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId });
        await ctx.SaveChangesAsync();
        return (persona.Id, puesto.Id);
    }

    // ═══ Invocación de los SP ═══

    private record ResultadoDespacho(long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoDespacho> DespacharAsync(int personalId, byte lineaDestino, int? puestoDestinoId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        // sp_DespacharPersona consulta Puesto (con RLS) cuando hay
        // puesto_destino_id — sin SESSION_CONTEXT la lectura no
        // devuelve filas y PUESTO_DESTINO_INEXISTENTE sale mal.
        await ComoCoordinadorAsync(conexion);
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DespacharPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_destino", lineaDestino);
        cmd.Parameters.AddWithValue("@motivo", "relevo");
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@puesto_destino_id", (object?)puestoDestinoId ?? DBNull.Value);
        var pId = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoDespacho(pId.Value as long?, pCodigo.Value as string, pMensaje.Value as string);
    }

    private record ResultadoRegistrarParo(int? ParoId, string? Codigo);

    private async Task<ResultadoRegistrarParo> RegistrarParoAsync(int jornadaLineaId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await ComoCoordinadorAsync(conexion);
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RegistrarParo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@categoria_id", (short)1);
        cmd.Parameters.AddWithValue("@causa_id", (short)1);
        cmd.Parameters.AddWithValue("@descripcion", "Paro real registrado para la prueba de E11.4.");
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pParo = new SqlParameter("@paro_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pRotativos = new SqlParameter("@rotativos_liberados", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pParo);
        cmd.Parameters.Add(pRotativos);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoRegistrarParo(pParo.Value as int?, pCodigo.Value as string);
    }

    private async Task ReanudarAsync(int paroId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ReanudarProduccion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@paro_id", paroId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
    }

    private record ResultadoRecepcion(bool? DestinoEnParo, string? Aviso, string? Codigo, string? Mensaje);

    private async Task<ResultadoRecepcion> RecibirAsync(long movimientoId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        // sp_RecibirPersona (E11.4) ahora consulta JornadaLinea (con RLS)
        // para saber si el destino tiene un paro abierto.
        await ComoCoordinadorAsync(conexion);
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RecibirPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@movimiento_id", movimientoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pDestinoEnParo = new SqlParameter("@destino_en_paro", SqlDbType.Bit) { Direction = ParameterDirection.Output };
        var pAviso = new SqlParameter("@aviso_linea_en_paro", SqlDbType.NVarChar, 200) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pDestinoEnParo);
        cmd.Parameters.Add(pAviso);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoRecepcion(pDestinoEnParo.Value as bool?, pAviso.Value as string, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task El_transito_no_se_cancela_cuando_la_linea_destino_entra_en_paro_a_mitad_de_camino()
    {
        // 00 §C9, literal: "El tránsito no se cancela: es inmune (§6.1)".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var (relevado, puestoRotativo) = await OcuparRotativoAsync(ctx, 4, jornada, usuario);
        var relevista = await CrearPersonaAsync(ctx, lineaFisicaActual: 8, situacion: "en_bolson");
        var despacho = await DespacharAsync(relevista, lineaDestino: 4, puestoRotativo, usuario);

        await RegistrarParoAsync(jornada, usuario);

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == despacho.MovimientoId);
        movimiento.Estado.Should().Be("en_transito", "el paro no cancela un tránsito ya en curso");
    }

    [Fact]
    public async Task Al_recibir_a_un_relevista_cuya_linea_destino_entro_en_paro_ve_el_aviso_explicito()
    {
        // 00 §C9, literal: "L4 está en paro — el puesto que venía a cubrir fue liberado".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var (relevado, puestoRotativo) = await OcuparRotativoAsync(ctx, 4, jornada, usuario);
        var relevista = await CrearPersonaAsync(ctx, lineaFisicaActual: 8, situacion: "en_bolson");
        var despacho = await DespacharAsync(relevista, lineaDestino: 4, puestoRotativo, usuario);
        await RegistrarParoAsync(jornada, usuario);

        var resultado = await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        resultado.Codigo.Should().BeNull("la recepción real siempre se confirma, el aviso es informativo");
        resultado.DestinoEnParo.Should().BeTrue();
        resultado.Aviso.Should().Be("L4 está en paro — el puesto que venía a cubrir fue liberado.");

        // El relevista sí llegó de verdad — la recepción no se bloquea por el aviso.
        var relevistaDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == relevista);
        relevistaDb.Situacion.Should().Be("presente_sin_asignar");
    }

    [Fact]
    public async Task El_puesto_que_el_relevista_venia_a_cubrir_queda_vacio_tras_el_paro()
    {
        // 00 §C9, literal: "La reserva del puesto fatigado se libera cuando el paro vacía los rotativos".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var (relevado, puestoRotativo) = await OcuparRotativoAsync(ctx, 4, jornada, usuario);
        var relevista = await CrearPersonaAsync(ctx, lineaFisicaActual: 8, situacion: "en_bolson");
        await DespacharAsync(relevista, lineaDestino: 4, puestoRotativo, usuario);

        await RegistrarParoAsync(jornada, usuario);

        var asignacionCerrada = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.PersonalId == relevado);
        asignacionCerrada.Fin.Should().NotBeNull("el paro ya vació ese rotativo — ya no hay a quién relevar cuando el relevista llegue");
    }

    [Fact]
    public async Task Si_el_paro_ya_termino_antes_de_recibir_no_hay_aviso_y_se_recibe_normalmente()
    {
        // 00 §C9, literal: "Si el paro ya terminó, se asigna normalmente".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var (relevado, puestoRotativo) = await OcuparRotativoAsync(ctx, 4, jornada, usuario);
        var relevista = await CrearPersonaAsync(ctx, lineaFisicaActual: 8, situacion: "en_bolson");
        var despacho = await DespacharAsync(relevista, lineaDestino: 4, puestoRotativo, usuario);
        var paro = await RegistrarParoAsync(jornada, usuario);
        await ReanudarAsync(paro.ParoId!.Value, usuario);

        var resultado = await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        resultado.DestinoEnParo.Should().BeFalse();
        resultado.Aviso.Should().BeNull();
    }

    [Fact]
    public async Task Recibir_hacia_una_linea_sin_ningun_paro_nunca_genera_el_aviso()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var relevista = await CrearPersonaAsync(ctx, lineaFisicaActual: 8, situacion: "en_bolson");
        var despacho = await DespacharAsync(relevista, lineaDestino: 4, puestoDestinoId: null, usuario);

        var resultado = await RecibirAsync(despacho.MovimientoId!.Value, usuario);

        resultado.DestinoEnParo.Should().BeFalse();
        resultado.Aviso.Should().BeNull();
    }
}

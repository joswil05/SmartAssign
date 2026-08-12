using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.6 (docs/PROGRESO.md): <c>sp_AceptarRelevo</c>/
/// <c>sp_RechazarPropuestaRelevo</c>/<c>sp_LimpiarDescartado</c> — §9.4
/// paso 3, literal, y 00 §B10. Mismo patrón de base descartable que el
/// resto de la suite.
/// </summary>
public class AceptarRechazarYDescartadosTests : IAsyncLifetime
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

    private static async Task<int> CrearPersonaEnBolsonAsync(SmartAssignDbContext ctx, string ficha)
    {
        var p = new Personal
        {
            Ficha = ficha, NombreCompleto = "Candidato de prueba", Categoria = "operario",
            Situacion = "en_bolson", LineaFisicaActual = 8,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba", Tipo = "rotativo",
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx, string rol = "coordinador")
    {
        var u = new Usuario
        {
            Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba",
            Rol = rol, OrigenIdentidad = "local", Activo = true,
        };
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
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = DateOnly.FromDateTime(DateTime.UtcNow) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    private async Task<long> InsertarSolicitudAsync(int puestoId, int jornadaLineaId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel, exceso_relativo)
            OUTPUT INSERTED.Id
            VALUES (@puesto_id, @jornada_linea_id, 'umbral_automatico', 'sugerido', 120);
            """;
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        var resultado = await cmd.ExecuteScalarAsync();
        return (long)resultado!;
    }

    // ═══ Invocación de los SP ═══

    private record ResultadoAceptar(int? CandidatoId, long? MovimientoId, string? Codigo, string? Mensaje);
    private record ResultadoSimple(string? Codigo, string? Mensaje);

    private async Task<ResultadoAceptar> AceptarAsync(long solicitudId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_AceptarRelevo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCandidato = new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pMovimiento = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCandidato);
        cmd.Parameters.Add(pMovimiento);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoAceptar(pCandidato.Value as int?, pMovimiento.Value as long?, pCodigo.Value as string, pMensaje.Value as string);
    }

    private async Task<ResultadoSimple> RechazarPropuestaAsync(long solicitudId, int personalId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RechazarPropuestaRelevo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoSimple(pCodigo.Value as string, pMensaje.Value as string);
    }

    private async Task<ResultadoSimple> LimpiarDescartadoAsync(long descarteId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_LimpiarDescartado";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@descarte_id", descarteId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoSimple(pCodigo.Value as string, pMensaje.Value as string);
    }

    private async Task<(int? CandidatoId, string? Codigo)> ProponerAsync(int puestoId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ProponerRelevista";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        var pCandidato = new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCede = new SqlParameter("@cede_perfil", SqlDbType.Bit) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCandidato);
        cmd.Parameters.Add(pCede);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return (pCandidato.Value as int?, pCodigo.Value as string);
    }

    // ═══ sp_AceptarRelevo ═══

    [Fact]
    public async Task Aceptar_despacha_al_candidato_con_el_puesto_reservado_y_cierra_la_solicitud_cubierta()
    {
        // §9.4 p3, literal: "el candidato queda en tránsito... y el puesto fatigado queda reservado para él".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);

        var resultado = await AceptarAsync(solicitud, usuario);

        resultado.Codigo.Should().BeNull();
        resultado.CandidatoId.Should().Be(candidato);
        resultado.MovimientoId.Should().NotBeNull();

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.PersonalId.Should().Be(candidato);
        movimiento.PuestoDestinoId.Should().Be(puesto);
        movimiento.Estado.Should().Be("en_transito");

        var solicitudTras = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == solicitud);
        solicitudTras.ResueltaEn.Should().NotBeNull();
        solicitudTras.Resultado.Should().Be("cubierta");
        solicitudTras.MovimientoId.Should().Be(resultado.MovimientoId);

        var candidatoTras = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == candidato);
        candidatoTras.Situacion.Should().Be("en_transito");
    }

    [Fact]
    public async Task Aceptar_una_solicitud_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var resultado = await AceptarAsync(999_999_999, usuario);

        resultado.Codigo.Should().Be("SOLICITUD_INEXISTENTE");
    }

    [Fact]
    public async Task No_se_puede_aceptar_dos_veces_la_misma_solicitud()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);
        await AceptarAsync(solicitud, usuario);

        var segunda = await AceptarAsync(solicitud, usuario);

        segunda.Codigo.Should().Be("SOLICITUD_NO_ABIERTA");
    }

    [Fact]
    public async Task Sin_candidatos_en_el_Bolson_aceptar_propaga_el_rechazo_y_la_solicitud_sigue_abierta()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4); // sin nadie en el Bolsón
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);

        var resultado = await AceptarAsync(solicitud, usuario);

        resultado.Codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
        resultado.MovimientoId.Should().BeNull();

        var solicitudTras = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == solicitud);
        solicitudTras.ResueltaEn.Should().BeNull("sin candidato no hay nada que aceptar — la solicitud sigue pendiente");
    }

    // ═══ sp_RechazarPropuestaRelevo ═══

    [Fact]
    public async Task Rechazar_registra_el_descarte_y_la_solicitud_sigue_abierta()
    {
        // 00 §B10 + §9.4 p3: "el sistema carga otra sugerencia si hay alguna disponible".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuarioL8 = await CrearUsuarioAsync(ctx);
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);

        var resultado = await RechazarPropuestaAsync(solicitud, candidato, usuarioL8);

        resultado.Codigo.Should().BeNull();
        var descarte = await ctx.RelevosDescartados.AsNoTracking().SingleAsync(d => d.PuestoId == puesto && d.PersonalId == candidato);
        descarte.DescartadoPor.Should().Be(usuarioL8);
        descarte.LimpiadoEn.Should().BeNull();

        var solicitudTras = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == solicitud);
        solicitudTras.ResueltaEn.Should().BeNull();

        var candidatoTras = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == candidato);
        candidatoTras.Situacion.Should().Be("en_bolson", "el rechazo de una PROPUESTA no mueve a nadie — nunca llegó a despacharse");
    }

    [Fact]
    public async Task No_se_puede_descartar_dos_veces_a_la_misma_persona_para_el_mismo_puesto_hoy()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);
        await RechazarPropuestaAsync(solicitud, candidato, usuario);

        var segundo = await RechazarPropuestaAsync(solicitud, candidato, usuario);

        segundo.Codigo.Should().Be("YA_DESCARTADO");
    }

    [Fact]
    public async Task Tras_rechazar_al_unico_candidato_sp_ProponerRelevista_ya_no_lo_ofrece()
    {
        // Cierra el ciclo: B10 en acción sobre B2 (E9.5).
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);

        (await ProponerAsync(puesto)).CandidatoId.Should().Be(candidato);

        await RechazarPropuestaAsync(solicitud, candidato, usuario);

        var trasRechazo = await ProponerAsync(puesto);
        trasRechazo.CandidatoId.Should().BeNull();
        trasRechazo.Codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
    }

    [Fact]
    public async Task Con_dos_candidatos_rechazar_al_primero_hace_que_se_proponga_el_segundo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var primero = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var segundo = await CrearPersonaEnBolsonAsync(ctx, "F0002");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);

        (await ProponerAsync(puesto)).CandidatoId.Should().Be(primero, "F0001 gana el desempate por ficha");

        await RechazarPropuestaAsync(solicitud, primero, usuario);

        (await ProponerAsync(puesto)).CandidatoId.Should().Be(segundo);
    }

    // ═══ sp_LimpiarDescartado ═══

    [Fact]
    public async Task Quien_creo_el_descarte_puede_limpiarlo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var supervisorL8 = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);
        await RechazarPropuestaAsync(solicitud, candidato, supervisorL8);
        var descarteId = (await ctx.RelevosDescartados.AsNoTracking().SingleAsync()).Id;

        var resultado = await LimpiarDescartadoAsync(descarteId, supervisorL8);

        resultado.Codigo.Should().BeNull();
        var descarte = await ctx.RelevosDescartados.AsNoTracking().SingleAsync(d => d.Id == descarteId);
        descarte.LimpiadoEn.Should().NotBeNull();
        descarte.LimpiadoPor.Should().Be(supervisorL8);
    }

    [Fact]
    public async Task El_coordinador_puede_limpiar_un_descarte_que_no_creo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var supervisorL8 = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var coordinador = await CrearUsuarioAsync(ctx, rol: "coordinador");
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);
        await RechazarPropuestaAsync(solicitud, candidato, supervisorL8);
        var descarteId = (await ctx.RelevosDescartados.AsNoTracking().SingleAsync()).Id;

        var resultado = await LimpiarDescartadoAsync(descarteId, coordinador);

        resultado.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task Un_supervisor_distinto_de_quien_lo_creo_no_puede_limpiarlo()
    {
        // 00 §B10, literal: "el supervisor destino NO: no manda sobre personal ajeno".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var supervisorL8 = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var supervisorDestino = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);
        await RechazarPropuestaAsync(solicitud, candidato, supervisorL8);
        var descarteId = (await ctx.RelevosDescartados.AsNoTracking().SingleAsync()).Id;

        var resultado = await LimpiarDescartadoAsync(descarteId, supervisorDestino);

        resultado.Codigo.Should().Be("SIN_PERMISO_PARA_LIMPIAR");
    }

    [Fact]
    public async Task Tras_limpiar_el_descarte_sp_ProponerRelevista_vuelve_a_ofrecer_a_esa_persona()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var supervisorL8 = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var candidato = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var solicitud = await InsertarSolicitudAsync(puesto, jornada);
        await RechazarPropuestaAsync(solicitud, candidato, supervisorL8);
        (await ProponerAsync(puesto)).CandidatoId.Should().BeNull();
        var descarteId = (await ctx.RelevosDescartados.AsNoTracking().SingleAsync()).Id;

        await LimpiarDescartadoAsync(descarteId, supervisorL8);

        (await ProponerAsync(puesto)).CandidatoId.Should().Be(candidato);
    }

    [Fact]
    public async Task Un_descarte_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var resultado = await LimpiarDescartadoAsync(999_999_999, usuario);

        resultado.Codigo.Should().Be("DESCARTE_INEXISTENTE");
    }
}

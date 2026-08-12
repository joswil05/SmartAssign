using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E8.2 (docs/PROGRESO.md): "Tránsito inmune" — fuente §6.1, literal:
/// "Mientras alguien camina entre dos puntos, ninguna otra terminal puede
/// capturarlo ni reasignarlo. Su destino ya está comprometido." No añade
/// mecanismo nuevo: ambas piezas que otorgan la inmunidad ya existían —
/// <c>sp_ValidarAsignacion</c> paso 2 (E4.6) ya rechaza cualquier
/// situación distinta de <c>presente_sin_asignar</c>/<c>en_bolson</c>, y
/// <c>UX_Mov_transito</c> (E8.1) ya es la defensa de última línea a nivel
/// de base de datos. Esta UT prueba, con el camino REAL de principio a
/// fin (despachar de verdad vía <c>sp_DespacharPersona</c>, no sembrar
/// <c>situacion='en_transito'</c> a mano), que la garantía de §6.1 se
/// sostiene en los dos caminos de captura que ya existen
/// (<c>sp_AsignarPersona</c> directo y la escalera <c>sp_SugerirPuesto</c>)
/// y en el índice único como respaldo si algún día un camino nuevo se
/// olvida de consultar <c>situacion</c>.
/// </summary>
public class TransitoInmuneTests : IAsyncLifetime
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

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, byte lineaFisicaActual, string situacion = "presente_sin_asignar")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = "operario", LineaFisicaActual = lineaFisicaActual, Situacion = situacion,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoRotativoAsync(SmartAssignDbContext ctx, byte lineaId)
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

    // ═══ Invocación de los SP ═══

    private record ResultadoDespacho(long? MovimientoId, string? Codigo);
    private record ResultadoAsignar(string? Codigo, string? Mensaje);
    private record ResultadoSugerencia(int? PuestoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoDespacho> DespacharAsync(int personalId, byte lineaDestino, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DespacharPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_destino", lineaDestino);
        cmd.Parameters.AddWithValue("@motivo", "relevo");
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pId = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoDespacho(pId.Value as long?, pCodigo.Value as string);
    }

    private async Task<ResultadoAsignar> AsignarAsync(int personalId, int puestoId, int usuarioId, int jornadaLineaId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_AsignarPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@origen", "manual_supervisor");
        cmd.Parameters.AddWithValue("@idempotency_key", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@ceder_perfil", false);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        var pAsignacion = new SqlParameter("@asignacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        cmd.Parameters.Add(pAsignacion);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoAsignar(pCodigo.Value as string, pMensaje.Value as string);
    }

    private async Task<ResultadoSugerencia> SugerirAsync(int personalId, byte lineaId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_SugerirPuesto";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pPuesto = new SqlParameter("@puesto_id_sugerido", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pNivel = new SqlParameter("@nivel", SqlDbType.TinyInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pPuesto);
        cmd.Parameters.Add(pNivel);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoSugerencia(pPuesto.Value as int?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Una_persona_recien_despachada_no_puede_ser_capturada_por_asignacion_directa()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var despacho = await DespacharAsync(persona, lineaDestino: 8, usuario);
        despacho.Codigo.Should().BeNull("el despacho debe salir bien para que la persona quede en tránsito");

        // Un tercer supervisor, en una línea que NO es ni el origen ni el
        // destino del movimiento, intenta capturarla a pie de línea —
        // §6.1: "ninguna otra terminal puede capturarlo".
        var puestoEnOtraLinea = await CrearPuestoRotativoAsync(ctx, lineaId: 2);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 2);

        var resultado = await AsignarAsync(persona, puestoEnOtraLinea, usuario, jornada);

        resultado.Codigo.Should().Be("PERSONA_NO_DISPONIBLE");
    }

    [Fact]
    public async Task Una_persona_recien_despachada_no_puede_ser_capturada_ni_en_su_propia_linea_de_destino()
    {
        // Su destino ya está comprometido (§6.1) — ni siquiera el
        // supervisor que la está esperando puede "adelantarla" a otro
        // puesto mientras todavía está caminando.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        await DespacharAsync(persona, lineaDestino: 8, usuario);

        var puestoEnDestino = await CrearPuestoRotativoAsync(ctx, lineaId: 8);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 8);

        var resultado = await AsignarAsync(persona, puestoEnDestino, usuario, jornada);

        resultado.Codigo.Should().Be("PERSONA_NO_DISPONIBLE");
    }

    [Fact]
    public async Task La_escalera_de_sugerencia_tampoco_ofrece_a_alguien_en_transito()
    {
        // sp_SugerirPuesto (E6.7) recorre TODOS los puestos libres
        // compatibles de la línea vía sp_ValidarAsignacion — con al menos
        // un puesto libre disponible, el código debe ser el de la
        // persona, no "no había nada que ofrecer".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        await DespacharAsync(persona, lineaDestino: 8, usuario);
        await CrearPuestoRotativoAsync(ctx, lineaId: 8); // hay puesto libre, pero la persona no está disponible

        var sugerencia = await SugerirAsync(persona, lineaId: 8, usuario);

        sugerencia.PuestoId.Should().BeNull();
        sugerencia.Codigo.Should().Be("PERSONA_NO_DISPONIBLE");
    }

    [Fact]
    public async Task El_indice_UX_Mov_transito_es_la_ultima_linea_de_defensa_a_nivel_de_base_de_datos()
    {
        // Aunque ningún camino de la aplicación debería saltarse
        // sp_DespacharPersona, el índice único filtrado (E8.1) es el
        // respaldo: dos filas en_transito para la misma persona no
        // pueden coexistir ni insertadas directamente.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
            VALUES ({persona}, 4, 8, 'relevo', {usuario});
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
            VALUES ({persona}, 4, 2, 'relevo', {usuario});
            """;

        var act = async () => await cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("UX_Mov_transito");
    }
}

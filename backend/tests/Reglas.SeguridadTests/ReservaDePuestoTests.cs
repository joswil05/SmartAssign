using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E8.5 (docs/PROGRESO.md): reserva de puesto en el despacho — 00 §B4,
/// literal: "el puesto destino no puede estar ya reservado por otro
/// relevista en tránsito. Sin esta guarda, dos personas convergen al
/// mismo puesto y una queda sin destino a mitad de la planta." Prueba
/// solo la GUARDA de convergencia (<c>UX_Mov_reserva</c> + el chequeo de
/// aplicación bajo bloqueo); el algoritmo de qué puesto elegir (B4
/// puntos 1-3) es el motor de relevos completo, fuera de esta UT (E9).
/// Mismo patrón de base descartable que el resto de la familia de E8.
/// </summary>
public class ReservaDePuestoTests : IAsyncLifetime
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

    // ═══ Invocación del SP ═══

    private record ResultadoDespacho(long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoDespacho> DespacharAsync(int personalId, byte lineaDestino, int usuarioId, int? puestoDestinoId = null)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
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

    [Fact]
    public async Task Despachar_sin_puesto_destino_sigue_funcionando_igual_que_antes()
    {
        // Regresión de compatibilidad: @puesto_destino_id = NULL por
        // default, así que E8.1-E8.4 no cambian de comportamiento.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);

        var resultado = await DespacharAsync(persona, lineaDestino: 8, usuario);

        resultado.Codigo.Should().BeNull();
        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.PuestoDestinoId.Should().BeNull();
    }

    [Fact]
    public async Task Despachar_con_puesto_destino_lo_persiste_como_reserva()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var puesto = await CrearPuestoRotativoAsync(ctx, lineaId: 8);

        var resultado = await DespacharAsync(persona, lineaDestino: 8, usuario, puesto);

        resultado.Codigo.Should().BeNull();
        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.PuestoDestinoId.Should().Be(puesto);
    }

    [Fact]
    public async Task Dos_relevistas_no_pueden_converger_al_mismo_puesto()
    {
        // 00 §B4, literal.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, lineaId: 8);
        var personaA = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var personaB = await CrearPersonaAsync(ctx, lineaFisicaActual: 1);

        var primero = await DespacharAsync(personaA, lineaDestino: 8, usuario, puesto);
        var segundo = await DespacharAsync(personaB, lineaDestino: 8, usuario, puesto);

        primero.Codigo.Should().BeNull();
        segundo.Codigo.Should().Be("PUESTO_YA_RESERVADO");
    }

    [Fact]
    public async Task Una_vez_resuelto_el_primer_transito_el_puesto_vuelve_a_estar_disponible()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, lineaId: 8);
        var personaA = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var personaB = await CrearPersonaAsync(ctx, lineaFisicaActual: 1);
        var primero = await DespacharAsync(personaA, lineaDestino: 8, usuario, puesto);

        await using (var conexion = new SqlConnection(CadenaConexion))
        {
            await conexion.OpenAsync();
            await using var cmd = conexion.CreateCommand();
            cmd.CommandText = "sp_RecibirPersona";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@movimiento_id", primero.MovimientoId!.Value);
            cmd.Parameters.AddWithValue("@usuario_id", usuario);
            cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
            await cmd.ExecuteNonQueryAsync();
        }

        var segundo = await DespacharAsync(personaB, lineaDestino: 8, usuario, puesto);

        segundo.Codigo.Should().BeNull("la reserva del primero ya se cerró al recibirlo — el puesto quedó libre para reservarse de nuevo");
    }

    [Fact]
    public async Task Un_puesto_destino_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);

        var resultado = await DespacharAsync(persona, lineaDestino: 8, usuario, puestoDestinoId: 999_999_999);

        resultado.Codigo.Should().Be("PUESTO_DESTINO_INEXISTENTE");
    }

    [Fact]
    public async Task Cinco_relevistas_despachados_a_la_vez_al_mismo_puesto_solo_uno_gana()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, lineaId: 8);
        var personas = new List<int>();
        for (var i = 0; i < 5; i++) personas.Add(await CrearPersonaAsync(ctx, lineaFisicaActual: 4));

        var tareas = personas.Select(p => DespacharAsync(p, lineaDestino: 8, usuario, puesto));
        var resultados = await Task.WhenAll(tareas);

        resultados.Count(r => r.Codigo is null).Should().Be(1, "UX_Mov_reserva + el bloqueo sobre Puesto serializan la contienda");
        resultados.Count(r => r.Codigo == "PUESTO_YA_RESERVADO").Should().Be(4);
    }

    [Fact]
    public async Task El_indice_UX_Mov_reserva_es_la_ultima_linea_de_defensa_a_nivel_de_base_de_datos()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoRotativoAsync(ctx, lineaId: 8);
        var personaA = await CrearPersonaAsync(ctx, lineaFisicaActual: 4);
        var personaB = await CrearPersonaAsync(ctx, lineaFisicaActual: 1);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
            VALUES ({personaA}, 4, 8, {puesto}, 'relevo', {usuario});
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
            VALUES ({personaB}, 1, 8, {puesto}, 'relevo', {usuario});
            """;

        var act = async () => await cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("UX_Mov_reserva");
    }
}

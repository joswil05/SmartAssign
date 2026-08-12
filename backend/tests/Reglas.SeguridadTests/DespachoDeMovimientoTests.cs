using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E8.1 (docs/PROGRESO.md): <c>sp_DespacharPersona</c> — paso 1 del
/// proceso de tres pasos de la Parte X ("mover a alguien de una línea a
/// otra... porque implica un desplazamiento físico real"). Solo abre la
/// fila de <c>Movimiento</c> con <c>hora_salida</c> real (§12.7); la
/// inmunidad durante el tránsito (paso 2) llega en E8.2. Mismo patrón de
/// base descartable que el resto de la suite.
/// </summary>
public class DespachoDeMovimientoTests : IAsyncLifetime
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

    private static async Task<int> CrearPersonaAsync(
        SmartAssignDbContext ctx, byte? lineaFisicaActual, string situacion, string categoria = "operario")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = categoria, LineaFisicaActual = lineaFisicaActual, Situacion = situacion,
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

    // ═══ Invocación del SP ═══

    private record ResultadoDespacho(long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoDespacho> DespacharAsync(int personalId, byte lineaDestino, string motivo, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DespacharPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_destino", lineaDestino);
        cmd.Parameters.AddWithValue("@motivo", motivo);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
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
    public async Task Despachar_a_alguien_presente_sin_asignar_abre_el_movimiento_con_hora_de_salida_real()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4, situacion: "presente_sin_asignar");
        var antes = DateTime.UtcNow;

        var resultado = await DespacharAsync(persona, lineaDestino: 8, motivo: "liberacion_bolson", usuario);

        resultado.Codigo.Should().BeNull();
        resultado.MovimientoId.Should().NotBeNull();

        var movimiento = await ctx.Movimientos.SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.PersonalId.Should().Be(persona);
        movimiento.LineaOrigen.Should().Be((byte)4);
        movimiento.LineaDestino.Should().Be((byte)8);
        movimiento.Motivo.Should().Be("liberacion_bolson");
        movimiento.Estado.Should().Be("en_transito");
        movimiento.DespachadoPor.Should().Be(usuario);
        movimiento.HoraLlegada.Should().BeNull();
        movimiento.HoraSalida.Should().BeOnOrAfter(antes.AddSeconds(-2)); // §12.7: hora exacta de salida, real
    }

    [Fact]
    public async Task Despachar_a_alguien_en_bolson_tambien_es_valido()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 8, situacion: "en_bolson");

        var resultado = await DespacharAsync(persona, lineaDestino: 4, motivo: "relevo", usuario);

        resultado.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task El_despacho_deja_a_la_persona_en_transito()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4, situacion: "presente_sin_asignar");

        await DespacharAsync(persona, lineaDestino: 8, motivo: "liberacion_bolson", usuario);

        // AsNoTracking(): la fila se escribió por una conexión cruda distinta
        // — el ctx que la creó todavía tiene la entidad en su change tracker.
        (await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona)).Situacion.Should().Be("en_transito");
    }

    [Fact]
    public async Task Sin_ubicacion_fisica_registrada_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: null, situacion: "presente_sin_asignar");

        var resultado = await DespacharAsync(persona, lineaDestino: 4, motivo: "relevo", usuario);

        resultado.Codigo.Should().Be("SIN_LINEA_FISICA");
        resultado.MovimientoId.Should().BeNull();
    }

    [Theory]
    [InlineData("asignado")]
    [InlineData("fuera_de_turno")]
    [InlineData("en_transito")]
    [InlineData("retirado_temporal")]
    [InlineData("ausente_justificado")]
    public async Task Solo_se_despacha_a_quien_esta_disponible_en_bolson_o_sin_asignar(string situacionNoDisponible)
    {
        // Parte X paso 1, literal: "en Bolsón o sin asignar" — cualquier otra situación se rechaza.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4, situacion: situacionNoDisponible);

        var resultado = await DespacharAsync(persona, lineaDestino: 8, motivo: "relevo", usuario);

        resultado.Codigo.Should().Be("NO_DISPONIBLE_PARA_DESPACHO");
        resultado.MovimientoId.Should().BeNull();
    }

    [Fact]
    public async Task No_se_puede_despachar_dos_veces_a_quien_ya_esta_en_transito()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4, situacion: "presente_sin_asignar");
        await DespacharAsync(persona, lineaDestino: 8, motivo: "liberacion_bolson", usuario);

        // La situación real ya cambió a en_transito por el primer despacho — el
        // segundo intento debe rechazarse por eso, aunque UX_Mov_transito también lo bloquearía.
        var segundo = await DespacharAsync(persona, lineaDestino: 2, motivo: "relevo", usuario);

        segundo.Codigo.Should().Be("NO_DISPONIBLE_PARA_DESPACHO");
    }

    [Fact]
    public async Task El_rol_de_aplicacion_no_puede_escribir_movimiento_directo_saltandose_el_sp()
    {
        // 04 §7.5 — mismo mecanismo de impersonación que el resto de tablas críticas.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4, situacion: "presente_sin_asignar");

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = $"""
            EXECUTE AS USER = 'rol_app';
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, despachado_por)
            VALUES ({persona}, 4, 8, 'relevo', {usuario});
            REVERT;
            """;

        var act = async () => await cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("INSERT permission was denied");
    }

    [Fact]
    public async Task Dos_supervisores_despachando_a_la_misma_persona_a_la_vez_solo_uno_gana()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisicaActual: 4, situacion: "presente_sin_asignar");

        var tareas = Enumerable.Range(0, 5).Select(_ => DespacharAsync(persona, lineaDestino: 8, motivo: "relevo", usuario));
        var resultados = await Task.WhenAll(tareas);

        resultados.Count(r => r.Codigo is null).Should().Be(1, "solo un despacho puede ganar por persona (UX_Mov_transito, §6.1)");
        // El chequeo de situación (pre-transacción) puede pasar en varios a
        // la vez si aún no hay ningún commit — la sección serializada por
        // UPDLOCK, que sí ve el Movimiento ya insertado por el ganador, es
        // la que de verdad garantiza el "solo uno" de arriba. Cuál de los
        // dos códigos recibe cada perdedor depende de si su lectura previa
        // de `situacion` corrió antes o después del commit del ganador —
        // no es determinista bajo carga real (verificado: la distribución
        // exacta varía entre corridas). La única invariante real es que
        // TODOS los perdedores queden rechazados por uno de los dos.
        resultados.Where(r => r.Codigo is not null)
            .Should().OnlyContain(r => r.Codigo == "YA_EN_TRANSITO" || r.Codigo == "NO_DISPONIBLE_PARA_DESPACHO")
            .And.HaveCount(4);
    }
}

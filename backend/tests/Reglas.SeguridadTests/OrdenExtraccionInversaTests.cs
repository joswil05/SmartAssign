using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E10.1 (docs/PROGRESO.md): arranca E10. <c>fn_OrdenExtraccionInversa</c>
/// — §9.6, literal 00 §A5: "se busca personal en la línea activa de
/// menor prioridad, recorriendo la jerarquía al revés". Mismo patrón de
/// base descartable que el resto de la suite.
/// </summary>
public class OrdenExtraccionInversaTests : IAsyncLifetime
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

    /// <summary>Activa todas las líneas 1..10 (§9.6 exige "línea ACTIVA") — la semilla estructural las trae inactivas por defecto.</summary>
    private async Task ActivarTodasLasLineasAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "UPDATE Linea SET activa_hoy = 1;";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task DesactivarLineaAsync(byte lineaId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "UPDATE Linea SET activa_hoy = 0 WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", lineaId);
        await cmd.ExecuteNonQueryAsync();
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

    private async Task<List<byte>> OrdenExtraccionAsync(byte lineaSolicitante)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT linea_id FROM dbo.fn_OrdenExtraccionInversa(@linea) ORDER BY orden DESC;";
        cmd.Parameters.AddWithValue("@linea", lineaSolicitante);
        var resultado = new List<byte>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) resultado.Add(reader.GetByte(0));
        return resultado;
    }

    [Fact]
    public async Task Reproduce_el_orden_publicado_literal_de_00_A5_y_9_6()
    {
        // "L10, L9, L3, L5, L7, L6, L2, L1" — L4 excluida porque en ESE
        // ejemplo es la línea solicitante, no una exclusión permanente (A5b).
        await ActivarTodasLasLineasAsync();

        var orden = await OrdenExtraccionAsync(lineaSolicitante: 4);

        orden.Should().Equal((byte)10, (byte)9, (byte)3, (byte)5, (byte)7, (byte)6, (byte)2, (byte)1);
    }

    [Fact]
    public async Task La_L8_nunca_aparece_aunque_este_marcada_activa()
    {
        await ActivarTodasLasLineasAsync(); // incluye a L8

        var orden = await OrdenExtraccionAsync(lineaSolicitante: 4);

        orden.Should().NotContain((byte)8, "la L8 está vacía por definición — es lo que dispara el mecanismo, nunca una donante");
    }

    [Fact]
    public async Task A5b_L4_si_puede_ser_donante_cuando_no_es_la_solicitante()
    {
        // 00 §A5b, literal: "L4 SÍ puede ser donante". Si L1 solicita, L4 debe aparecer.
        await ActivarTodasLasLineasAsync();

        var orden = await OrdenExtraccionAsync(lineaSolicitante: 1);

        orden.Should().Contain((byte)4);
    }

    [Fact]
    public async Task La_linea_solicitante_nunca_aparece_en_su_propio_resultado()
    {
        await ActivarTodasLasLineasAsync();

        var orden = await OrdenExtraccionAsync(lineaSolicitante: 6);

        orden.Should().NotContain((byte)6);
    }

    [Fact]
    public async Task Una_linea_inactiva_no_cuenta_como_donante()
    {
        // §9.6, literal: "línea ACTIVA de menor prioridad" — inactiva no participa.
        await ActivarTodasLasLineasAsync();
        await DesactivarLineaAsync(10); // la de menor prioridad de todas

        var orden = await OrdenExtraccionAsync(lineaSolicitante: 4);

        orden.Should().NotContain((byte)10);
        orden.Should().StartWith((byte)9, "con L10 inactiva, L9 pasa a ser la primera candidata");
    }

    [Fact]
    public async Task El_orden_se_deriva_en_vivo_cambiar_la_prioridad_lo_refleja_sin_lista_aparte()
    {
        // 00 §A5: "se implementa como derivación... para que §12.6 (prioridad
        // configurable en caliente) no exija mantener dos listas en sincronía".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        await ActivarTodasLasLineasAsync();
        var usuario = await CrearUsuarioAsync(ctx);

        var ordenAntes = await OrdenExtraccionAsync(lineaSolicitante: 4);
        ordenAntes.First().Should().Be(10, "L10 tiene la menor prioridad de todas — la primera candidata a extracción");

        // El Coordinador sube L10 al primer puesto de prioridad — deja de ser la de menor prioridad.
        await using (var conexion = new SqlConnection(CadenaConexion))
        {
            await conexion.OpenAsync();
            await using var cmd = conexion.CreateCommand();
            cmd.CommandText = "sp_CambiarPrioridadLinea";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@linea_id", (byte)10);
            cmd.Parameters.AddWithValue("@orden_nuevo", (byte)1);
            cmd.Parameters.AddWithValue("@usuario_id", usuario);
            cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
            await cmd.ExecuteNonQueryAsync();
        }

        var ordenDespues = await OrdenExtraccionAsync(lineaSolicitante: 4);

        ordenDespues.First().Should().NotBe(10, "L10 ya no es la de menor prioridad — el cambio se refleja de inmediato, sin lista aparte");
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E11.1 (docs/PROGRESO.md), arranca E11: <c>sp_RegistrarParo</c> +
/// <c>sp_ReanudarProduccion</c> — §11.1, dos niveles + descripción
/// obligatoria. Mismo patrón de base descartable que el resto de la
/// suite. Catálogo sembrado desde E0/E1: CategoriaParo 1=Mecánico,
/// 2=Eléctrico; CausaParo 1=Avería de máquina (cat. 1), 3=Corte de
/// energía (cat. 2).
/// </summary>
public class ParoDosNivelesYDescripcionObligatoriaTests : IAsyncLifetime
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

    // ═══ Invocación de sp_RegistrarParo ═══

    private record ResultadoRegistrar(int? ParoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoRegistrar> RegistrarParoAsync(
        int jornadaLineaId, short categoriaId, short causaId, string descripcion, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RegistrarParo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@categoria_id", categoriaId);
        cmd.Parameters.AddWithValue("@causa_id", causaId);
        cmd.Parameters.AddWithValue("@descripcion", descripcion);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pParo = new SqlParameter("@paro_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pParo);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoRegistrar(pParo.Value as int?, pCodigo.Value as string, pMensaje.Value as string);
    }

    // ═══ Invocación de sp_ReanudarProduccion ═══

    private record ResultadoReanudar(string? Codigo, string? Mensaje);

    private async Task<ResultadoReanudar> ReanudarAsync(int paroId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ReanudarProduccion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@paro_id", paroId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoReanudar(pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Registrar_un_paro_valido_lo_abre_con_inicio_real_y_sin_fin()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);

        var antes = DateTime.UtcNow;
        var resultado = await RegistrarParoAsync(jornada, categoriaId: 1, causaId: 1, "Se detuvo el motor principal, huele a quemado.", usuario);

        resultado.Codigo.Should().BeNull();
        resultado.ParoId.Should().NotBeNull();

        var paro = await ctx.Paros.AsNoTracking().SingleAsync(p => p.Id == resultado.ParoId);
        paro.CategoriaId.Should().Be((short)1);
        paro.CausaId.Should().Be((short)1);
        paro.Fin.Should().BeNull();
        paro.Inicio.Should().BeOnOrAfter(antes.AddSeconds(-2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Una_descripcion_vacia_o_en_blanco_se_rechaza(string descripcionVacia)
    {
        // §11.1, literal: "El supervisor debe escribir qué observó antes de confirmar".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);

        var resultado = await RegistrarParoAsync(jornada, categoriaId: 1, causaId: 1, descripcionVacia, usuario);

        resultado.Codigo.Should().Be("DESCRIPCION_OBLIGATORIA");
        resultado.ParoId.Should().BeNull();
    }

    [Fact]
    public async Task Una_causa_que_no_pertenece_a_la_categoria_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);

        // causa 3 = "Corte de energía" pertenece a la categoría 2 (Eléctrico), no a la 1 (Mecánico).
        var resultado = await RegistrarParoAsync(jornada, categoriaId: 1, causaId: 3, "Descripción cualquiera con contenido real.", usuario);

        resultado.Codigo.Should().Be("CAUSA_NO_PERTENECE_A_LA_CATEGORIA");
        resultado.ParoId.Should().BeNull();
    }

    [Fact]
    public async Task Una_segunda_apertura_en_la_misma_linea_con_un_paro_ya_abierto_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var primero = await RegistrarParoAsync(jornada, categoriaId: 1, causaId: 1, "Primer paro real de la línea.", usuario);

        var resultado = await RegistrarParoAsync(jornada, categoriaId: 2, causaId: 3, "Segundo intento sobre la misma línea.", usuario);

        resultado.Codigo.Should().Be("PARO_YA_ABIERTO");
        resultado.ParoId.Should().BeNull();
        primero.ParoId.Should().NotBeNull("el primer paro sí debió abrirse sin problema");
    }

    [Fact]
    public async Task Dos_lineas_distintas_pueden_tener_cada_una_su_propio_paro_abierto()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornadaL4 = await JornadaAbiertaAsync(ctx, 4);
        var jornadaL9 = await JornadaAbiertaAsync(ctx, 9);

        var resultadoL4 = await RegistrarParoAsync(jornadaL4, categoriaId: 1, causaId: 1, "Paro en la línea 4.", usuario);
        var resultadoL9 = await RegistrarParoAsync(jornadaL9, categoriaId: 2, causaId: 3, "Paro en la línea 9.", usuario);

        resultadoL4.Codigo.Should().BeNull();
        resultadoL9.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task Reanudar_un_paro_abierto_lo_cierra_con_hora_real_y_registra_quien_reanudo()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var paro = await RegistrarParoAsync(jornada, categoriaId: 1, causaId: 1, "Paro real que luego se reanuda.", usuario);
        var otroUsuario = await CrearUsuarioAsync(ctx);

        var antes = DateTime.UtcNow;
        var resultado = await ReanudarAsync(paro.ParoId!.Value, otroUsuario);

        resultado.Codigo.Should().BeNull();
        var paroDb = await ctx.Paros.AsNoTracking().SingleAsync(p => p.Id == paro.ParoId);
        paroDb.Fin.Should().NotBeNull().And.BeOnOrAfter(antes.AddSeconds(-2));
        paroDb.ReanudadoPor.Should().Be(otroUsuario);

        // Con el paro cerrado, la línea vuelve a poder abrir uno nuevo.
        var siguienteParo = await RegistrarParoAsync(jornada, categoriaId: 2, causaId: 3, "Nuevo paro después de reanudar.", usuario);
        siguienteParo.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task Reanudar_un_paro_inexistente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var resultado = await ReanudarAsync(999_999, usuario);

        resultado.Codigo.Should().Be("PARO_INEXISTENTE");
    }

    [Fact]
    public async Task Reanudar_un_paro_que_ya_estaba_resuelto_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var paro = await RegistrarParoAsync(jornada, categoriaId: 1, causaId: 1, "Paro que se reanuda dos veces.", usuario);
        await ReanudarAsync(paro.ParoId!.Value, usuario);

        var resultado = await ReanudarAsync(paro.ParoId!.Value, usuario);

        resultado.Codigo.Should().Be("PARO_YA_RESUELTO");
    }
}

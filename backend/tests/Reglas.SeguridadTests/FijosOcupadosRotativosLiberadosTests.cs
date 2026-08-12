using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E11.2 (docs/PROGRESO.md): <c>sp_RegistrarParo</c> extendido —
/// §11.1 (fijos ocupados, rotativos liberados) + 00 §C8 (tránsito
/// individual, no en bloque). Mismo patrón de base descartable que el
/// resto de la suite.
/// </summary>
public class FijosOcupadosRotativosLiberadosTests : IAsyncLifetime
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

    private static async Task<(int personalId, int puestoId)> OcuparPuestoAsync(
        SmartAssignDbContext ctx, byte lineaId, int jornadaId, int usuarioId, string tipo)
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba", Tipo = tipo };
        ctx.Puestos.Add(puesto);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operario", Situacion = "asignado" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaId, PuestoId = puesto.Id, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
        return (persona.Id, puesto.Id);
    }

    // ═══ Invocación de sp_RegistrarParo ═══

    private record ResultadoRegistrar(int? ParoId, int? RotativosLiberados, string? Codigo, string? Mensaje);

    private async Task<ResultadoRegistrar> RegistrarParoAsync(int jornadaLineaId, int usuarioId, short categoriaId = 1, short causaId = 1)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            // sp_RegistrarParo (E11.2) resuelve linea_id leyendo JornadaLinea,
            // que tiene RLS (fn_AlcanceLinea) — sin SESSION_CONTEXT la lectura
            // no devuelve filas y el cursor de rotativos nunca encuentra nada.
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
        cmd.Parameters.AddWithValue("@descripcion", "Descripción real del paro observado por el supervisor.");
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
        return new ResultadoRegistrar(pParo.Value as int?, pRotativos.Value as int?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Un_puesto_fijo_ocupado_permanece_ocupado_durante_el_paro()
    {
        // §11.1, literal: "Los puestos fijos permanecen ocupados: los operadores técnicos son quienes ejecutan la reparación".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var (tecnico, puestoFijo) = await OcuparPuestoAsync(ctx, 4, jornada, usuario, tipo: "fijo");

        await RegistrarParoAsync(jornada, usuario);

        var asignacion = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.PersonalId == tecnico);
        asignacion.Fin.Should().BeNull("el técnico del puesto fijo sigue trabajando en la reparación");

        var tecnicoDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == tecnico);
        tecnicoDb.Situacion.Should().Be("asignado");

        (await ctx.Movimientos.CountAsync()).Should().Be(0, "ningún fijo genera tránsito");
    }

    [Fact]
    public async Task Un_puesto_rotativo_ocupado_se_libera_con_su_propio_transito_hacia_la_L8()
    {
        // §11.1 + 00 §C8: "Cada persona genera su propio tránsito con su hora de salida y de llegada".
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var (operario, puestoRotativo) = await OcuparPuestoAsync(ctx, 4, jornada, usuario, tipo: "rotativo");

        var resultado = await RegistrarParoAsync(jornada, usuario);

        resultado.Codigo.Should().BeNull();
        resultado.RotativosLiberados.Should().Be(1);

        var asignacion = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.PersonalId == operario);
        asignacion.Fin.Should().NotBeNull();
        asignacion.MotivoFin.Should().Be("paro");

        var operarioDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == operario);
        operarioDb.Situacion.Should().Be("en_transito");

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.PersonalId == operario);
        movimiento.Motivo.Should().Be("paro");
        movimiento.LineaOrigen.Should().Be((byte)4);
        movimiento.LineaDestino.Should().Be((byte)8, "se reubican en la L8 (§11.1)");
        movimiento.PuestoDestinoId.Should().BeNull("ningún puesto concreto que reservar — es genérico, a diferencia del motor de relevos (B4)");
        movimiento.Estado.Should().Be("en_transito");
    }

    [Fact]
    public async Task Varios_rotativos_ocupados_generan_transitos_individuales_no_uno_en_bloque()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);
        var ocupante1 = await OcuparPuestoAsync(ctx, 4, jornada, usuario, tipo: "rotativo");
        var ocupante2 = await OcuparPuestoAsync(ctx, 4, jornada, usuario, tipo: "rotativo");
        var ocupante3 = await OcuparPuestoAsync(ctx, 4, jornada, usuario, tipo: "rotativo");

        var resultado = await RegistrarParoAsync(jornada, usuario);

        resultado.RotativosLiberados.Should().Be(3);
        (await ctx.Movimientos.CountAsync()).Should().Be(3, "una fila por persona, nunca una fila agregada");

        var personasEnTransito = new[] { ocupante1.personalId, ocupante2.personalId, ocupante3.personalId };
        foreach (var personalId in personasEnTransito)
        {
            (await ctx.Movimientos.AsNoTracking().Where(m => m.PersonalId == personalId).CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task Un_rotativo_ocupado_en_otra_linea_no_se_toca()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornadaL4 = await JornadaAbiertaAsync(ctx, 4);
        var jornadaL9 = await JornadaAbiertaAsync(ctx, 9);
        var (operarioAjeno, _) = await OcuparPuestoAsync(ctx, 9, jornadaL9, usuario, tipo: "rotativo");

        await RegistrarParoAsync(jornadaL4, usuario);

        var asignacionAjena = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.PersonalId == operarioAjeno);
        asignacionAjena.Fin.Should().BeNull("el paro es solo de L4 — L9 sigue produciendo normalmente");
    }

    [Fact]
    public async Task Sin_ningun_rotativo_ocupado_el_paro_se_abre_igual_con_cero_liberados()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaAbiertaAsync(ctx, 4);

        var resultado = await RegistrarParoAsync(jornada, usuario);

        resultado.Codigo.Should().BeNull();
        resultado.ParoId.Should().NotBeNull();
        resultado.RotativosLiberados.Should().Be(0);
    }
}

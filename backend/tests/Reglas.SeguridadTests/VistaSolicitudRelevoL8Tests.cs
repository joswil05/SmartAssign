using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.4 (docs/PROGRESO.md): <c>vw_SolicitudRelevo_L8</c> — Capa 1 del
/// aislamiento de datos de 04 §6.3, literal 00 §D1: el mínimo estricto
/// que la L8 ve de un puesto ajeno, nunca nada de <c>Personal</c>. Mismo
/// patrón de base descartable que el resto de la suite; probada bajo
/// contexto de coordinador (la Capa 2 de alcance por línea es de
/// aplicación, fuera de esta UT).
/// </summary>
public class VistaSolicitudRelevoL8Tests : IAsyncLifetime
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

    private static async Task<int> CrearPuestoAsync(
        SmartAssignDbContext ctx, byte lineaId, string tipo = "rotativo", string? sexoPreferente = null)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba", Tipo = tipo, SexoPreferente = sexoPreferente,
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
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

    private static async Task<short> CrearCapacidadAsync(SmartAssignDbContext ctx, string nombre)
    {
        var c = new CapacidadFisica { Codigo = $"C{Guid.NewGuid():N}"[..10], Nombre = nombre };
        ctx.CapacidadesFisicas.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    private static async Task VincularCapacidadAsync(SmartAssignDbContext ctx, int puestoId, short capacidadId)
    {
        ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puestoId, CapacidadId = capacidadId });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Ocupa el puesto con una persona de nombre reconocible — el nombre nunca debe aparecer en la vista.</summary>
    private static async Task<string> OcuparPuestoAsync(SmartAssignDbContext ctx, int puestoId, byte lineaId, int usuarioId)
    {
        var jornadaId = await JornadaAbiertaAsync(ctx, lineaId);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Identidad Secreta De Prueba", Categoria = "operario" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaId, PuestoId = puestoId, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
        return persona.NombreCompleto;
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

    private async Task InsertarSolicitudAsync(int puestoId, int jornadaLineaId, string nivel = "sugerido", decimal? exceso = 120m, bool resuelta = false)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel, exceso_relativo, resuelta_en, resultado)
            VALUES (@puesto_id, @jornada_linea_id, 'umbral_automatico', @nivel, @exceso,
                    CASE WHEN @resuelta = 1 THEN SYSUTCDATETIME() ELSE NULL END,
                    CASE WHEN @resuelta = 1 THEN 'cubierta' ELSE NULL END);
            """;
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@nivel", nivel);
        cmd.Parameters.AddWithValue("@exceso", (object?)exceso ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@resuelta", resuelta);
        await cmd.ExecuteNonQueryAsync();
    }

    private record FilaVista(
        string LineaCodigo, string PuestoCodigo, string PuestoTipo, string Nivel,
        decimal? ExcesoRelativo, string? PerfilPreferente, string? CapacidadesExigidas);

    private async Task<List<FilaVista>> ConsultarVistaAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT linea_codigo, puesto_codigo, puesto_tipo, nivel, exceso_relativo, perfil_preferente, capacidades_exigidas FROM vw_SolicitudRelevo_L8";
        var resultado = new List<FilaVista>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            resultado.Add(new FilaVista(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return resultado;
    }

    /// <summary>Todo el texto de todas las filas concatenado — para probar ausencia de identidad en bloque.</summary>
    private async Task<string> VolcadoCompletoDeLaVistaAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT * FROM vw_SolicitudRelevo_L8 FOR JSON AUTO, INCLUDE_NULL_VALUES";
        var partes = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) partes.Add(reader.GetString(0));
        return string.Join(Environment.NewLine, partes);
    }

    [Fact]
    public async Task Expone_exactamente_los_campos_permitidos_de_00_D1()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, sexoPreferente: "Masculino");
        var capacidad = await CrearCapacidadAsync(ctx, "Manejo de carga pesada");
        await VincularCapacidadAsync(ctx, puesto, capacidad);
        await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(puesto, jornada, nivel: "critico", exceso: 145.50m);

        var fila = (await ConsultarVistaAsync()).Should().ContainSingle().Subject;

        fila.LineaCodigo.Should().Be("L4");
        fila.PuestoTipo.Should().Be("rotativo");
        fila.Nivel.Should().Be("critico");
        fila.ExcesoRelativo.Should().Be(145.50m);
        fila.PerfilPreferente.Should().Be("Masculino");
        fila.CapacidadesExigidas.Should().Be("Manejo de carga pesada");
    }

    [Fact]
    public async Task Agrega_varias_capacidades_exigidas_separadas_por_coma()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var c1 = await CrearCapacidadAsync(ctx, "Manejo de carga pesada");
        var c2 = await CrearCapacidadAsync(ctx, "Trabajo en altura");
        await VincularCapacidadAsync(ctx, puesto, c1);
        await VincularCapacidadAsync(ctx, puesto, c2);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(puesto, jornada);

        var fila = (await ConsultarVistaAsync()).Should().ContainSingle().Subject;

        fila.CapacidadesExigidas.Should().Contain("Manejo de carga pesada").And.Contain("Trabajo en altura");
    }

    [Fact]
    public async Task Un_puesto_sin_capacidades_exigidas_ni_perfil_preferente_muestra_null_no_vacio()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4); // sin capacidades, sin sexo preferente
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(puesto, jornada);

        var fila = (await ConsultarVistaAsync()).Should().ContainSingle().Subject;

        fila.PerfilPreferente.Should().BeNull();
        fila.CapacidadesExigidas.Should().BeNull();
    }

    [Fact]
    public async Task Una_solicitud_ya_resuelta_no_aparece_en_la_vista()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(puesto, jornada, resuelta: true);

        (await ConsultarVistaAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task La_vista_nunca_expone_la_identidad_del_ocupante_ni_en_ninguna_columna()
    {
        // 00 §D1, literal: "nombre, ficha ni foto del operario... NUNCA".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var nombreOcupante = await OcuparPuestoAsync(ctx, puesto, lineaId: 4, usuario);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 4);
        await InsertarSolicitudAsync(puesto, jornada);

        var volcado = await VolcadoCompletoDeLaVistaAsync();

        volcado.Should().NotBeEmpty();
        volcado.Should().NotContain(nombreOcupante);
        volcado.Should().NotContain("Secreta");
    }

    [Fact]
    public async Task Muestra_puestos_de_varias_lineas_a_la_vez()
    {
        // §9.4 p2: la L8 opera sobre puestos de OTRAS líneas, no solo una.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puestoL4 = await CrearPuestoAsync(ctx, lineaId: 4);
        var puestoL1 = await CrearPuestoAsync(ctx, lineaId: 1);
        var jornadaL4 = await JornadaAbiertaAsync(ctx, lineaId: 4);
        var jornadaL1 = await JornadaAbiertaAsync(ctx, lineaId: 1);
        await InsertarSolicitudAsync(puestoL4, jornadaL4);
        await InsertarSolicitudAsync(puestoL1, jornadaL1);

        var filas = await ConsultarVistaAsync();

        filas.Select(f => f.LineaCodigo).Should().BeEquivalentTo(["L4", "L1"]);
    }
}

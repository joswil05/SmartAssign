using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E10.2 (docs/PROGRESO.md): "Solo con la L8 completamente vacía" —
/// §9.6, literal: "Esto es distinto del caso de capacidad limitada del
/// §9.4: mientras la L8 tenga aunque sea una persona disponible y
/// compatible, se usa esa persona antes de recurrir a la extracción
/// inversa. La extracción inversa solo se activa cuando la L8 está
/// completamente vacía de candidatos viables."
///
/// Sin objeto SQL nuevo — el catálogo de 04 §7.4 describe
/// <c>sp_ExtraccionInversa</c> como "orden derivado (E10.1, A5) + piso de
/// seguridad (E10.3, B5)" en una sola línea: el procedimiento completo
/// se construye recién en E10.3, cuando exista también el piso. Esta UT
/// prueba que el disparador de E10.2 ya existe y es correcto:
/// <c>sp_ProponerRelevista</c> (E9.5) YA devuelve
/// <c>SIN_CANDIDATOS_EN_BOLSON</c> exactamente cuando "la L8 está
/// completamente vacía de candidatos viables" para un puesto — es la
/// misma condición, no una nueva. Mismo criterio de "sin mecanismo
/// nuevo, solo se prueba lo que ya existe" que E8.2 (tránsito inmune) y
/// E9.9 (arquitectura). <c>sp_ExtraccionInversa</c> (E10.3) usará este
/// mismo código como su primer chequeo.
/// </summary>
public class L8VaciaParaExtraccionTests : IAsyncLifetime
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

    private static async Task<int> CrearPersonaEnBolsonAsync(SmartAssignDbContext ctx, string categoria = "operario")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Candidato de prueba", Categoria = categoria,
            Situacion = "en_bolson", LineaFisicaActual = 8,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Alguien trabajando de verdad en OTRA línea — plantel abundante en planta, pero irrelevante para el Bolsón de este puesto.</summary>
    private static async Task OcuparOtraLineaAsync(SmartAssignDbContext ctx, byte lineaId, int usuarioId)
    {
        var puestoAjeno = await CrearPuestoAsync(ctx, lineaId);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupado en otra línea", Categoria = "operario" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada, PuestoId = puestoAjeno, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task<string?> ProponerCodigoAsync(int puestoId)
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
        cmd.Parameters.Add(new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@cede_perfil", SqlDbType.Bit) { Direction = ParameterDirection.Output });
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return pCodigo.Value as string;
    }

    [Fact]
    public async Task Con_al_menos_un_candidato_disponible_y_compatible_no_se_activa_la_extraccion_inversa()
    {
        // §9.4/§9.6, literal: "mientras la L8 tenga aunque sea una persona
        // disponible y compatible, se usa esa persona antes de recurrir
        // a la extracción inversa" — un único candidato basta.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        await CrearPersonaEnBolsonAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);

        var codigo = await ProponerCodigoAsync(puesto);

        codigo.Should().BeNull("hay un candidato viable — la extracción inversa no debe considerarse todavía");
    }

    [Fact]
    public async Task Con_el_Bolson_completamente_vacio_el_disparador_de_extraccion_inversa_se_activa()
    {
        // §9.6, literal: "la extracción inversa SOLO se activa cuando la
        // L8 está completamente vacía de candidatos viables".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4); // nadie en el Bolsón en absoluto

        var codigo = await ProponerCodigoAsync(puesto);

        codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON", "esta es la señal — sp_ExtraccionInversa (E10.3) la usará como su primer chequeo");
    }

    [Fact]
    public async Task Personal_ocupado_en_otras_lineas_nunca_cuenta_como_candidato_del_Bolson()
    {
        // "Vacía" es del Bolsón, no de la planta entera — alguien trabajando
        // en otra línea no es un candidato viable para esta L8, sin importar
        // cuánta gente haya en total.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await OcuparOtraLineaAsync(ctx, lineaId: 1, usuario);
        await OcuparOtraLineaAsync(ctx, lineaId: 2, usuario);
        await OcuparOtraLineaAsync(ctx, lineaId: 6, usuario);
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);

        var codigo = await ProponerCodigoAsync(puesto);

        codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
    }

    [Fact]
    public async Task Si_el_unico_candidato_del_Bolson_es_incompatible_tambien_se_activa_el_disparador()
    {
        // 00 §A8: incompatibilidad de categoría es uno de los tres únicos
        // motivos legítimos por los que la L8 "no tiene" candidato.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        await CrearPersonaEnBolsonAsync(ctx, categoria: "averiero"); // incompatible con rotativo
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);

        var codigo = await ProponerCodigoAsync(puesto);

        codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
    }
}

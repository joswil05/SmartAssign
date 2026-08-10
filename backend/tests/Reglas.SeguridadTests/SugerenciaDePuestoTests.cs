using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E6.7 (docs/PROGRESO.md) — <c>sp_SugerirPuesto</c>: la escalera de 4
/// niveles de la fuente §8.5, contra la base real (mismo patrón que
/// <see cref="MotorDeValidacionTests"/>: RLS de <c>Puesto</c> exige
/// contexto de coordinador, LocalDB descartable por prueba).
/// </summary>
public class SugerenciaDePuestoTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != System.Data.ConnectionState.Open) await conexion.OpenAsync();
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

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, string categoria, string? sexo = null)
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = categoria, Sexo = sexo, Situacion = "presente_sin_asignar",
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoRotativoAsync(SmartAssignDbContext ctx, byte lineaId,
        string? codigo = null, int? titularId = null, string? sexoPreferente = null)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId,
            Codigo = codigo ?? $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto rotativo de prueba",
            Tipo = "rotativo",
            TitularId = titularId,
            SexoPreferente = sexoPreferente,
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task<int> CrearPuestoFijoAsync(SmartAssignDbContext ctx, byte lineaId, string categoriaTitular)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"F{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto fijo de prueba",
            Tipo = "fijo", CategoriaTitular = categoriaTitular,
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

    /// <summary>Ocupa un puesto (deja de estar "libre") — para probar que la escalera nunca sugiere lo ya asignado.</summary>
    private static async Task OcuparPuestoAsync(SmartAssignDbContext ctx, int puestoId, byte lineaId, int usuarioId)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        var ocupante = await CrearPersonaAsync(ctx, "operario");
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada.Id, PuestoId = puestoId, PersonalId = ocupante,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task VincularCapacidadAsync(SmartAssignDbContext ctx, int puestoId, short capacidadId)
    {
        ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puestoId, CapacidadId = capacidadId });
        await ctx.SaveChangesAsync();
    }

    private static async Task CrearRestriccionAsync(SmartAssignDbContext ctx, int personalId, short capacidadId, int registradoPor)
    {
        ctx.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = personalId, CapacidadId = capacidadId,
            FechaInicio = new DateOnly(2020, 1, 1), FechaFin = null, FechaDictamen = new DateOnly(2020, 1, 1),
            Fuente = "Enfermería", RegistradoPor = registradoPor,
        });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación del SP ═══

    private record Sugerencia(int? PuestoId, byte? Nivel, string? Codigo, string? Mensaje);

    private async Task<Sugerencia> SugerirAsync(int personalId, byte lineaId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var contexto = conexion.CreateCommand();
        contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await contexto.ExecuteNonQueryAsync();

        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_SugerirPuesto";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pPuesto = new SqlParameter("@puesto_id_sugerido", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
        var pNivel = new SqlParameter("@nivel", System.Data.SqlDbType.TinyInt) { Direction = System.Data.ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", System.Data.SqlDbType.VarChar, 40) { Direction = System.Data.ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", System.Data.SqlDbType.NVarChar, 400) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pPuesto);
        cmd.Parameters.Add(pNivel);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);

        await cmd.ExecuteNonQueryAsync();
        return new Sugerencia(
            pPuesto.Value as int?, pNivel.Value as byte?,
            pCodigo.Value as string, pMensaje.Value as string);
    }

    // ═══ Nivel 1 — el puesto titular, cumpliendo todo (§8.5 N1) ═══

    [Fact]
    public async Task Nivel_1_sugiere_el_puesto_cuyo_titular_es_la_misma_persona()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puestoTitular = await CrearPuestoRotativoAsync(ctx, 1, titularId: persona);
        await CrearPuestoRotativoAsync(ctx, 1); // otro puesto libre, no debe ganarle al de titular

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().Be(puestoTitular);
        sugerencia.Nivel.Should().Be(1);
        sugerencia.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task Nivel_1_no_aplica_si_el_puesto_titular_ya_esta_ocupado()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puestoTitular = await CrearPuestoRotativoAsync(ctx, 1, titularId: persona);
        await OcuparPuestoAsync(ctx, puestoTitular, 1, usuario);
        var otroLibre = await CrearPuestoRotativoAsync(ctx, 1);

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().Be(otroLibre);
        sugerencia.Nivel.Should().Be(3, "el titular ya no está libre — cae directo a N3, ninguna otra regla se lo impide");
    }

    // ═══ Nivel 2 — el mismo puesto, cediendo el perfil preferente (§8.5 N2) ═══

    [Fact]
    public async Task Nivel_2_sugiere_el_puesto_titular_cediendo_el_perfil_preferente()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "masculino");
        var puestoTitular = await CrearPuestoRotativoAsync(ctx, 1, titularId: persona, sexoPreferente: "Femenino");

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().Be(puestoTitular);
        sugerencia.Nivel.Should().Be(2, "N1 exige también el perfil preferente; solo N2 lo cede");
    }

    // ═══ Nivel 3 — cualquier otro puesto libre compatible, todas las reglas (§8.5 N3) ═══

    [Fact]
    public async Task Nivel_3_sugiere_otro_puesto_libre_cuando_no_hay_titular()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoRotativoAsync(ctx, 1, codigo: "L1-R05");

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().Be(puesto);
        sugerencia.Nivel.Should().Be(3);
    }

    [Fact]
    public async Task Nivel_3_elige_el_puesto_de_codigo_menor_entre_varios_compatibles()
    {
        // §8.5 no especifica el orden entre "cualquier puesto compatible"
        // — el criterio determinista es de ingeniería (ver el comentario
        // de la migración), y esta prueba lo fija en verde.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        await CrearPuestoRotativoAsync(ctx, 1, codigo: "L1-R09");
        var elDeMenorCodigo = await CrearPuestoRotativoAsync(ctx, 1, codigo: "L1-R02");
        await CrearPuestoRotativoAsync(ctx, 1, codigo: "L1-R05");

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().Be(elDeMenorCodigo);
    }

    [Fact]
    public async Task Nivel_3_nunca_sugiere_un_puesto_fijo()
    {
        // §8.5 vive dentro de "Etapa 4 — llenado de rotativos" (§8.4): los
        // fijos ya los resolvió el barrido automático (§8.3) antes.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operador_a");
        await CrearPuestoFijoAsync(ctx, 1, categoriaTitular: "operador_a"); // libre, compatible, pero FIJO

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().BeNull();
        sugerencia.Codigo.Should().Be("SIN_PUESTOS_LIBRES");
    }

    // ═══ Nivel 4 — cualquier otro puesto libre, cediendo el perfil (§8.5 N4) ═══

    [Fact]
    public async Task Nivel_4_sugiere_un_puesto_ajeno_cediendo_el_perfil_preferente()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "masculino");
        var puesto = await CrearPuestoRotativoAsync(ctx, 1, sexoPreferente: "Femenino");

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().Be(puesto);
        sugerencia.Nivel.Should().Be(4);
    }

    // ═══ Las restricciones médicas nunca ceden, en ningún nivel (§8.5) ═══

    [Fact]
    public async Task Restriccion_medica_bloquea_incluso_en_el_nivel_4_mas_permisivo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "masculino");
        var puesto = await CrearPuestoRotativoAsync(ctx, 1, sexoPreferente: "Femenino"); // cedería en N4...
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, usuario); // ...pero la médica nunca cede

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().BeNull();
        sugerencia.Codigo.Should().Be("RESTRICCION_MEDICA");
        sugerencia.Mensaje.Should().NotBeNullOrWhiteSpace("§8.5: 'debe decir cuál regla lo impidió'");
    }

    // ═══ Nada aplica en ningún nivel — nunca silencio (§8.5) ═══

    [Fact]
    public async Task Sin_ningun_puesto_libre_explica_la_ausencia_en_vez_de_callar()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoRotativoAsync(ctx, 1);
        await OcuparPuestoAsync(ctx, puesto, 1, usuario);

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().BeNull();
        sugerencia.Nivel.Should().BeNull();
        sugerencia.Codigo.Should().Be("SIN_PUESTOS_LIBRES");
        sugerencia.Mensaje.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Categoria_incompatible_en_el_unico_puesto_libre_explica_esa_regla()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "averiero"); // no compatible con rotativo (§4.2: solo operario/operador_b)
        await CrearPuestoRotativoAsync(ctx, 1);

        var sugerencia = await SugerirAsync(persona, 1, usuario);

        sugerencia.PuestoId.Should().BeNull();
        sugerencia.Codigo.Should().Be("CATEGORIA_INCOMPATIBLE");
    }

    // ═══ Alcance de línea — nunca sugiere un puesto de otra línea ═══

    [Fact]
    public async Task Nunca_sugiere_un_puesto_libre_de_otra_linea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        await CrearPuestoRotativoAsync(ctx, 2); // libre y compatible, pero en L2

        var sugerencia = await SugerirAsync(persona, 1, usuario); // se pregunta por L1

        sugerencia.PuestoId.Should().BeNull();
        sugerencia.Codigo.Should().Be("SIN_PUESTOS_LIBRES");
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.7 (docs/PROGRESO.md): <c>sp_SugerirDestinoRelevado</c> — §9.4
/// paso 6, escalera de 00 §B4 (misma línea → proximidad de A1 → L8),
/// nunca prioridad (A9). Mismo patrón de base descartable que el resto
/// de la suite.
/// </summary>
public class SugerirDestinoRelevadoTests : IAsyncLifetime
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

    private static async Task<int> CrearRelevadoAsync(SmartAssignDbContext ctx, string categoria = "operario")
    {
        // "presente_sin_asignar": la asignación anterior ya se cerró (fuera del alcance de esta UT, ver comentario de la migración).
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Relevado de prueba",
            Categoria = categoria, Situacion = "presente_sin_asignar",
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

    /// <summary>Crea un puesto rotativo fatigado (u ocupado sin fatigar) — el ocupante nunca es el relevado.</summary>
    private static async Task<int> CrearPuestoFatigadoAsync(SmartAssignDbContext ctx, byte lineaId, int minutosOcupado)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba",
            Tipo = "rotativo", HorasEnPuesto = 1, // umbral sugerido: 60 min
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();

        var jornada = await JornadaAbiertaAsync(ctx, lineaId);
        var ocupante = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operario" };
        ctx.Personas.Add(ocupante);
        await ctx.SaveChangesAsync();
        var usuarioAsignador = await CrearUsuarioAsync(ctx);
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada, PuestoId = puesto.Id, PersonalId = ocupante.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddMinutes(-minutosOcupado), AsignadoPor = usuarioAsignador,
        });
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    /// <summary>Reserva un puesto (guarda de B4) sin usar el flujo completo de despacho — persona de relleno cualquiera.</summary>
    private async Task ReservarPuestoAsync(SmartAssignDbContext ctx, int puestoId, byte lineaOrigen)
    {
        var relevista = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Relevista de prueba", Categoria = "operario", Situacion = "en_bolson", LineaFisicaActual = 8 };
        ctx.Personas.Add(relevista);
        await ctx.SaveChangesAsync();
        var usuario = await CrearUsuarioAsync(ctx);

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, puesto_destino_id, motivo, despachado_por)
            VALUES (@personal_id, @linea_origen, @linea_destino, @puesto_destino_id, 'relevo', @usuario_id);
            """;
        cmd.Parameters.AddWithValue("@personal_id", relevista.Id);
        cmd.Parameters.AddWithValue("@linea_origen", lineaOrigen);
        cmd.Parameters.AddWithValue("@linea_destino", (await ctx.Puestos.AsNoTracking().Where(p => p.Id == puestoId).Select(p => p.LineaId).SingleAsync()));
        cmd.Parameters.AddWithValue("@puesto_destino_id", puestoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuario);
        await cmd.ExecuteNonQueryAsync();
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

    private record Sugerencia(int? PuestoId, byte? LineaSugerida, string? Codigo);

    private async Task<Sugerencia> SugerirAsync(int personalId, byte lineaActual)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_SugerirDestinoRelevado";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_actual", lineaActual);
        var pPuesto = new SqlParameter("@puesto_id_sugerido", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pLinea = new SqlParameter("@linea_sugerida", SqlDbType.TinyInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pPuesto);
        cmd.Parameters.Add(pLinea);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new Sugerencia(pPuesto.Value as int?, pLinea.Value as byte?, pCodigo.Value as string);
    }

    [Fact]
    public async Task Prefiere_un_puesto_fatigado_de_su_propia_linea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var relevado = await CrearRelevadoAsync(ctx);
        var puestoPropia = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 65);
        await CrearPuestoFatigadoAsync(ctx, lineaId: 2, minutosOcupado: 90); // L2 es la más cercana de L4 — igual pierde contra "misma línea"

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().Be(puestoPropia);
        sugerencia.LineaSugerida.Should().Be((byte)4);
    }

    [Fact]
    public async Task Entre_varios_fatigados_de_su_propia_linea_gana_el_de_mayor_exceso_relativo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var relevado = await CrearRelevadoAsync(ctx);
        var puestoBajo = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 65);  // ~108%
        var puestoAlto = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 150); // ~250%

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().Be(puestoAlto);
    }

    [Fact]
    public async Task Sin_fatiga_en_su_linea_recorre_la_proximidad_de_A1_en_su_propio_orden()
    {
        // L4 → L2, L1, L7, ... (00 §A1). Ningún puesto fatigado en L4, uno en L2 (primera de la fila).
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var relevado = await CrearRelevadoAsync(ctx);
        var puestoEnL2 = await CrearPuestoFatigadoAsync(ctx, lineaId: 2, minutosOcupado: 65);

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().Be(puestoEnL2);
        sugerencia.LineaSugerida.Should().Be((byte)2);
    }

    [Fact]
    public async Task Respeta_el_orden_de_proximidad_no_el_de_prioridad_de_lineas()
    {
        // 00 §A9, literal: "el motor de relevos se rige únicamente por proximidad,
        // nunca prioridad". L4 → A1 dice L2 antes que L1; A5/§3.3 (prioridad) diría L4>L1>L2.
        // Si el motor usara prioridad por error, elegiría L1. Debe elegir L2.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var relevado = await CrearRelevadoAsync(ctx);
        var puestoEnL1 = await CrearPuestoFatigadoAsync(ctx, lineaId: 1, minutosOcupado: 65);
        var puestoEnL2 = await CrearPuestoFatigadoAsync(ctx, lineaId: 2, minutosOcupado: 65);

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().Be(puestoEnL2, "L2 va antes que L1 en la fila de proximidad de L4 (A1)");
        sugerencia.PuestoId.Should().NotBe(puestoEnL1);
    }

    [Fact]
    public async Task Un_puesto_ya_reservado_por_otro_relevista_se_descarta()
    {
        // 00 §B4, la guarda de convergencia — mismo espíritu que E8.5.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var relevado = await CrearRelevadoAsync(ctx);
        var puestoReservado = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 200); // el de mayor exceso...
        var puestoLibre = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 65);
        await ReservarPuestoAsync(ctx, puestoReservado, lineaOrigen: 8);

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().Be(puestoLibre, "el de mayor exceso ya está reservado — se salta");
    }

    [Fact]
    public async Task Un_puesto_bloqueado_por_restriccion_medica_se_salta_por_el_siguiente()
    {
        // Parte VII se aplica siempre — restricción médica nunca cede (§7.2).
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var relevado = await CrearRelevadoAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var bloqueado = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 200); // el de mayor exceso...
        var disponible = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 65);
        var capacidad = await CrearCapacidadAsync(ctx, "Manejo de carga pesada");
        await VincularCapacidadAsync(ctx, bloqueado, capacidad);
        await CrearRestriccionAsync(ctx, relevado, capacidad, usuario);

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().Be(disponible, "el de mayor exceso está bloqueado por restricción médica — se salta");
    }

    [Fact]
    public async Task El_relevado_puede_ser_sugerido_a_un_puesto_fatigado_ocupado_para_relevar_directamente()
    {
        // §9.4, ejemplo normativo, literal: "cada relevado pasa a relevar
        // a uno de esos 2 compañeros" — el destino está OCUPADO a propósito,
        // nunca libre. Si sp_SugerirDestinoRelevado exigiera "puesto libre"
        // (como sp_ValidarAsignacion paso 1), esta prueba fallaría siempre.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var relevado = await CrearRelevadoAsync(ctx);
        var puesto = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, minutosOcupado: 65);

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().Be(puesto);
    }

    [Fact]
    public async Task Sin_ningun_puesto_fatigado_en_todo_el_recorrido_sugiere_la_L8_sin_puesto_concreto()
    {
        await using var ctx = CrearContexto();
        var relevado = await CrearRelevadoAsync(ctx);

        var sugerencia = await SugerirAsync(relevado, lineaActual: 4);

        sugerencia.PuestoId.Should().BeNull();
        sugerencia.LineaSugerida.Should().Be((byte)8, "la única línea con es_bolson=1");
        sugerencia.Codigo.Should().BeNull("ir a la L8 es un resultado válido, no un rechazo");
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.5 (docs/PROGRESO.md): <c>sp_ProponerRelevista</c> — el ranking
/// exacto de 00 §B2, contra la base real. Mismo patrón de base
/// descartable que el resto de la suite.
/// </summary>
public class ProponerRelevistaTests : IAsyncLifetime
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

    private static async Task<int> CrearPersonaEnBolsonAsync(
        SmartAssignDbContext ctx, string ficha, string categoria = "operario", string? sexo = null)
    {
        var p = new Personal
        {
            Ficha = ficha, NombreCompleto = "Candidato de prueba", Categoria = categoria,
            Sexo = sexo, Situacion = "en_bolson", LineaFisicaActual = 8,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoAsync(
        SmartAssignDbContext ctx, byte lineaId, int? titularId = null, string? sexoPreferente = null)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba", Tipo = "rotativo", TitularId = titularId, SexoPreferente = sexoPreferente,
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

    /// <summary>Simula que ya llegó al Bolsón hace @minutosAtras — único camino real hoy hacia en_bolson (E8.3).</summary>
    private async Task InsertarLlegadaAlBolsonAsync(int personalId, int usuarioId, int minutosAtras)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Movimiento (personal_id, linea_origen, linea_destino, motivo, estado, hora_salida, hora_llegada, despachado_por)
            VALUES (@personal_id, 4, 8, 'relevo', 'recibido',
                    DATEADD(MINUTE, -@minutos - 5, SYSUTCDATETIME()),
                    DATEADD(MINUTE, -@minutos, SYSUTCDATETIME()),
                    @usuario_id);
            """;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@minutos", minutosAtras);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Fatiga acumulada de HOY para el ranking — una Asignacion cerrada, ajena al puesto que se está proponiendo.
    /// Ancla el inicio a primera hora de hoy (UTC), nunca a "ahora menos N minutos": el SP filtra por
    /// CAST(inicio AS DATE) = hoy, y "ahora menos N" puede cruzar medianoche real y quedar fuera sin querer.
    /// </summary>
    private static async Task CrearFatigaAcumuladaHoyAsync(
        SmartAssignDbContext ctx, int personalId, int usuarioId, int minutosTrabajadosHoy)
    {
        var puestoAjeno = await CrearPuestoAsync(ctx, lineaId: 2);
        var jornada = await JornadaAbiertaAsync(ctx, lineaId: 2);
        var inicioDeHoy = DateTime.UtcNow.Date.AddHours(1);
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada, PuestoId = puestoAjeno, PersonalId = personalId,
            Origen = "manual_supervisor", Inicio = inicioDeHoy, Fin = inicioDeHoy.AddMinutes(minutosTrabajadosHoy), AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación del SP ═══

    private record Propuesta(int? CandidatoId, bool? CedePerfil, string? Codigo, string? Mensaje);

    private async Task<Propuesta> ProponerAsync(int puestoId)
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
        var pCandidato = new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCede = new SqlParameter("@cede_perfil", SqlDbType.Bit) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCandidato);
        cmd.Parameters.Add(pCede);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new Propuesta(pCandidato.Value as int?, pCede.Value as bool?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task El_titular_del_puesto_gana_sobre_cualquier_otro_candidato()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var titular = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var otro = await CrearPersonaEnBolsonAsync(ctx, "F0002");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, titularId: titular);
        // "otro" lleva más tiempo y menos fatiga — igual pierde contra el titular (criterio 1).
        await InsertarLlegadaAlBolsonAsync(otro, usuario, minutosAtras: 500);

        var propuesta = await ProponerAsync(puesto);

        propuesta.CandidatoId.Should().Be(titular);
    }

    [Fact]
    public async Task Sin_titular_disponible_gana_quien_lleva_mas_tiempo_en_el_Bolson()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var antiguo = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var reciente = await CrearPersonaEnBolsonAsync(ctx, "F0002");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        await InsertarLlegadaAlBolsonAsync(antiguo, usuario, minutosAtras: 60);
        await InsertarLlegadaAlBolsonAsync(reciente, usuario, minutosAtras: 5);

        var propuesta = await ProponerAsync(puesto);

        propuesta.CandidatoId.Should().Be(antiguo);
    }

    [Fact]
    public async Task Empatados_en_tiempo_en_Bolson_gana_quien_tiene_menos_fatiga_acumulada_hoy()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var descansado = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var desgastado = await CrearPersonaEnBolsonAsync(ctx, "F0002");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        // Ninguno tiene Movimiento real — empatan en el criterio 2 (ambos NULL).
        await CrearFatigaAcumuladaHoyAsync(ctx, descansado, usuario, minutosTrabajadosHoy: 10);
        await CrearFatigaAcumuladaHoyAsync(ctx, desgastado, usuario, minutosTrabajadosHoy: 300);

        var propuesta = await ProponerAsync(puesto);

        propuesta.CandidatoId.Should().Be(descansado);
    }

    [Fact]
    public async Task Empatados_en_todo_gana_ficha_ascendente()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var candidatoB = await CrearPersonaEnBolsonAsync(ctx, "F9999");
        var candidatoA = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);

        var propuesta = await ProponerAsync(puesto);

        propuesta.CandidatoId.Should().Be(candidatoA, "F0001 es alfabéticamente anterior a F9999");
    }

    [Fact]
    public async Task El_perfil_preferente_ordena_pero_no_excluye()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var cumple = await CrearPersonaEnBolsonAsync(ctx, "F0001", sexo: "masculino");
        var noCumple = await CrearPersonaEnBolsonAsync(ctx, "F0002", sexo: "femenino");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, sexoPreferente: "Masculino");

        var propuesta = await ProponerAsync(puesto);

        propuesta.CandidatoId.Should().Be(cumple);
        propuesta.CedePerfil.Should().BeFalse();
    }

    [Fact]
    public async Task Si_todos_fallan_el_perfil_se_propone_igual_marcado_como_cede_perfil()
    {
        // 00 §B2, literal: "se propone si no hay otro, marcado explícitamente como tal".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var unico = await CrearPersonaEnBolsonAsync(ctx, "F0001", sexo: "femenino");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4, sexoPreferente: "Masculino");

        var propuesta = await ProponerAsync(puesto);

        propuesta.CandidatoId.Should().Be(unico);
        propuesta.CedePerfil.Should().BeTrue();
    }

    [Fact]
    public async Task Una_categoria_incompatible_excluye_al_candidato()
    {
        await using var ctx = CrearContexto();
        await CrearPersonaEnBolsonAsync(ctx, "F0001", categoria: "averiero"); // no compatible con rotativo
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);

        var propuesta = await ProponerAsync(puesto);

        propuesta.Codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
    }

    [Fact]
    public async Task Una_restriccion_medica_bloqueante_excluye_al_candidato()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaEnBolsonAsync(ctx, "F0001");
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);
        var capacidad = await CrearCapacidadAsync(ctx, "Manejo de carga pesada");
        await VincularCapacidadAsync(ctx, puesto, capacidad);
        await CrearRestriccionAsync(ctx, persona, capacidad, usuario);

        var propuesta = await ProponerAsync(puesto);

        propuesta.Codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
    }

    [Fact]
    public async Task Sin_nadie_en_el_bolson_no_hay_candidatos()
    {
        await using var ctx = CrearContexto();
        var puesto = await CrearPuestoAsync(ctx, lineaId: 4);

        var propuesta = await ProponerAsync(puesto);

        propuesta.CandidatoId.Should().BeNull();
        propuesta.Codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
        propuesta.Mensaje.Should().NotBeNullOrEmpty("nunca silencio (§9.4)");
    }
}

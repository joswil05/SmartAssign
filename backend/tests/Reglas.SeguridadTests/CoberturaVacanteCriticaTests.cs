using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E10.4 (docs/PROGRESO.md): <c>sp_CubrirVacanteCritica</c> — 00
/// §C15, la escalera N1→N4 con guarda anti-dominó. Mismo patrón de base
/// descartable que el resto de la suite.
/// </summary>
public class CoberturaVacanteCriticaTests : IAsyncLifetime
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
        var u = new Usuario
        {
            Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba",
            Rol = "coordinador", OrigenIdentidad = "local", Activo = true,
        };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    /// <summary>Jornada realmente arrancada (SKU + ArrancadoEn) — condición literal de fn_EsVacanteCritica.</summary>
    private static async Task<int> JornadaArrancadaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var existente = await ctx.JornadasLinea.Where(j => j.LineaId == lineaId && j.CerradoEn == null).Select(j => j.Id).SingleOrDefaultAsync();
        if (existente != 0) return existente;

        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100 };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea
        {
            LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1),
            SkuId = sku.Id, Estado = "arrancada", ArrancadoEn = DateTime.UtcNow,
        };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    /// <summary>Puesto FIJO, titular operador_a, sin ocupante — vacante crítica (00 §5.3) en una línea ya arrancada.</summary>
    private static async Task<int> CrearVacanteCriticaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        await JornadaArrancadaAsync(ctx, lineaId);
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = $"F{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto fijo de prueba", Tipo = "fijo", CategoriaTitular = "operador_a",
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    /// <summary>Ocupa un rotativo nuevo de la línea con una persona real de la categoría dada — dotación que cuenta contra el piso (B5).</summary>
    private static async Task<(int personalId, int puestoId)> OcuparRotativoAsync(
        SmartAssignDbContext ctx, byte lineaId, string categoria, int usuarioId)
    {
        var jornada = await JornadaArrancadaAsync(ctx, lineaId);
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"R{Guid.NewGuid():N}"[..15], NombrePuesto = "Rotativo de prueba", Tipo = "rotativo" };
        ctx.Puestos.Add(puesto);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = categoria };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada, PuestoId = puesto.Id, PersonalId = persona.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
        return (persona.Id, puesto.Id);
    }

    private static async Task<int> CrearCandidatoEnBolsonAsync(SmartAssignDbContext ctx, string categoria = "operador_b")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Candidato del Bolsón",
            Categoria = categoria, Situacion = "en_bolson", LineaFisicaActual = 8,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task SetPisoAsync(byte lineaId, short? minimo)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "UPDATE Linea SET minimo_operarios = @min WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@min", (object?)minimo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", lineaId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ═══ Invocación del SP ═══

    private record Resultado(
        string? NivelAplicado, int? CandidatoId, byte? LineaOrigen, long? SolicitudId,
        long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<Resultado> CubrirAsync(int puestoVacante, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CubrirVacanteCritica";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@puesto_id_vacante", puestoVacante);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pNivel = new SqlParameter("@nivel_aplicado", SqlDbType.VarChar, 2) { Direction = ParameterDirection.Output };
        var pCandidato = new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pLinea = new SqlParameter("@linea_origen", SqlDbType.TinyInt) { Direction = ParameterDirection.Output };
        var pSolicitud = new SqlParameter("@solicitud_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pMovimiento = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pNivel);
        cmd.Parameters.Add(pCandidato);
        cmd.Parameters.Add(pLinea);
        cmd.Parameters.Add(pSolicitud);
        cmd.Parameters.Add(pMovimiento);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new Resultado(
            pNivel.Value as string, pCandidato.Value as int?, pLinea.Value as byte?, pSolicitud.Value as long?,
            pMovimiento.Value as long?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task N1_Con_candidato_compatible_en_el_bolson_abre_solicitud_de_maxima_prioridad_sin_hueco()
    {
        // 00 §B3, literal: "una solicitud generada por vacante crítica de
        // puesto fijo (C15-N1) encabeza la cola por delante de cualquier fatiga".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearVacanteCriticaAsync(ctx, lineaId: 4);
        var candidato = await CrearCandidatoEnBolsonAsync(ctx);

        var resultado = await CubrirAsync(puesto, usuario);

        resultado.NivelAplicado.Should().Be("N1");
        resultado.CandidatoId.Should().Be(candidato);
        resultado.MovimientoId.Should().BeNull("N1 lo ejecuta el supervisor de L8 por el flujo estándar, no este procedimiento");
        resultado.SolicitudId.Should().NotBeNull();

        var solicitud = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == resultado.SolicitudId);
        solicitud.PuestoId.Should().Be(puesto);
        solicitud.Nivel.Should().Be("maxima");
        solicitud.Origen.Should().Be("vacante_critica");
        solicitud.ResueltaEn.Should().BeNull();
    }

    [Fact]
    public async Task N2_Sin_nadie_en_bolson_extrae_al_operador_b_de_la_misma_linea_y_abre_la_guarda_anti_domino()
    {
        // C15, literal: "el que le sigue es el Operador B [...] se debe
        // ejecutar la rotación, porque dejará un puesto vacío" + guarda:
        // "entra a la cola de relevos pendientes a prioridad normal".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoVacante = await CrearVacanteCriticaAsync(ctx, lineaId: 4);
        var (operadorB, puestoDonante) = await OcuparRotativoAsync(ctx, lineaId: 4, categoria: "operador_b", usuario);

        var resultado = await CubrirAsync(puestoVacante, usuario);

        resultado.NivelAplicado.Should().Be("N2");
        resultado.CandidatoId.Should().Be(operadorB);
        resultado.LineaOrigen.Should().Be((byte)4);
        resultado.MovimientoId.Should().NotBeNull();
        resultado.Codigo.Should().BeNull();

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.Motivo.Should().Be("cobertura_vacante_critica");
        movimiento.LineaOrigen.Should().Be((byte)4);
        movimiento.LineaDestino.Should().Be((byte)4);
        movimiento.PuestoDestinoId.Should().Be(puestoVacante);
        movimiento.Estado.Should().Be("en_transito");

        var extraido = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == operadorB);
        extraido.Situacion.Should().Be("en_transito");

        var asignacionCerrada = await ctx.Asignaciones.AsNoTracking().Where(a => a.PersonalId == operadorB).SingleAsync();
        asignacionCerrada.Fin.Should().NotBeNull();

        var domino = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == resultado.SolicitudId);
        domino.PuestoId.Should().Be(puestoDonante, "el rotativo que el Operador B deja vacío entra a la cola, no el puesto fijo original");
        domino.Nivel.Should().Be("sugerido", "prioridad NORMAL — nunca una emergencia nueva (guarda anti-dominó)");
        domino.Origen.Should().Be("manual_supervisor");
    }

    [Fact]
    public async Task N2_Un_operador_a_ocupando_un_rotativo_no_es_candidato_valido_solo_cuenta_el_operador_B()
    {
        // C15, literal: "el que le sigue es el Operador B" — no cualquier
        // categoría compatible con el puesto fijo.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoVacante = await CrearVacanteCriticaAsync(ctx, lineaId: 4);
        await OcuparRotativoAsync(ctx, lineaId: 4, categoria: "operador_a", usuario); // compatible por matriz, pero no es Operador B

        var resultado = await CubrirAsync(puestoVacante, usuario);

        resultado.NivelAplicado.Should().Be("N4", "el único ocupante disponible no es Operador B, así que no hay candidato en ningún nivel");
        resultado.Codigo.Should().Be("VACANTE_CRITICA_PERSISTENTE");
    }

    [Fact]
    public async Task N2_bloqueado_por_el_piso_de_su_propia_linea_cae_a_N3_via_proximidad_A1()
    {
        // C15: "Piso de seguridad: B5 aplica a N2 y N3." L4 → primera en su
        // fila de proximidad (00 §A1): L2.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoVacante = await CrearVacanteCriticaAsync(ctx, lineaId: 4);

        await SetPisoAsync(4, minimo: 1);
        await OcuparRotativoAsync(ctx, lineaId: 4, categoria: "operador_b", usuario); // exactamente en el piso — inmune

        await SetPisoAsync(2, minimo: 0);
        var (operadorB2, _) = await OcuparRotativoAsync(ctx, lineaId: 2, categoria: "operador_b", usuario); // con margen

        var resultado = await CubrirAsync(puestoVacante, usuario);

        resultado.NivelAplicado.Should().Be("N3");
        resultado.CandidatoId.Should().Be(operadorB2);
        resultado.LineaOrigen.Should().Be((byte)2);
        resultado.Codigo.Should().Be("N3_REQUIERE_JUSTIFICACION_COORDINADOR");
        resultado.MovimientoId.Should().BeNull("00 §A6 exige justificación del Coordinador — no se ejecuta hasta E10.5");

        (await ctx.Movimientos.CountAsync()).Should().Be(0);
        (await ctx.SolicitudesRelevo.CountAsync()).Should().Be(0, "nada se ejecutó todavía — sin guarda anti-dominó que abrir");
    }

    [Fact]
    public async Task N4_Sin_ningun_operador_b_en_toda_la_planta_alerta_vacante_critica_persistente()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoVacante = await CrearVacanteCriticaAsync(ctx, lineaId: 4);

        var resultado = await CubrirAsync(puestoVacante, usuario);

        resultado.NivelAplicado.Should().Be("N4");
        resultado.CandidatoId.Should().BeNull();
        resultado.Codigo.Should().Be("VACANTE_CRITICA_PERSISTENTE");
        resultado.Mensaje.Should().Contain("Vacante crítica persistente");
    }

    [Fact]
    public async Task Un_puesto_que_no_esta_en_vacante_critica_se_rechaza()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        // Puesto fijo sin ninguna jornada arrancada — nunca es vacante crítica (fn_EsVacanteCritica).
        var puesto = new Puesto { LineaId = 4, Codigo = $"F{Guid.NewGuid():N}"[..15], NombrePuesto = "Fijo sin arrancar", Tipo = "fijo", CategoriaTitular = "operador_a" };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();

        var resultado = await CubrirAsync(puesto.Id, usuario);

        resultado.Codigo.Should().Be("PUESTO_NO_ES_VACANTE_CRITICA");
        resultado.NivelAplicado.Should().BeNull();
    }

    [Fact]
    public async Task Una_vacante_con_cobertura_ya_en_curso_no_dispara_una_segunda()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puesto = await CrearVacanteCriticaAsync(ctx, lineaId: 4);
        await CrearCandidatoEnBolsonAsync(ctx);
        await CubrirAsync(puesto, usuario); // primera llamada — abre la SolicitudRelevo de N1

        var resultado = await CubrirAsync(puesto, usuario);

        resultado.Codigo.Should().Be("YA_TIENE_SOLICITUD_ABIERTA");
    }
}

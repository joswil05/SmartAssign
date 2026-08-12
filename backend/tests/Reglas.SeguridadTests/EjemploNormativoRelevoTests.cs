using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E9.8 (docs/PROGRESO.md): el ejemplo normativo completo de §9.4 —
/// "capacidad limitada de la L8 y relevo en cadena" — contra la base
/// real, encadenando piezas ya construidas de E7 (fatiga), E8
/// (movimiento) y E9.1-E9.7 (motor de relevos), sin reimplementar
/// ninguna. 00 §A8 confirma que el ejemplo "es normativo tal cual está
/// escrito" y que los únicos motivos de falta de candidato son médica,
/// categoría o "no tener más gente disponible" — nunca sexo/género como
/// categoría de regla aparte.
///
/// Texto del ejemplo: "Supongamos que hay 5 puestos fatigados: 4 en L4
/// y 1 en L1. La L8 solo tiene personal disponible y compatible para
/// cubrir 3 [...]: envía 2 hacia L4 y 1 hacia L1. [...] Al llegar los 2
/// relevistas a L4, los 2 operarios relevados no van a la L8: como en
/// L4 siguen fatigados otros 2 puestos, el sistema los sugiere como
/// destino directo — cada relevado pasa a relevar a uno de esos 2
/// compañeros, resolviendo la fatiga de L4 sin gastar más personal de
/// la L8."
///
/// La asignación real del relevista al puesto que libera el relevado
/// ("solo entonces se le asigna el puesto", Parte X paso 3) no tiene SP
/// propio todavía — se simula con SQL crudo, mismo criterio que el
/// resto de la sesión para pasos sin UT propia todavía.
/// </summary>
public class EjemploNormativoRelevoTests : IAsyncLifetime
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

    /// <summary>Puesto rotativo fatigado, ocupado por un operario cualquiera desde hace @minutosOcupado.</summary>
    private static async Task<(int puestoId, int ocupanteId)> CrearPuestoFatigadoAsync(
        SmartAssignDbContext ctx, byte lineaId, int usuarioAsignador, int minutosOcupado = 65)
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
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada, PuestoId = puesto.Id, PersonalId = ocupante.Id,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddMinutes(-minutosOcupado), AsignadoPor = usuarioAsignador,
        });
        await ctx.SaveChangesAsync();
        return (puesto.Id, ocupante.Id);
    }

    private static async Task<int> CrearCandidatoEnBolsonAsync(SmartAssignDbContext ctx, string ficha)
    {
        var p = new Personal
        {
            Ficha = ficha, NombreCompleto = "Candidato de la L8", Categoria = "operario",
            Situacion = "en_bolson", LineaFisicaActual = 8,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task<long> InsertarSolicitudAsync(int puestoId, int jornadaLineaId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SolicitudRelevo (puesto_id, jornada_linea_id, origen, nivel, exceso_relativo)
            OUTPUT INSERTED.Id
            VALUES (@puesto_id, @jornada_linea_id, 'umbral_automatico', 'sugerido', 120);
            """;
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        var resultado = await cmd.ExecuteScalarAsync();
        return (long)resultado!;
    }

    /// <summary>Parte X paso 3, "solo entonces se le asigna el puesto" — sin SP propio todavía (fuera de E9).
    /// Cierra la Asignacion del ocupante anterior y abre la del relevista que acaba de llegar.</summary>
    private static async Task AsignarRelevistaYCerrarAlRelevadoAsync(
        SmartAssignDbContext ctx, int puestoId, int relevistaId, int jornadaLineaId, int usuarioId)
    {
        var anterior = await ctx.Asignaciones.SingleAsync(a => a.PuestoId == puestoId && a.Fin == null);
        anterior.Fin = DateTime.UtcNow;
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaLineaId, PuestoId = puestoId, PersonalId = relevistaId,
            Origen = "relevo", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        await ctx.SaveChangesAsync();
    }

    // ═══ Invocación de los SP ═══

    private record ResultadoAceptar(int? CandidatoId, long? MovimientoId, string? Codigo);

    private async Task<ResultadoAceptar> AceptarAsync(long solicitudId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_AceptarRelevo";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCandidato = new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pMovimiento = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCandidato);
        cmd.Parameters.Add(pMovimiento);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoAceptar(pCandidato.Value as int?, pMovimiento.Value as long?, pCodigo.Value as string);
    }

    private async Task RecibirAsync(long movimientoId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_RecibirPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@movimiento_id", movimientoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<(int? PuestoId, byte? Linea)> SugerirDestinoAsync(int personalId, byte lineaActual)
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
        cmd.Parameters.Add(pPuesto);
        cmd.Parameters.Add(pLinea);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        return (pPuesto.Value as int?, pLinea.Value as byte?);
    }

    [Fact]
    public async Task Capacidad_limitada_la_L8_solo_cubre_tantos_puestos_como_candidatos_tiene_disponibles()
    {
        // "5 puestos fatigados: 4 en L4 y 1 en L1. La L8 solo tiene [...]
        // para cubrir 3 [...]: envía 2 hacia L4 y 1 hacia L1."
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);

        var (puestoL4_1, _) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario);
        var (puestoL4_2, _) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario);
        var (puestoL4_3, _) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario); // sin candidato — capacidad agotada
        var (puestoL4_4, _) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario); // sin candidato — capacidad agotada
        var (puestoL1, _) = await CrearPuestoFatigadoAsync(ctx, lineaId: 1, usuario);

        await CrearCandidatoEnBolsonAsync(ctx, "F0001");
        await CrearCandidatoEnBolsonAsync(ctx, "F0002");
        await CrearCandidatoEnBolsonAsync(ctx, "F0003"); // exactamente 3 — "no tener más gente disponible" (00 §A8)

        var jornadaL4 = await JornadaAbiertaAsync(ctx, 4);
        var jornadaL1 = await JornadaAbiertaAsync(ctx, 1);
        var sL4_1 = await InsertarSolicitudAsync(puestoL4_1, jornadaL4);
        var sL4_2 = await InsertarSolicitudAsync(puestoL4_2, jornadaL4);
        var sL4_3 = await InsertarSolicitudAsync(puestoL4_3, jornadaL4);
        var sL4_4 = await InsertarSolicitudAsync(puestoL4_4, jornadaL4);
        var sL1 = await InsertarSolicitudAsync(puestoL1, jornadaL1);

        var r1 = await AceptarAsync(sL4_1, usuario);
        var r2 = await AceptarAsync(sL4_2, usuario);
        var rL1 = await AceptarAsync(sL1, usuario);
        var r3 = await AceptarAsync(sL4_3, usuario);
        var r4 = await AceptarAsync(sL4_4, usuario);

        // Las 3 primeras (2 de L4 + 1 de L1) tienen candidato — la L8 se agota justo después.
        r1.Codigo.Should().BeNull();
        r2.Codigo.Should().BeNull();
        rL1.Codigo.Should().BeNull();
        r3.Codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");
        r4.Codigo.Should().Be("SIN_CANDIDATOS_EN_BOLSON");

        var idsGanadores = new[] { r1.CandidatoId, r2.CandidatoId, rL1.CandidatoId };
        idsGanadores.Should().OnlyHaveUniqueItems("cada candidato solo puede cubrir un puesto a la vez");
    }

    [Fact]
    public async Task Relevo_en_cadena_los_relevados_pasan_a_relevar_directamente_sin_gastar_mas_personal_de_la_L8()
    {
        // "Al llegar los 2 relevistas a L4, los 2 operarios relevados no
        // van a la L8: como en L4 siguen fatigados otros 2 puestos, el
        // sistema los sugiere como destino directo [...] resolviendo la
        // fatiga de L4 sin gastar más personal de la L8."
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var jornadaL4 = await JornadaAbiertaAsync(ctx, 4);

        var (puestoA, relevadoA) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario); // cubierto por relevista1
        var (puestoB, relevadoB) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario); // cubierto por relevista2
        var (puestoC, _) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario);          // sigue fatigado — destino de relevadoA/B
        var (puestoD, _) = await CrearPuestoFatigadoAsync(ctx, lineaId: 4, usuario);          // sigue fatigado — destino de relevadoA/B

        var relevista1 = await CrearCandidatoEnBolsonAsync(ctx, "F0001");
        var relevista2 = await CrearCandidatoEnBolsonAsync(ctx, "F0002");

        var solicitudA = await InsertarSolicitudAsync(puestoA, jornadaL4);
        var solicitudB = await InsertarSolicitudAsync(puestoB, jornadaL4);

        var aceptadaA = await AceptarAsync(solicitudA, usuario);
        var aceptadaB = await AceptarAsync(solicitudB, usuario);
        aceptadaA.Codigo.Should().BeNull();
        aceptadaB.Codigo.Should().BeNull();
        // El ranking de B2 (E9.5) decide quién de los dos candidatos cubre cuál puesto —
        // esta prueba no depende de saber cuál es cuál, solo de que cada uno llegó a alguno.
        new[] { aceptadaA.CandidatoId, aceptadaB.CandidatoId }.Should().BeEquivalentTo([relevista1, relevista2]);

        // Paso 5: llegada real de ambos relevistas.
        await RecibirAsync(aceptadaA.MovimientoId!.Value, usuario);
        await RecibirAsync(aceptadaB.MovimientoId!.Value, usuario);

        // "Solo entonces se le asigna el puesto" (Parte X p3) — sin SP propio todavía, se simula.
        await AsignarRelevistaYCerrarAlRelevadoAsync(ctx, puestoA, aceptadaA.CandidatoId!.Value, jornadaL4, usuario);
        await AsignarRelevistaYCerrarAlRelevadoAsync(ctx, puestoB, aceptadaB.CandidatoId!.Value, jornadaL4, usuario);

        // Paso 6: a los dos relevados NO se les sugiere la L8 — C y D siguen fatigados en su propia línea.
        // sp_SugerirDestinoRelevado es sin efectos secundarios (no reserva nada por sí solo) — el
        // supervisor "ejecuta la sugerencia" (§9.4 p6) entre una y otra, exactamente como en la
        // planta real: por eso se simula la asignación de A ANTES de pedir destino para B, si no,
        // ambas consultas verían a C y D igual de libres y podrían coincidir en la misma.
        var destinoA = await SugerirDestinoAsync(relevadoA, lineaActual: 4);
        destinoA.Linea.Should().Be((byte)4, "sigue habiendo fatiga en su propia línea — nunca va a la L8 mientras eso sea cierto");
        destinoA.PuestoId.Should().BeOneOf(puestoC, puestoD);

        await AsignarRelevistaYCerrarAlRelevadoAsync(ctx, destinoA.PuestoId!.Value, relevadoA, jornadaL4, usuario);

        var destinoB = await SugerirDestinoAsync(relevadoB, lineaActual: 4);
        destinoB.Linea.Should().Be((byte)4);
        destinoB.PuestoId.Should().BeOneOf(puestoC, puestoD)
            .And.NotBe(destinoA.PuestoId, "el compañero que ya tomó A ya no está fatigado — B pasa a relevar al otro");
    }
}

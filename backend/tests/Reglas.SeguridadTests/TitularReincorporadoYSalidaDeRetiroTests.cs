using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E10.6 (docs/PROGRESO.md), cierra E10 (6/6): <c>sp_ReincorporarTitular</c>
/// (00 §C1) y <c>sp_FinalizarRetiroTemporal</c> (00 §C2). Mismo patrón de
/// base descartable que el resto de la suite.
/// </summary>
public class TitularReincorporadoYSalidaDeRetiroTests : IAsyncLifetime
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

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx, string rol = "coordinador")
    {
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = rol, OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> JornadaArrancadaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var existente = await ctx.JornadasLinea.Where(j => j.LineaId == lineaId && j.CerradoEn == null).Select(j => j.Id).SingleOrDefaultAsync();
        if (existente != 0) return existente;

        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100 };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1), SkuId = sku.Id, Estado = "arrancada", ArrancadoEn = DateTime.UtcNow };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, string categoria = "operador_a", string situacion = "fuera_de_turno", byte? lineaFisica = null)
    {
        var p = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba", Categoria = categoria, Situacion = situacion, LineaFisicaActual = lineaFisica };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Puesto fijo con titular técnico registrado (C12) — condición que resuelve sp_ReincorporarTitular.</summary>
    private static async Task<int> CrearPuestoFijoConTitularAsync(SmartAssignDbContext ctx, byte lineaId, int titularId, string categoriaTitular = "operador_a")
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"F{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto fijo de prueba", Tipo = "fijo", CategoriaTitular = categoriaTitular, TitularId = titularId };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task OcuparConAsync(SmartAssignDbContext ctx, int puestoId, int jornadaId, int personalId, int usuarioId, int? titularOriginalId = null)
    {
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaId, PuestoId = puestoId, PersonalId = personalId,
            TitularOriginalId = titularOriginalId, Origen = "barrido_automatico", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId,
        });
        var persona = await ctx.Personas.SingleAsync(p => p.Id == personalId);
        persona.Situacion = "asignado";
        await ctx.SaveChangesAsync();
    }

    private static async Task<short> CrearCapacidadAsync(SmartAssignDbContext ctx, string nombre)
    {
        var c = new CapacidadFisica { Codigo = $"C{Guid.NewGuid():N}"[..10], Nombre = nombre };
        ctx.CapacidadesFisicas.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    // ═══ Invocación de sp_ReincorporarTitular ═══

    private record ResultadoReincorporar(
        int? PuestoId, int? SuplenteLiberadoId, long? AsignacionId,
        byte? LineaSugeridaSuplente, int? PuestoSugeridoSuplente, string? Codigo, string? Mensaje);

    private async Task<ResultadoReincorporar> ReincorporarAsync(int titularId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ReincorporarTitular";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@titular_id", titularId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pPuesto = new SqlParameter("@puesto_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pSuplente = new SqlParameter("@suplente_liberado_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pAsignacion = new SqlParameter("@asignacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pLineaSug = new SqlParameter("@linea_sugerida_suplente", SqlDbType.TinyInt) { Direction = ParameterDirection.Output };
        var pPuestoSug = new SqlParameter("@puesto_sugerido_suplente", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pPuesto);
        cmd.Parameters.Add(pSuplente);
        cmd.Parameters.Add(pAsignacion);
        cmd.Parameters.Add(pLineaSug);
        cmd.Parameters.Add(pPuestoSug);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoReincorporar(
            pPuesto.Value as int?, pSuplente.Value as int?, pAsignacion.Value as long?,
            pLineaSug.Value as byte?, pPuestoSug.Value as int?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Titular_reincorporado_libera_al_suplente_y_le_sugiere_destino_sin_mandarlo_a_la_L8()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var titular = await CrearPersonaAsync(ctx, categoria: "operador_a");
        var puesto = await CrearPuestoFijoConTitularAsync(ctx, lineaId: 4, titularId: titular);
        var jornada = await JornadaArrancadaAsync(ctx, 4);
        var suplente = await CrearPersonaAsync(ctx, categoria: "operador_b");
        await OcuparConAsync(ctx, puesto, jornada, suplente, usuario, titularOriginalId: titular);

        var resultado = await ReincorporarAsync(titular, usuario);

        resultado.Codigo.Should().BeNull();
        resultado.PuestoId.Should().Be(puesto);
        resultado.SuplenteLiberadoId.Should().Be(suplente);
        resultado.AsignacionId.Should().NotBeNull();

        var nuevaAsignacion = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.Id == resultado.AsignacionId);
        nuevaAsignacion.PersonalId.Should().Be(titular);
        nuevaAsignacion.PuestoId.Should().Be(puesto);

        var cerrada = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.PersonalId == suplente);
        cerrada.Fin.Should().NotBeNull();
        cerrada.MotivoFin.Should().Be("titular_reincorporado");

        var titularDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == titular);
        titularDb.Situacion.Should().Be("asignado");

        var suplenteDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == suplente);
        suplenteDb.Situacion.Should().Be("presente_sin_asignar", "A7: con el titular presente el Operador B nunca va al Bolsón, solo queda disponible");

        // Sin ningún otro rotativo fatigado en toda la planta, la escalera de B4 cae a L8 sin puesto — pero nunca es un rechazo.
        resultado.LineaSugeridaSuplente.Should().Be((byte)8);
        resultado.PuestoSugeridoSuplente.Should().BeNull();
    }

    [Fact]
    public async Task Puesto_sin_ocupante_no_tiene_suplente_que_liberar()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var titular = await CrearPersonaAsync(ctx, categoria: "operador_a");
        await CrearPuestoFijoConTitularAsync(ctx, lineaId: 4, titularId: titular);

        var resultado = await ReincorporarAsync(titular, usuario);

        resultado.Codigo.Should().Be("PUESTO_SIN_OCUPANTE");
        resultado.AsignacionId.Should().BeNull();
    }

    [Fact]
    public async Task El_titular_ya_en_su_propio_puesto_no_tiene_nada_que_reincorporar()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var titular = await CrearPersonaAsync(ctx, categoria: "operador_a");
        var puesto = await CrearPuestoFijoConTitularAsync(ctx, lineaId: 4, titularId: titular);
        var jornada = await JornadaArrancadaAsync(ctx, 4);
        await OcuparConAsync(ctx, puesto, jornada, titular, usuario);

        var resultado = await ReincorporarAsync(titular, usuario);

        resultado.Codigo.Should().Be("TITULAR_YA_EN_SU_PUESTO");
    }

    [Fact]
    public async Task Una_persona_que_no_es_titular_de_ningun_fijo_se_rechaza()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx);

        var resultado = await ReincorporarAsync(persona, usuario);

        resultado.Codigo.Should().Be("NO_ES_TITULAR_DE_NINGUN_PUESTO_FIJO");
    }

    [Fact]
    public async Task Una_restriccion_medica_bloqueante_del_titular_impide_la_reincorporacion_y_nadie_se_mueve()
    {
        // §7.2, regla dura: nunca cede, ni siquiera para volver a su propio puesto.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var titular = await CrearPersonaAsync(ctx, categoria: "operador_a");
        var puesto = await CrearPuestoFijoConTitularAsync(ctx, lineaId: 4, titularId: titular);
        var jornada = await JornadaArrancadaAsync(ctx, 4);
        var suplente = await CrearPersonaAsync(ctx, categoria: "operador_b");
        await OcuparConAsync(ctx, puesto, jornada, suplente, usuario, titularOriginalId: titular);

        var capacidad = await CrearCapacidadAsync(ctx, "Levantar peso");
        ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puesto, CapacidadId = capacidad });
        ctx.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = titular, CapacidadId = capacidad, FechaInicio = new DateOnly(2026, 1, 1), FechaFin = null,
            FechaDictamen = new DateOnly(2026, 1, 1), Fuente = "Enfermería", RegistradoPor = usuario,
        });
        await ctx.SaveChangesAsync();

        var resultado = await ReincorporarAsync(titular, usuario);

        resultado.Codigo.Should().Be("RESTRICCION_MEDICA");
        var suplenteDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == suplente);
        suplenteDb.Situacion.Should().Be("asignado", "nada se movió — el suplente sigue exactamente donde estaba");
    }

    // ═══ Invocación de sp_FinalizarRetiroTemporal ═══

    private record ResultadoRetiro(string? Codigo, string? Mensaje);

    private async Task<ResultadoRetiro> FinalizarRetiroAsync(int personalId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_FinalizarRetiroTemporal";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoRetiro(pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Solo_el_coordinador_puede_finalizar_un_retiro_temporal()
    {
        await using var ctx = CrearContexto();
        var supervisor = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var persona = await CrearPersonaAsync(ctx, situacion: "retirado_temporal", lineaFisica: 4);

        var resultado = await FinalizarRetiroAsync(persona, supervisor);

        resultado.Codigo.Should().Be("SOLO_COORDINADOR");
    }

    [Fact]
    public async Task Una_persona_que_no_esta_retirada_no_tiene_nada_que_finalizar()
    {
        await using var ctx = CrearContexto();
        var coordinador = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, situacion: "presente_sin_asignar", lineaFisica: 4);

        var resultado = await FinalizarRetiroAsync(persona, coordinador);

        resultado.Codigo.Should().Be("NO_ESTA_RETIRADO_TEMPORALMENTE");
    }

    [Fact]
    public async Task El_coordinador_finaliza_el_retiro_sin_tocar_la_linea_fisica_y_queda_auditado()
    {
        await using var ctx = CrearContexto();
        var coordinador = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, situacion: "retirado_temporal", lineaFisica: 6);

        var resultado = await FinalizarRetiroAsync(persona, coordinador);

        resultado.Codigo.Should().BeNull();

        var personaDb = await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona);
        personaDb.Situacion.Should().Be("presente_sin_asignar");
        personaDb.LineaFisicaActual.Should().Be((byte)6, "C2, literal: no se toca la línea física");

        var auditoria = await ctx.Auditorias.AsNoTracking().SingleAsync(a => a.PersonalId == persona);
        auditoria.Accion.Should().Be("FINALIZAR_RETIRO_TEMPORAL");
        auditoria.Resultado.Should().Be("OK");
    }
}

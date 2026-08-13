using System.Data;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E14.2 (docs/PROGRESO.md): "<c>UltimaTareaJornada</c> + cierre
/// forzado con justificación" (00 §B6, 00 §A6). Extiende
/// <c>sp_CerrarTurno</c> (E14.1) con <c>@justificacion_motivo_id</c>/
/// <c>@justificacion_texto</c> — mismo patrón que E10.5. Sin bandera de
/// "forzar" propia: los dos parámetros presentes a la vez SON la señal
/// (mismo patrón que <c>sp_ExtraccionInversa</c>/<c>@forzando_piso</c>,
/// no el de <c>sp_AsignarPersona</c>/<c>@es_liderazgo_manual</c>, porque
/// C13 no describe una bandera de intención separada de la
/// justificación misma).
/// </summary>
public class CierreForzadoConJustificacionTests : IAsyncLifetime
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

    // ═══ Helpers de datos (mismos que E14.1) ═══

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx)
    {
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<(int jornadaId, DateOnly dia)> JornadaArrancadaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var dia = new DateOnly(2026, 1, 1);
        var jornada = new JornadaLinea
        {
            LineaId = lineaId, TurnoId = turno.Id, DiaOperacion = dia,
            Estado = "arrancada", ArrancadoEn = DateTime.UtcNow,
        };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return (jornada.Id, dia);
    }

    private static async Task<int> PersonaAsync(SmartAssignDbContext ctx, string situacion = "asignado", string nombreCompleto = "Ocupante de prueba")
    {
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = nombreCompleto, Categoria = "operario", Situacion = situacion };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        return persona.Id;
    }

    private static async Task<int> PuestoAsync(SmartAssignDbContext ctx, byte lineaId, string tipo = "rotativo", short? tipoActividadId = null)
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba", Tipo = tipo, TipoActividadId = tipoActividadId };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    /// <summary>Deliberadamente SIN fijar <c>Inicio</c> — ver la nota gemela en <c>CierreDeTurnoConListaExactaDeBloqueosTests</c> (E14.1).</summary>
    private static async Task<long> AsignarAsync(SmartAssignDbContext ctx, int jornadaId, int puestoId, int personalId, int usuarioId)
    {
        var asignacion = new Asignacion
        {
            JornadaLineaId = jornadaId, PuestoId = puestoId, PersonalId = personalId,
            Origen = "manual_supervisor", AsignadoPor = usuarioId,
        };
        ctx.Asignaciones.Add(asignacion);
        await ctx.SaveChangesAsync();
        return asignacion.Id;
    }

    private static async Task<int> LoteAbiertoAsync(SmartAssignDbContext ctx, int jornadaId, short numero = 3)
    {
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100, Activo = true };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();
        var lote = new Lote { JornadaLineaId = jornadaId, SkuId = sku.Id, Numero = numero };
        ctx.Lotes.Add(lote);
        await ctx.SaveChangesAsync();
        return lote.Id;
    }

    private async Task<SqlConnection> AbrirComoCoordinadorAsync()
    {
        var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
        return conexion;
    }

    /// <summary><c>JornadaLinea</c> lleva RLS — mismo patrón recurrente de la sesión.</summary>
    private async Task<(string estado, DateTime? cerradoEn, int? cerradoForzadoPor)> LeerJornadaAsync(int jornadaId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT estado, cerrado_en, cerrado_forzado_por FROM JornadaLinea WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", jornadaId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1), reader.IsDBNull(2) ? null : reader.GetInt32(2));
    }

    // ═══ Invocación de sp_CerrarTurno ═══

    private record ResultadoCierre(string? Bloqueos, string? Codigo, string? Mensaje);

    private async Task<ResultadoCierre> CerrarTurnoAsync(
        int jornadaLineaId, int usuarioId, short? justificacionMotivoId = null, string? justificacionTexto = null)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CerrarTurno";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@justificacion_motivo_id", (object?)justificacionMotivoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@justificacion_texto", (object?)justificacionTexto ?? DBNull.Value);
        var pBloqueos = new SqlParameter("@bloqueos", SqlDbType.NVarChar, -1) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pBloqueos);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoCierre(pBloqueos.Value as string, pCodigo.Value as string, pMensaje.Value as string);
    }

    // El motivo 3 ("Forzar cierre de turno") está sembrado desde E0.
    private const short MotivoForzarCierre = 3;

    [Fact]
    public async Task Con_bloqueos_y_sin_justificacion_el_cierre_sigue_rechazado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await LoteAbiertoAsync(ctx, jornada);

        var resultado = await CerrarTurnoAsync(jornada, usuario);

        resultado.Codigo.Should().Be("CIERRE_BLOQUEADO");
        (await LeerJornadaAsync(jornada)).estado.Should().Be("arrancada");
    }

    [Fact]
    public async Task Con_bloqueos_y_justificacion_completa_el_cierre_se_fuerza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await LoteAbiertoAsync(ctx, jornada, numero: 7);

        var resultado = await CerrarTurnoAsync(jornada, usuario, MotivoForzarCierre, "Lote 7 se termina de conciliar mañana, el turno debe cerrar ya.");

        resultado.Codigo.Should().BeNull("la justificación autoriza el cierre pese al bloqueo");
        var (estado, cerradoEn, cerradoForzadoPor) = await LeerJornadaAsync(jornada);
        estado.Should().Be("cerrada");
        cerradoEn.Should().NotBeNull();
        cerradoForzadoPor.Should().Be(usuario);
    }

    [Fact]
    public async Task El_cierre_forzado_devuelve_la_lista_exacta_de_bloqueos_que_paso_por_encima()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await LoteAbiertoAsync(ctx, jornada, numero: 9);

        var resultado = await CerrarTurnoAsync(jornada, usuario, MotivoForzarCierre, "Forzando el cierre para pruebas, el lote 9 se retoma en el próximo turno.");

        resultado.Bloqueos.Should().NotBeNull("aunque el cierre haya tenido éxito, la lista que se pasó por encima queda para auditoría");
        using var json = JsonDocument.Parse(resultado.Bloqueos!);
        var bloqueo = json.RootElement.EnumerateArray().Single();
        bloqueo.GetProperty("tipo").GetString().Should().Be("lote_abierto");
        bloqueo.GetProperty("numero").GetInt32().Should().Be(9);
    }

    [Fact]
    public async Task El_cierre_forzado_registra_la_justificacion_con_el_tipo_de_excepcion_correcto()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await LoteAbiertoAsync(ctx, jornada);

        await CerrarTurnoAsync(jornada, usuario, MotivoForzarCierre, "Texto de justificación real, con más de diez caracteres.");

        var justificacion = await ctx.JustificacionesExcepcion.AsNoTracking()
            .SingleAsync(j => j.UsuarioId == usuario && j.TipoExcepcion == "forzar_cierre_turno");
        justificacion.MotivoId.Should().Be(MotivoForzarCierre);
        justificacion.Texto.Should().Be("Texto de justificación real, con más de diez caracteres.");
    }

    [Fact]
    public async Task Justificacion_parcial_con_solo_motivo_no_fuerza_el_cierre()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await LoteAbiertoAsync(ctx, jornada);

        var resultado = await CerrarTurnoAsync(jornada, usuario, justificacionMotivoId: MotivoForzarCierre, justificacionTexto: null);

        resultado.Codigo.Should().Be("CIERRE_BLOQUEADO", "sin texto libre no hay formulario completo (A6)");
        (await LeerJornadaAsync(jornada)).estado.Should().Be("arrancada");
    }

    [Fact]
    public async Task Justificacion_parcial_con_solo_texto_no_fuerza_el_cierre()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await LoteAbiertoAsync(ctx, jornada);

        var resultado = await CerrarTurnoAsync(jornada, usuario, justificacionMotivoId: null, justificacionTexto: "Texto sin motivo de catálogo asociado.");

        resultado.Codigo.Should().Be("CIERRE_BLOQUEADO", "sin motivo de catálogo no hay formulario completo (A6)");
        (await LeerJornadaAsync(jornada)).estado.Should().Be("arrancada");
    }

    [Fact]
    public async Task Sin_bloqueos_una_justificacion_de_mas_no_se_registra_ni_marca_el_cierre_como_forzado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);

        var resultado = await CerrarTurnoAsync(jornada, usuario, MotivoForzarCierre, "Justificación adjunta de más, sin que hiciera falta forzar nada.");

        resultado.Codigo.Should().BeNull();
        var (estado, _, cerradoForzadoPor) = await LeerJornadaAsync(jornada);
        estado.Should().Be("cerrada");
        cerradoForzadoPor.Should().BeNull("no hubo nada que forzar de verdad — no es un cierre forzado");
        (await ctx.JustificacionesExcepcion.CountAsync(j => j.UsuarioId == usuario)).Should().Be(0, "adjuntar justificación sin necesidad no crea una excepción fantasma");
    }

    [Fact]
    public async Task El_cierre_forzado_tambien_persiste_el_ultimo_puesto_ocupado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, dia) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var actividad = new TipoActividad { Nombre = $"Actividad {Guid.NewGuid():N}"[..20] };
        ctx.Add(actividad);
        await ctx.SaveChangesAsync();
        var puesto = await PuestoAsync(ctx, lineaId: 4, tipoActividadId: actividad.Id);
        var persona = await PersonaAsync(ctx);
        await AsignarAsync(ctx, jornada, puesto, persona, usuario);
        await LoteAbiertoAsync(ctx, jornada);

        var resultado = await CerrarTurnoAsync(jornada, usuario, MotivoForzarCierre, "Forzando con una persona asignada, para confirmar que igual se persiste.");

        resultado.Codigo.Should().BeNull();
        var utj = await ctx.Set<UltimaTareaJornada>().AsNoTracking().SingleAsync(u => u.PersonalId == persona);
        utj.TipoActividadId.Should().Be(actividad.Id);
        utj.PuestoId.Should().Be(puesto);
        utj.DiaOperacion.Should().Be(dia);
    }

    [Fact]
    public async Task El_cierre_forzado_tambien_libera_al_personal_asignado()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var puesto = await PuestoAsync(ctx, lineaId: 4, tipo: "fijo");
        var persona = await PersonaAsync(ctx);
        var asignacionId = await AsignarAsync(ctx, jornada, puesto, persona, usuario);
        await LoteAbiertoAsync(ctx, jornada);

        await CerrarTurnoAsync(jornada, usuario, MotivoForzarCierre, "Forzando el cierre con el puesto fijo todavía asignado.");

        var asignacion = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.Id == asignacionId);
        asignacion.Fin.Should().NotBeNull();
        asignacion.MotivoFin.Should().Be("cierre_turno");
        (await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona)).Situacion.Should().Be("fuera_de_turno");
    }

    [Fact]
    public async Task Una_jornada_ya_cerrada_no_se_puede_forzar_a_cerrar_de_nuevo()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await CerrarTurnoAsync(jornada, usuario);

        var segundo = await CerrarTurnoAsync(jornada, usuario, MotivoForzarCierre, "Intentando forzar un segundo cierre, que no debería proceder.");

        segundo.Codigo.Should().Be("JORNADA_YA_CERRADA", "la justificación no salta el chequeo de estado, solo la lista de bloqueos");
    }
}

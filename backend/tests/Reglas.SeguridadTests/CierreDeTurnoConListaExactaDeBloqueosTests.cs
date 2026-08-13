using System.Data;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E14.1 (docs/PROGRESO.md): "Cierre de turno con lista exacta de
/// bloqueos" (00 §C13, 02 §4.10) — arranca E14. <c>sp_CerrarTurno</c> lee
/// y escribe <c>JornadaLinea</c>, que lleva RLS — todas las llamadas al
/// SP necesitan `SESSION_CONTEXT('rol')`, mismo patrón recurrente de la
/// sesión.
/// </summary>
public class CierreDeTurnoConListaExactaDeBloqueosTests : IAsyncLifetime
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

    /// <summary>
    /// Deliberadamente SIN fijar <c>Inicio</c> en C# — se deja el default
    /// de la columna (<c>SYSUTCDATETIME()</c>, `AsignacionConfig`) para
    /// que tanto `inicio` como el `fin`/`@ahora` que escribe
    /// `sp_CerrarTurno` salgan del MISMO reloj (el de SQL Server). Con
    /// `DateTime.UtcNow` (reloj del proceso de .NET) se observó
    /// `CK_Asig_fin` fallar de forma real e intermitente — dos relojes de
    /// procesos distintos, aunque en la misma máquina, no están
    /// garantizados a coincidir al milisegundo.
    /// </summary>
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

    private static async Task<long> MovimientoEnTransitoAsync(
        SmartAssignDbContext ctx, byte lineaOrigen, byte lineaDestino, int personalId, int usuarioId)
    {
        var mov = new Movimiento
        {
            PersonalId = personalId, LineaOrigen = lineaOrigen, LineaDestino = lineaDestino,
            Motivo = "relevo", Estado = "en_transito", HoraSalida = DateTime.UtcNow, DespachadoPor = usuarioId,
        };
        ctx.Movimientos.Add(mov);
        await ctx.SaveChangesAsync();
        return mov.Id;
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

    /// <summary>
    /// <c>JornadaLinea</c> lleva RLS — un <c>ctx</c> sin `SESSION_CONTEXT`
    /// (como el que ya tienen las pruebas, abierto antes de llamar al SP)
    /// no ve ninguna fila al releer, aunque el `sp_CerrarTurno` sí haya
    /// escrito de verdad. Mismo patrón recurrente de toda la sesión.
    /// </summary>
    private async Task<(string estado, DateTime? cerradoEn)> LeerJornadaAsync(int jornadaId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT estado, cerrado_en FROM JornadaLinea WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", jornadaId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1));
    }

    // ═══ Invocación de sp_CerrarTurno ═══

    private record ResultadoCierre(string? Bloqueos, string? Codigo, string? Mensaje);

    private async Task<ResultadoCierre> CerrarTurnoAsync(int jornadaLineaId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CerrarTurno";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pBloqueos = new SqlParameter("@bloqueos", SqlDbType.NVarChar, -1) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pBloqueos);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoCierre(pBloqueos.Value as string, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Sin_bloqueos_el_cierre_ejecuta_y_marca_la_jornada_cerrada()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);

        var resultado = await CerrarTurnoAsync(jornada, usuario);

        resultado.Bloqueos.Should().BeNull();
        resultado.Codigo.Should().BeNull();

        var (estado, cerradoEn) = await LeerJornadaAsync(jornada);
        estado.Should().Be("cerrada");
        cerradoEn.Should().NotBeNull();
    }

    [Fact]
    public async Task Un_lote_abierto_bloquea_el_cierre_y_lo_lista_con_su_numero_exacto()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await LoteAbiertoAsync(ctx, jornada, numero: 3);

        var resultado = await CerrarTurnoAsync(jornada, usuario);

        resultado.Codigo.Should().Be("CIERRE_BLOQUEADO");
        resultado.Bloqueos.Should().NotBeNull();
        using var json = JsonDocument.Parse(resultado.Bloqueos!);
        var bloqueo = json.RootElement.EnumerateArray().Single();
        bloqueo.GetProperty("tipo").GetString().Should().Be("lote_abierto");
        bloqueo.GetProperty("numero").GetInt32().Should().Be(3);

        (await LeerJornadaAsync(jornada)).estado.Should().Be("arrancada");
    }

    [Fact]
    public async Task Gente_en_transito_hacia_la_linea_bloquea_el_cierre_con_nombre_y_linea_de_origen()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var persona = await PersonaAsync(ctx, situacion: "en_transito", nombreCompleto: "María López");
        await MovimientoEnTransitoAsync(ctx, lineaOrigen: 8, lineaDestino: 4, persona, usuario);

        var resultado = await CerrarTurnoAsync(jornada, usuario);

        resultado.Codigo.Should().Be("CIERRE_BLOQUEADO");
        using var json = JsonDocument.Parse(resultado.Bloqueos!);
        var bloqueo = json.RootElement.EnumerateArray().Single();
        bloqueo.GetProperty("tipo").GetString().Should().Be("transito_entrante");
        bloqueo.GetProperty("nombreCompleto").GetString().Should().Be("María López");
        bloqueo.GetProperty("lineaOrigenCodigo").GetString().Should().Be("L8");
    }

    [Fact]
    public async Task Gente_propia_en_transito_hacia_fuera_sin_recibir_bloquea_el_cierre_con_linea_de_destino()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var persona = await PersonaAsync(ctx, situacion: "en_transito", nombreCompleto: "Juan Pérez");
        await MovimientoEnTransitoAsync(ctx, lineaOrigen: 4, lineaDestino: 2, persona, usuario);

        var resultado = await CerrarTurnoAsync(jornada, usuario);

        resultado.Codigo.Should().Be("CIERRE_BLOQUEADO");
        using var json = JsonDocument.Parse(resultado.Bloqueos!);
        var bloqueo = json.RootElement.EnumerateArray().Single();
        bloqueo.GetProperty("tipo").GetString().Should().Be("transito_saliente_sin_recibir");
        bloqueo.GetProperty("nombreCompleto").GetString().Should().Be("Juan Pérez");
        bloqueo.GetProperty("lineaDestinoCodigo").GetString().Should().Be("L2");
    }

    [Fact]
    public async Task Un_transito_ya_recibido_no_bloquea_el_cierre()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var persona = await PersonaAsync(ctx, situacion: "presente_sin_asignar");
        var mov = await MovimientoEnTransitoAsync(ctx, lineaOrigen: 8, lineaDestino: 4, persona, usuario);
        (await ctx.Movimientos.SingleAsync(m => m.Id == mov)).Estado = "recibido";
        await ctx.SaveChangesAsync();

        var resultado = await CerrarTurnoAsync(jornada, usuario);

        resultado.Codigo.Should().BeNull();
    }

    [Fact]
    public async Task El_cierre_pasa_el_personal_asignado_a_fuera_de_turno_y_libera_el_puesto()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var puesto = await PuestoAsync(ctx, lineaId: 4, tipo: "fijo");
        var persona = await PersonaAsync(ctx);
        var asignacionId = await AsignarAsync(ctx, jornada, puesto, persona, usuario);

        await CerrarTurnoAsync(jornada, usuario);

        var asignacion = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.Id == asignacionId);
        asignacion.Fin.Should().NotBeNull();
        asignacion.MotivoFin.Should().Be("cierre_turno");
        (await ctx.Personas.AsNoTracking().SingleAsync(p => p.Id == persona)).Situacion.Should().Be("fuera_de_turno");
    }

    [Fact]
    public async Task El_cierre_persiste_el_ultimo_puesto_ocupado_para_la_regla_de_24_horas()
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

        await CerrarTurnoAsync(jornada, usuario);

        var utj = await ctx.Set<UltimaTareaJornada>().AsNoTracking().SingleAsync(u => u.PersonalId == persona);
        utj.TipoActividadId.Should().Be(actividad.Id);
        utj.PuestoId.Should().Be(puesto);
        utj.DiaOperacion.Should().Be(dia);
    }

    [Fact]
    public async Task Un_cierre_posterior_actualiza_la_fila_existente_en_vez_de_duplicarla()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var actividad = new TipoActividad { Nombre = $"Actividad {Guid.NewGuid():N}"[..20] };
        ctx.Add(actividad);
        await ctx.SaveChangesAsync();
        var persona = await PersonaAsync(ctx);

        var (jornada1, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var puesto1 = await PuestoAsync(ctx, lineaId: 4, tipoActividadId: actividad.Id);
        await AsignarAsync(ctx, jornada1, puesto1, persona, usuario);
        await CerrarTurnoAsync(jornada1, usuario);

        // La misma persona, otro turno, otro puesto de la misma actividad.
        // Por SQL crudo, no por el `ctx` original: el cierre anterior ya
        // cambió `Personal.situacion` (y su `row_version`) por su cuenta
        // — el `ctx` sigue con la instancia vieja en el change tracker, y
        // guardarla chocaría con la concurrencia optimista real, no
        // simulada (mismo patrón "conexión cruda distinta" de otras
        // pruebas de la sesión).
        await using (var conexionCruda = new SqlConnection(CadenaConexion))
        {
            await conexionCruda.OpenAsync();
            await using var cmd = conexionCruda.CreateCommand();
            cmd.CommandText = "UPDATE Personal SET situacion = 'presente_sin_asignar' WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", persona);
            await cmd.ExecuteNonQueryAsync();
        }
        var (jornada2, dia2) = await JornadaArrancadaAsync(ctx, lineaId: 6);
        var puesto2 = await PuestoAsync(ctx, lineaId: 6, tipoActividadId: actividad.Id);
        await AsignarAsync(ctx, jornada2, puesto2, persona, usuario);
        await CerrarTurnoAsync(jornada2, usuario);

        var filas = await ctx.Set<UltimaTareaJornada>().AsNoTracking().Where(u => u.PersonalId == persona).ToListAsync();
        filas.Should().HaveCount(1, "es 'el último puesto', no un historial");
        filas[0].PuestoId.Should().Be(puesto2);
        filas[0].DiaOperacion.Should().Be(dia2);
    }

    [Fact]
    public async Task Un_puesto_sin_tipo_de_actividad_no_escribe_en_UltimaTareaJornada()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var puesto = await PuestoAsync(ctx, lineaId: 4, tipoActividadId: null);
        var persona = await PersonaAsync(ctx);
        await AsignarAsync(ctx, jornada, puesto, persona, usuario);

        await CerrarTurnoAsync(jornada, usuario);

        (await ctx.Set<UltimaTareaJornada>().CountAsync(u => u.PersonalId == persona)).Should().Be(0);
    }

    [Fact]
    public async Task El_cierre_cancela_las_solicitudes_de_relevo_pendientes_de_la_linea()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var puesto = await PuestoAsync(ctx, lineaId: 4);
        var solicitud = new SolicitudRelevo { PuestoId = puesto, JornadaLineaId = jornada, Origen = "manual_supervisor", Nivel = "sugerido", CreadaEn = DateTime.UtcNow };
        ctx.Add(solicitud);
        await ctx.SaveChangesAsync();

        await CerrarTurnoAsync(jornada, usuario);

        var recargada = await ctx.Set<SolicitudRelevo>().AsNoTracking().SingleAsync(s => s.Id == solicitud.Id);
        recargada.Resultado.Should().Be("cierre_turno");
        recargada.ResueltaEn.Should().NotBeNull();
    }

    [Fact]
    public async Task El_cierre_caduca_los_descartados_de_los_puestos_de_la_linea()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, dia) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        var puesto = await PuestoAsync(ctx, lineaId: 4);
        var persona = await PersonaAsync(ctx, situacion: "en_bolson");
        var descarte = new RelevoDescartado { PuestoId = puesto, PersonalId = persona, JornadaDia = dia, DescartadoPor = usuario, DescartadoEn = DateTime.UtcNow };
        ctx.Add(descarte);
        await ctx.SaveChangesAsync();

        await CerrarTurnoAsync(jornada, usuario);

        var recargado = await ctx.Set<RelevoDescartado>().AsNoTracking().SingleAsync(d => d.Id == descarte.Id);
        recargado.LimpiadoEn.Should().NotBeNull();
        recargado.LimpiadoPor.Should().Be(usuario);
    }

    [Fact]
    public async Task Una_jornada_ya_cerrada_no_se_puede_volver_a_cerrar()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var (jornada, _) = await JornadaArrancadaAsync(ctx, lineaId: 4);
        await CerrarTurnoAsync(jornada, usuario);

        var segundo = await CerrarTurnoAsync(jornada, usuario);

        segundo.Codigo.Should().Be("JORNADA_YA_CERRADA");
    }

    [Fact]
    public async Task Una_jornada_que_nunca_arranco_no_se_puede_cerrar()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        var jornada = new JornadaLinea { LineaId = 4, TurnoId = turno.Id, DiaOperacion = new DateOnly(2026, 1, 1) }; // planificada
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();

        var resultado = await CerrarTurnoAsync(jornada.Id, usuario);

        resultado.Codigo.Should().Be("JORNADA_NO_ARRANCADA");
    }

    [Fact]
    public async Task Una_jornada_inexistente_se_rechaza_explicitamente()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var resultado = await CerrarTurnoAsync(999_999, usuario);

        resultado.Codigo.Should().Be("JORNADA_INEXISTENTE");
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E10.5 (docs/PROGRESO.md): <c>JustificacionExcepcion</c> en toda
/// excepción — 00 §A6, 04 §5.4. Mismo patrón de base descartable que el
/// resto de la suite. Motivos de catálogo (sembrados desde E0):
/// 1=movimiento_fuera_de_flujo, 2=extraccion_operador_b,
/// 4=forzar_bajo_piso_seguridad, 5=saltar_ventana_arranque,
/// 7=asignacion_liderazgo.
/// </summary>
public class JustificacionEnExcepcionesTests : IAsyncLifetime
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
        var u = new Usuario { Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> JornadaArrancadaAsync(SmartAssignDbContext ctx, byte lineaId, DateTime? ventanaArranqueFin = null)
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
            SkuId = sku.Id, Estado = "arrancada", ArrancadoEn = DateTime.UtcNow, VentanaArranqueFin = ventanaArranqueFin,
        };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        return jornada.Id;
    }

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, string categoria = "operario", string? situacion = "presente_sin_asignar", byte? lineaFisica = null)
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = categoria, Situacion = situacion ?? "presente_sin_asignar", LineaFisicaActual = lineaFisica,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoAsync(SmartAssignDbContext ctx, byte lineaId, string tipo = "rotativo", string? categoriaTitular = null)
    {
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba", Tipo = tipo, CategoriaTitular = categoriaTitular };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
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

    // ═══ Invocación de sp_AsignarPersona ═══

    private record ResultadoAsignar(string? Codigo, string? Mensaje, long? AsignacionId);

    private async Task<ResultadoAsignar> AsignarAsync(
        int personalId, int puestoId, int usuarioId, int jornadaLineaId, string origen = "manual_supervisor",
        bool esLiderazgoManual = false, bool permitirSaltarVentana = false,
        short? justificacionMotivoId = null, string? justificacionTexto = null)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_AsignarPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@origen", origen);
        cmd.Parameters.AddWithValue("@idempotency_key", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@es_liderazgo_manual", esLiderazgoManual);
        cmd.Parameters.AddWithValue("@permitir_saltar_ventana", permitirSaltarVentana);
        cmd.Parameters.AddWithValue("@justificacion_motivo_id", (object?)justificacionMotivoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@justificacion_texto", (object?)justificacionTexto ?? DBNull.Value);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        var pAsignacion = new SqlParameter("@asignacion_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        cmd.Parameters.Add(pAsignacion);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoAsignar(pCodigo.Value as string, pMensaje.Value as string, pAsignacion.Value as long?);
    }

    // ═══ Invocación de sp_DespacharPersona ═══

    private record ResultadoDespacho(long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoDespacho> DespacharAsync(
        int personalId, byte lineaDestino, string motivo, int usuarioId,
        short? justificacionMotivoId = null, string? justificacionTexto = null)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_DespacharPersona";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@linea_destino", lineaDestino);
        cmd.Parameters.AddWithValue("@motivo", motivo);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@justificacion_motivo_id", (object?)justificacionMotivoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@justificacion_texto", (object?)justificacionTexto ?? DBNull.Value);
        var pId = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoDespacho(pId.Value as long?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Asignacion_de_liderazgo_manual_sin_justificacion_se_rechaza()
    {
        // A7b + A6, regla dura: "Requiere justificación registrada".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaArrancadaAsync(ctx, 4);
        var puesto = await CrearPuestoAsync(ctx, 4, tipo: "fijo", categoriaTitular: "operador_c");
        var lider = await CrearPersonaAsync(ctx, categoria: "liderazgo");

        var resultado = await AsignarAsync(lider, puesto, usuario, jornada, esLiderazgoManual: true);

        resultado.Codigo.Should().Be("JUSTIFICACION_REQUERIDA");
        resultado.AsignacionId.Should().BeNull();
    }

    [Fact]
    public async Task Asignacion_de_liderazgo_manual_con_justificacion_salta_la_categoria_y_queda_enlazada()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaArrancadaAsync(ctx, 4);
        var puesto = await CrearPuestoAsync(ctx, 4, tipo: "fijo", categoriaTitular: "operador_c");
        var lider = await CrearPersonaAsync(ctx, categoria: "liderazgo");

        var resultado = await AsignarAsync(lider, puesto, usuario, jornada, esLiderazgoManual: true,
            justificacionMotivoId: 7, justificacionTexto: "Cobertura de emergencia autorizada por gerencia de planta.");

        resultado.Codigo.Should().BeNull();
        resultado.AsignacionId.Should().NotBeNull();

        var asignacion = await ctx.Asignaciones.AsNoTracking().SingleAsync(a => a.Id == resultado.AsignacionId);
        asignacion.JustificacionId.Should().NotBeNull();

        var justificacion = await ctx.JustificacionesExcepcion.AsNoTracking().SingleAsync(j => j.Id == asignacion.JustificacionId);
        justificacion.TipoExcepcion.Should().Be("asignacion_liderazgo");
        justificacion.MotivoId.Should().Be((short)7);
    }

    [Fact]
    public async Task Saltar_la_ventana_de_arranque_sin_justificacion_se_rechaza_aunque_la_ventana_bloquee()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaArrancadaAsync(ctx, 4, ventanaArranqueFin: DateTime.UtcNow.AddMinutes(30));
        var puesto = await CrearPuestoAsync(ctx, 4, tipo: "rotativo");
        // Físicamente en otra línea — es justo lo que la ventana bloquea (§8.4).
        var persona = await CrearPersonaAsync(ctx, lineaFisica: 9);

        var resultado = await AsignarAsync(persona, puesto, usuario, jornada, permitirSaltarVentana: true);

        resultado.Codigo.Should().Be("JUSTIFICACION_REQUERIDA");
    }

    [Fact]
    public async Task Saltar_la_ventana_de_arranque_con_justificacion_asigna_pese_al_bloqueo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaArrancadaAsync(ctx, 4, ventanaArranqueFin: DateTime.UtcNow.AddMinutes(30));
        var puesto = await CrearPuestoAsync(ctx, 4, tipo: "rotativo");
        var persona = await CrearPersonaAsync(ctx, lineaFisica: 9);

        var resultado = await AsignarAsync(persona, puesto, usuario, jornada, permitirSaltarVentana: true,
            justificacionMotivoId: 5, justificacionTexto: "Coordinador autoriza traslado inmediato por vacante crítica.");

        resultado.Codigo.Should().BeNull();
        resultado.AsignacionId.Should().NotBeNull();

        var justificacion = await ctx.JustificacionesExcepcion.AsNoTracking().SingleAsync();
        justificacion.TipoExcepcion.Should().Be("saltar_ventana_arranque");
    }

    [Fact]
    public async Task Sin_ninguna_bandera_de_excepcion_no_exige_justificacion()
    {
        // El camino ordinario (manual_supervisor, sin banderas) nunca se ve afectado por esta UT.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var jornada = await JornadaArrancadaAsync(ctx, 4);
        var puesto = await CrearPuestoAsync(ctx, 4, tipo: "rotativo");
        var persona = await CrearPersonaAsync(ctx);

        var resultado = await AsignarAsync(persona, puesto, usuario, jornada);

        resultado.Codigo.Should().BeNull();
        resultado.AsignacionId.Should().NotBeNull();
        (await ctx.JustificacionesExcepcion.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Despachar_por_intervencion_del_coordinador_sin_justificacion_se_rechaza()
    {
        // §2.1.9: "retirar a alguien fuera del flujo normal" — A6 lo exige siempre.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisica: 4);

        var resultado = await DespacharAsync(persona, lineaDestino: 8, motivo: "intervencion_coordinador", usuario);

        resultado.Codigo.Should().Be("JUSTIFICACION_REQUERIDA");
        resultado.MovimientoId.Should().BeNull();
    }

    [Fact]
    public async Task Despachar_por_intervencion_del_coordinador_con_justificacion_queda_enlazado()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, lineaFisica: 4);

        var resultado = await DespacharAsync(persona, lineaDestino: 8, motivo: "intervencion_coordinador", usuario,
            justificacionMotivoId: 1, justificacionTexto: "Acuerdo directo con el trabajador por permiso médico familiar.");

        resultado.Codigo.Should().BeNull();
        resultado.MovimientoId.Should().NotBeNull();

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.JustificacionId.Should().NotBeNull();

        var justificacion = await ctx.JustificacionesExcepcion.AsNoTracking().SingleAsync(j => j.Id == movimiento.JustificacionId);
        justificacion.TipoExcepcion.Should().Be("movimiento_fuera_de_flujo");
    }

    // ═══ sp_ExtraccionInversa — forzar_bajo_piso_seguridad (B5) ═══

    private static async Task<List<int>> OcuparRotativosAsync(SmartAssignDbContext ctx, byte lineaId, int cuantos, int usuarioId)
    {
        var jornada = await JornadaArrancadaAsync(ctx, lineaId);
        var ocupantes = new List<int>();
        for (var i = 0; i < cuantos; i++)
        {
            var puesto = await CrearPuestoAsync(ctx, lineaId);
            var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operario" };
            ctx.Personas.Add(persona);
            await ctx.SaveChangesAsync();
            ctx.Asignaciones.Add(new Asignacion { JornadaLineaId = jornada, PuestoId = puesto, PersonalId = persona.Id, Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId });
            await ctx.SaveChangesAsync();
            ocupantes.Add(persona.Id);
        }
        return ocupantes;
    }

    private async Task ActivarLineasAsync(params byte[] lineas)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "UPDATE Linea SET activa_hoy = 1 WHERE Id IN (" + string.Join(",", lineas) + ");";
        await cmd.ExecuteNonQueryAsync();
    }

    private record ResultadoExtraccion(int? CandidatoId, byte? LineaOrigen, long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoExtraccion> ExtraerAsync(int puestoSolicitante, int usuarioId, short? justificacionMotivoId = null, string? justificacionTexto = null)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ExtraccionInversa";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@puesto_id_solicitante", puestoSolicitante);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@justificacion_motivo_id", (object?)justificacionMotivoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@justificacion_texto", (object?)justificacionTexto ?? DBNull.Value);
        var pCandidato = new SqlParameter("@candidato_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pLinea = new SqlParameter("@linea_origen", SqlDbType.TinyInt) { Direction = ParameterDirection.Output };
        var pMovimiento = new SqlParameter("@movimiento_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCandidato);
        cmd.Parameters.Add(pLinea);
        cmd.Parameters.Add(pMovimiento);
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);
        await cmd.ExecuteNonQueryAsync();
        return new ResultadoExtraccion(pCandidato.Value as int?, pLinea.Value as byte?, pMovimiento.Value as long?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task Extraccion_inversa_sin_justificacion_sigue_respetando_el_piso_igual_que_antes()
    {
        // No regresión de E10.3: sin justificación, el comportamiento es idéntico.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await ActivarLineasAsync(4, 10, 9, 3, 5, 7, 6, 2, 1);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);
        await SetPisoAsync(10, minimo: 1);
        await OcuparRotativosAsync(ctx, lineaId: 10, cuantos: 1, usuario); // exactamente en el piso

        var resultado = await ExtraerAsync(puestoSolicitante, usuario);

        resultado.Codigo.Should().Be("CAPACIDAD_CRITICA_DE_PLANTA_AGOTADA", "L10 es la única activada con dotación y está en su piso — sin justificación, sigue inmune");
    }

    [Fact]
    public async Task Extraccion_inversa_con_justificacion_fuerza_por_debajo_del_piso_y_queda_enlazada()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        await ActivarLineasAsync(4, 10, 9, 3, 5, 7, 6, 2, 1);
        var puestoSolicitante = await CrearPuestoAsync(ctx, lineaId: 4);
        await SetPisoAsync(10, minimo: 1);
        var ocupantesL10 = await OcuparRotativosAsync(ctx, lineaId: 10, cuantos: 1, usuario); // exactamente en el piso

        var resultado = await ExtraerAsync(puestoSolicitante, usuario,
            justificacionMotivoId: 4, justificacionTexto: "Coordinador fuerza extracción por parada crítica de línea 4.");

        resultado.Codigo.Should().BeNull();
        resultado.LineaOrigen.Should().Be((byte)10);
        resultado.CandidatoId.Should().BeOneOf(ocupantesL10);
        resultado.MovimientoId.Should().NotBeNull();

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.JustificacionId.Should().NotBeNull();

        var justificacion = await ctx.JustificacionesExcepcion.AsNoTracking().SingleAsync(j => j.Id == movimiento.JustificacionId);
        justificacion.TipoExcepcion.Should().Be("forzar_bajo_piso_seguridad");
        justificacion.MotivoId.Should().Be((short)4);
    }

    // ═══ sp_CubrirVacanteCritica — N3 pasa a ejecutarse con justificación (C15, A6) ═══

    private static async Task<int> CrearVacanteCriticaAsync(SmartAssignDbContext ctx, byte lineaId)
    {
        await JornadaArrancadaAsync(ctx, lineaId);
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"F{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto fijo de prueba", Tipo = "fijo", CategoriaTitular = "operador_a" };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task<(int personalId, int puestoId)> OcuparRotativoOperadorBAsync(SmartAssignDbContext ctx, byte lineaId, int usuarioId)
    {
        var jornada = await JornadaArrancadaAsync(ctx, lineaId);
        var puesto = new Puesto { LineaId = lineaId, Codigo = $"R{Guid.NewGuid():N}"[..15], NombrePuesto = "Rotativo de prueba", Tipo = "rotativo" };
        ctx.Puestos.Add(puesto);
        var persona = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Ocupante de prueba", Categoria = "operador_b" };
        ctx.Personas.Add(persona);
        await ctx.SaveChangesAsync();
        ctx.Asignaciones.Add(new Asignacion { JornadaLineaId = jornada, PuestoId = puesto.Id, PersonalId = persona.Id, Origen = "manual_supervisor", Inicio = DateTime.UtcNow, AsignadoPor = usuarioId });
        await ctx.SaveChangesAsync();
        return (persona.Id, puesto.Id);
    }

    private record ResultadoVacante(
        string? NivelAplicado, int? CandidatoId, byte? LineaOrigen, long? SolicitudId,
        long? MovimientoId, string? Codigo, string? Mensaje);

    private async Task<ResultadoVacante> CubrirAsync(int puestoVacante, int usuarioId, short? justificacionMotivoId = null, string? justificacionTexto = null)
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
        cmd.Parameters.AddWithValue("@justificacion_motivo_id", (object?)justificacionMotivoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@justificacion_texto", (object?)justificacionTexto ?? DBNull.Value);
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
        return new ResultadoVacante(
            pNivel.Value as string, pCandidato.Value as int?, pLinea.Value as byte?, pSolicitud.Value as long?,
            pMovimiento.Value as long?, pCodigo.Value as string, pMensaje.Value as string);
    }

    [Fact]
    public async Task N3_con_justificacion_del_coordinador_ejecuta_la_extraccion_y_abre_la_guarda_anti_domino()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoVacante = await CrearVacanteCriticaAsync(ctx, lineaId: 4);
        // L4 sin Operador B propio → N2 no aplica; L2 es la primera en la fila de proximidad de L4 (00 §A1).
        var (operadorB2, puestoDonante) = await OcuparRotativoOperadorBAsync(ctx, lineaId: 2, usuario);

        var resultado = await CubrirAsync(puestoVacante, usuario,
            justificacionMotivoId: 2, justificacionTexto: "Coordinador autoriza extracción de Operador B de L2 por vacante crítica en L4.");

        resultado.NivelAplicado.Should().Be("N3");
        resultado.Codigo.Should().BeNull("con justificación N3 se ejecuta, no solo se detecta");
        resultado.CandidatoId.Should().Be(operadorB2);
        resultado.LineaOrigen.Should().Be((byte)2);
        resultado.MovimientoId.Should().NotBeNull();

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        movimiento.Motivo.Should().Be("cobertura_vacante_critica");
        movimiento.JustificacionId.Should().NotBeNull();

        var justificacion = await ctx.JustificacionesExcepcion.AsNoTracking().SingleAsync(j => j.Id == movimiento.JustificacionId);
        justificacion.TipoExcepcion.Should().Be("extraccion_operador_b");

        var domino = await ctx.SolicitudesRelevo.AsNoTracking().SingleAsync(s => s.Id == resultado.SolicitudId);
        domino.PuestoId.Should().Be(puestoDonante);
        domino.Nivel.Should().Be("sugerido", "guarda anti-dominó — prioridad normal, no una emergencia nueva");
    }

    [Fact]
    public async Task N2_bloqueado_por_su_propio_piso_se_fuerza_con_justificacion_en_vez_de_escalar_a_N3()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var puestoVacante = await CrearVacanteCriticaAsync(ctx, lineaId: 4);

        await SetPisoAsync(4, minimo: 1);
        var (operadorB4, puestoDonante) = await OcuparRotativoOperadorBAsync(ctx, lineaId: 4, usuario); // exactamente en el piso

        var resultado = await CubrirAsync(puestoVacante, usuario,
            justificacionMotivoId: 4, justificacionTexto: "Coordinador fuerza por debajo del piso de seguridad de L4.");

        resultado.NivelAplicado.Should().Be("N2", "la misma justificación cubre forzar el piso de la propia línea, sin necesidad de cruzar a otra");
        resultado.CandidatoId.Should().Be(operadorB4);
        resultado.LineaOrigen.Should().Be((byte)4);

        var movimiento = await ctx.Movimientos.AsNoTracking().SingleAsync(m => m.Id == resultado.MovimientoId);
        var justificacion = await ctx.JustificacionesExcepcion.AsNoTracking().SingleAsync(j => j.Id == movimiento.JustificacionId);
        justificacion.TipoExcepcion.Should().Be("forzar_bajo_piso_seguridad");
    }
}

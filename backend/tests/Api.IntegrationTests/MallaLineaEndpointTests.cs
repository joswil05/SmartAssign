using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E6.4 (docs/PROGRESO.md): <c>GET /api/lineas/{lineaId}/puestos</c> —
/// la tarjeta de puesto tal como la exige 03 §3.1, contra la Api real de
/// punta a punta (HTTP + JWT + alcance + los procedimientos/funciones
/// reales de E5, no una reimplementación en memoria).
/// </summary>
public class MallaLineaEndpointTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    // ═══ Identidad HTTP ═══

    private async Task<(int usuarioId, string username, string password)> CrearUsuarioAsync(
        string rol, string prefijo, byte? lineaSupervisada = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var credenciales = new ServicioCredenciales();
        var password = $"Clave#{Guid.NewGuid():N}"[..16];
        var (hash, salt) = credenciales.HashConSal(password);
        var username = $"{prefijo}_{Guid.NewGuid():N}"[..30];

        var usuario = new Usuario
        {
            Username = username, NombreCompleto = username, Rol = rol, OrigenIdentidad = "local",
            PasswordHash = hash, PasswordSalt = salt, Activo = true,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        if (lineaSupervisada is { } lineaId)
        {
            var linea = await db.Lineas.SingleAsync(l => l.Id == lineaId);
            linea.SupervisorActualId = usuario.Id;
            await db.SaveChangesAsync();
        }

        return (usuario.Id, username, password);
    }

    private static async Task<string> LoginAsync(HttpClient cliente, string username, string password, string deviceId)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { username, password, deviceId });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static void ConAutorizacion(HttpClient cliente, string token) =>
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ═══ Datos de dominio (mismo patrón que Reglas.SeguridadTests) ═══

    private async Task<int> CrearPersonaAsync(string categoria, byte? lineaFisicaActual = null, string situacion = "presente_sin_asignar")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = categoria, LineaFisicaActual = lineaFisicaActual, Situacion = situacion,
        };
        db.Personas.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CrearPuestoAsync(byte lineaId, string tipo, string? codigo = null,
        string? categoriaTitular = null, int? titularId = null, short? horasEnPuesto = null, short? umbralCriticoHoras = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var puesto = new Puesto
        {
            LineaId = lineaId, Codigo = codigo ?? $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba",
            Tipo = tipo, CategoriaTitular = categoriaTitular, TitularId = titularId, Activo = true,
            HorasEnPuesto = horasEnPuesto, UmbralCriticoHoras = umbralCriticoHoras,
        };
        db.Puestos.Add(puesto);
        await db.SaveChangesAsync();
        return puesto.Id;
    }

    private async Task<int> CrearPersonaConDobleTurnoAsync(string categoria, bool dobleTurno, byte? lineaFisicaActual = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba",
            Categoria = categoria, LineaFisicaActual = lineaFisicaActual, Situacion = "presente_sin_asignar",
            DobleTurno = dobleTurno,
        };
        db.Personas.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>
    /// Asigna directamente por EF, sin pasar por <c>sp_AsignarPersona</c>
    /// — igual que <c>Reglas.SeguridadTests</c> — para poder controlar
    /// <c>Inicio</c> y así probar E7.4 (nivel/exceso de fatiga) sin
    /// depender de tiempo real de ejecución.
    /// </summary>
    private async Task AsignarDirectoDesdeHaceAsync(int puestoId, int personalId, int jornadaLineaId, int usuarioId, int minutosAtras)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        db.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornadaLineaId, PuestoId = puestoId, PersonalId = personalId,
            Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddMinutes(-minutosAtras), AsignadoPor = usuarioId,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// JornadaLinea tiene RLS (04 §6.3, E4.7) — el <c>db</c> de un scope
    /// creado fuera de una petición HTTP real no trae contexto de
    /// coordinador, así que se lee por SQL crudo con el contexto puesto
    /// a mano, igual que <see cref="AbrirComoCoordinadorAsync"/>.
    /// </summary>
    private async Task<int> JornadaAbiertaDeAsync(byte lineaId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT Id FROM JornadaLinea WHERE linea_id = @linea_id AND cerrado_en IS NULL";
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task AgregarRestriccionMedicaAsync(int personalId, int registradoPor)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        db.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = personalId, CapacidadId = 1,
            FechaInicio = new DateOnly(2020, 1, 1), FechaFin = null, FechaDictamen = new DateOnly(2020, 1, 1),
            Fuente = "Enfermería", RegistradoPor = registradoPor,
        });
        await db.SaveChangesAsync();
    }

    private async Task<byte> CrearTurnoAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        db.Turnos.Add(turno);
        await db.SaveChangesAsync();
        return turno.Id;
    }

    private async Task<int> CrearSkuAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100 };
        db.Skus.Add(sku);
        await db.SaveChangesAsync();
        return sku.Id;
    }

    // ═══ Procedimientos reales de E5, vía SQL crudo ═══

    private async Task<SqlConnection> AbrirComoCoordinadorAsync()
    {
        var conexion = new SqlConnection(factory.CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
        return conexion;
    }

    private async Task PlanificarLineaAsync(byte lineaId, byte turnoId, DateOnly dia, int? skuId, int? supervisorId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_PlanificarLinea";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@sku_id", (object?)skuId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@supervisor_id", (object?)supervisorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@jornada_linea_id", SqlDbType.Int) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ConfirmarPlanificacionAsync(byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ConfirmarPlanificacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        cmd.Parameters.Add(new SqlParameter("@lineas_sin_supervisor", SqlDbType.VarChar, 200) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ArrancarTurnoAsync(byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ArrancarTurno";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// <paramref name="actorUsuarioId"/> es quien planifica/confirma
    /// (el Coordinador); <paramref name="supervisorUsuarioId"/> es a
    /// quien <c>sp_ConfirmarPlanificacion</c> le sincroniza
    /// <c>Linea.SupervisorActualId</c> (E5.4). Confundirlos deja al
    /// supervisor de la prueba sin acceso a su propia línea — ver la
    /// nota de ingeniería de E6.4 en docs/PROGRESO.md.
    /// </summary>
    private async Task<(byte Turno, int Sku)> PrepararTurnoConfirmadoAsync(
        byte lineaId, DateOnly dia, int actorUsuarioId, int supervisorUsuarioId)
    {
        var turno = await CrearTurnoAsync();
        var sku = await CrearSkuAsync();
        await PlanificarLineaAsync(lineaId, turno, dia, sku, supervisorUsuarioId, actorUsuarioId);
        await ConfirmarPlanificacionAsync(turno, dia, actorUsuarioId);
        return (turno, sku);
    }

    // ═══ Pruebas ═══

    [Fact]
    public async Task Un_fijo_asignado_por_el_barrido_muestra_ocupante_situacion_y_microcopia_correctos()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coordId = (await CrearUsuarioAsync("coordinador", "coord_malla_1")).usuarioId;
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_1", lineaSupervisada: 1);
        var (turno, _) = await PrepararTurnoConfirmadoAsync(1, dia, coordId, supId);
        var titular = await CrearPersonaAsync("operador_a", lineaFisicaActual: 1);
        var puestoId = await CrearPuestoAsync(1, "fijo", codigo: "L1-A01", categoriaTitular: "operador_a", titularId: titular);

        await ArrancarTurnoAsync(turno, dia, coordId);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-1"));
        var puestos = await (await cliente.GetAsync("/api/lineas/1/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("situacion").GetString().Should().Be("ocupado");
        puesto.GetProperty("ocupante").GetProperty("personalId").GetInt32().Should().Be(titular);
        puesto.GetProperty("microCopia").GetString().Should().Be("Asignado automáticamente por asistencia");
        puesto.GetProperty("indicadorMedico").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Un_fijo_cubierto_por_suplente_muestra_al_suplente_como_ocupante_no_al_titular()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coordId = (await CrearUsuarioAsync("coordinador", "coord_malla_2")).usuarioId;
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_2", lineaSupervisada: 2);
        var (turno, _) = await PrepararTurnoConfirmadoAsync(2, dia, coordId, supId);
        var titular = await CrearPersonaAsync("operador_a", lineaFisicaActual: 2, situacion: "ausente_justificado");
        var suplente = await CrearPersonaAsync("operador_b", lineaFisicaActual: 2);
        var puestoId = await CrearPuestoAsync(2, "fijo", codigo: "L2-A01", categoriaTitular: "operador_a", titularId: titular);

        await ArrancarTurnoAsync(turno, dia, coordId);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-2"));
        var puestos = await (await cliente.GetAsync("/api/lineas/2/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("ocupante").GetProperty("personalId").GetInt32().Should().Be(suplente);
        puesto.GetProperty("microCopia").GetString().Should().Be("Cubierto por suplente — titular ausente");
    }

    [Fact]
    public async Task Un_fijo_sin_candidato_tras_arrancar_es_vacante_critica()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coordId = (await CrearUsuarioAsync("coordinador", "coord_malla_3")).usuarioId;
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_3", lineaSupervisada: 6);
        var (turno, _) = await PrepararTurnoConfirmadoAsync(6, dia, coordId, supId);
        var puestoId = await CrearPuestoAsync(6, "fijo", codigo: "L6-A01", categoriaTitular: "operador_a");

        await ArrancarTurnoAsync(turno, dia, coordId);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-3"));
        var puestos = await (await cliente.GetAsync("/api/lineas/6/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("situacion").GetString().Should().Be("vacante_critica");
        puesto.GetProperty("ocupante").ValueKind.Should().Be(JsonValueKind.Null);
        puesto.GetProperty("microCopia").GetString().Should().Be("Sin titular ni suplente disponible");
    }

    [Fact]
    public async Task Un_fijo_antes_de_arrancar_el_turno_esta_libre_y_lo_dice_con_honestidad()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coordId = (await CrearUsuarioAsync("coordinador", "coord_malla_4")).usuarioId;
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_4", lineaSupervisada: 4);
        await PrepararTurnoConfirmadoAsync(4, dia, coordId, supId); // planificada + confirmada, SIN arrancar
        var puestoId = await CrearPuestoAsync(4, "fijo", codigo: "L4-A01", categoriaTitular: "operador_a");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-4"));
        var puestos = await (await cliente.GetAsync("/api/lineas/4/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("situacion").GetString().Should().Be("libre");
        puesto.GetProperty("microCopia").GetString().Should().Be("Esperando el arranque del turno");
    }

    [Fact]
    public async Task Un_rotativo_sin_ocupante_se_muestra_descubierto_no_libre_ni_vacante_critica()
    {
        // 03 §2.1: "Rotativo descubierto" es su propio estado visual — C12.
        // Necesita una jornada planificada+confirmada con SKU (si no,
        // fn_PuestoFueraDeOperacion ya lo marca fuera de operación, y esa
        // es una prueba distinta, más abajo).
        var dia = new DateOnly(2026, 8, 10);
        var coordId = (await CrearUsuarioAsync("coordinador", "coord_malla_5")).usuarioId;
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_5", lineaSupervisada: 3);
        await PrepararTurnoConfirmadoAsync(3, dia, coordId, supId);
        var puestoId = await CrearPuestoAsync(3, "rotativo", codigo: "L3-R01");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-5"));
        var puestos = await (await cliente.GetAsync("/api/lineas/3/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("situacion").GetString().Should().Be("descubierto");
        puesto.GetProperty("microCopia").GetString().Should().Be("Sin ocupante — pendiente de cubrir");
    }

    [Fact]
    public async Task Un_puesto_sin_ninguna_jornada_abierta_esta_fuera_de_operacion()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_6", lineaSupervisada: 5);
        var puestoId = await CrearPuestoAsync(5, "fijo", codigo: "L5-A01");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-6"));
        var puestos = await (await cliente.GetAsync("/api/lineas/5/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("situacion").GetString().Should().Be("fuera_de_operacion");
        puesto.GetProperty("microCopia").GetString().Should().Be("Puesto no requerido por el SKU de hoy");
    }

    [Fact]
    public async Task El_indicador_medico_cuenta_las_restricciones_vigentes_del_ocupante()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coord = await CrearUsuarioAsync("coordinador", "coord_malla_7");
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_7", lineaSupervisada: 7);
        var (turno, _) = await PrepararTurnoConfirmadoAsync(7, dia, coord.usuarioId, supId);
        var titular = await CrearPersonaAsync("operador_a", lineaFisicaActual: 7);
        var puestoId = await CrearPuestoAsync(7, "fijo", codigo: "L7-A01", categoriaTitular: "operador_a", titularId: titular);
        await AgregarRestriccionMedicaAsync(titular, coord.usuarioId);

        // La restricción no bloquea este puesto (sin PuestoCapacidad ligada) — solo debe contarse, no impedir la asignación.
        await ArrancarTurnoAsync(turno, dia, coord.usuarioId);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-7"));
        var puestos = await (await cliente.GetAsync("/api/lineas/7/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("indicadorMedico").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Los_fijos_aparecen_antes_que_los_rotativos_y_cada_grupo_ordenado_por_codigo()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_8", lineaSupervisada: 9);
        await CrearPuestoAsync(9, "rotativo", codigo: "L9-R02");
        await CrearPuestoAsync(9, "fijo", codigo: "L9-A02");
        await CrearPuestoAsync(9, "rotativo", codigo: "L9-R01");
        await CrearPuestoAsync(9, "fijo", codigo: "L9-A01");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-8"));
        var puestos = await (await cliente.GetAsync("/api/lineas/9/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var codigos = puestos.EnumerateArray().Select(p => p.GetProperty("codigo").GetString()).ToList();
        codigos.Should().Equal("L9-A01", "L9-A02", "L9-R01", "L9-R02");
    }

    [Fact]
    public async Task Un_supervisor_de_otra_linea_no_puede_ver_la_malla_ajena()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_9", lineaSupervisada: 1);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-9"));

        (await cliente.GetAsync("/api/lineas/2/puestos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══ E7.4 — nivel/exceso de fatiga y distintivo de doble turno ═══
    //
    // Líneas 8 y 10: las dos únicas sin reclamar por el resto de la
    // suite (docs/PROGRESO.md, nota de esta UT) — cada prueba abre su
    // propia jornada, así que reutiliza otra línea ya usada arriba
    // rompería UX_JornadaLinea_abierta o el conteo exacto de puestos de
    // "Los_fijos_aparecen_antes...". Cada prueba también cubre, de paso,
    // el distintivo de doble turno (00 §B7) para no reclamar una tercera
    // línea solo para eso.

    [Fact]
    public async Task Un_rotativo_ocupado_muestra_nivel_exceso_de_fatiga_y_el_distintivo_de_doble_turno()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coordId = (await CrearUsuarioAsync("coordinador", "coord_malla_10")).usuarioId;
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_10", lineaSupervisada: 8);
        var (turno, _) = await PrepararTurnoConfirmadoAsync(8, dia, coordId, supId);
        var jornadaId = await JornadaAbiertaDeAsync(8);

        var puestoId = await CrearPuestoAsync(8, "rotativo", codigo: "L8-R01", horasEnPuesto: 1); // umbral 60 min
        var persona = await CrearPersonaConDobleTurnoAsync("operario", dobleTurno: true, lineaFisicaActual: 8);
        await AsignarDirectoDesdeHaceAsync(puestoId, persona, jornadaId, coordId, minutosAtras: 90);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-10"));
        var puestos = await (await cliente.GetAsync("/api/lineas/8/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("nivelFatiga").GetString().Should().Be("sugerido");
        puesto.GetProperty("excesoFatiga").GetDecimal().Should().BeGreaterThan(100m, "90 min sobre un umbral de 60 supera el 100%");
        // 00 §B7: distintivo permanente en la persona, visible también en la malla.
        puesto.GetProperty("ocupante").GetProperty("dobleTurno").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Un_fijo_nunca_muestra_nivel_ni_exceso_de_fatiga_aunque_este_ocupado()
    {
        // §9.1/§5.1: "los operadores en puestos fijos no entran en este
        // cálculo" — asignación directa (no barrido) para no depender de
        // arrancar el turno; sin doble turno, cubre el caso "false".
        var dia = new DateOnly(2026, 8, 10);
        var coordId = (await CrearUsuarioAsync("coordinador", "coord_malla_11")).usuarioId;
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_malla_11", lineaSupervisada: 10);
        var (turno, _) = await PrepararTurnoConfirmadoAsync(10, dia, coordId, supId);
        var jornadaId = await JornadaAbiertaDeAsync(10);

        var puestoId = await CrearPuestoAsync(10, "fijo", codigo: "L10-A01", categoriaTitular: "operador_a", horasEnPuesto: 1);
        var titular = await CrearPersonaAsync("operador_a", lineaFisicaActual: 10);
        await AsignarDirectoDesdeHaceAsync(puestoId, titular, jornadaId, coordId, minutosAtras: 500);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-malla-11"));
        var puestos = await (await cliente.GetAsync("/api/lineas/10/puestos")).Content.ReadFromJsonAsync<JsonElement>();

        var puesto = puestos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == puestoId);
        puesto.GetProperty("nivelFatiga").ValueKind.Should().Be(JsonValueKind.Null);
        puesto.GetProperty("excesoFatiga").ValueKind.Should().Be(JsonValueKind.Null);
        puesto.GetProperty("ocupante").GetProperty("dobleTurno").GetBoolean().Should().BeFalse();
    }
}

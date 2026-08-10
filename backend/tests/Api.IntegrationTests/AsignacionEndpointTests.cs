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
/// UT-E6.8 (docs/PROGRESO.md): <c>GET /api/asignaciones/sugerencia</c>
/// (E6.7) y <c>POST /api/puestos/{id}/asignar</c> (E6.8) contra la Api
/// real de punta a punta — HTTP + JWT + alcance + los procedimientos
/// reales de E6.7/E6.8. Mismo patrón que MallaLineaEndpointTests (E6.4).
/// </summary>
public class AsignacionEndpointTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
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

    private async Task<int> CrearPersonaAsync(string categoria = "operario")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var p = new Personal { Ficha = $"F{Guid.NewGuid():N}"[..12], NombreCompleto = "Persona de prueba", Categoria = categoria };
        db.Personas.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CrearPuestoRotativoAsync(byte lineaId, string? codigo = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var puesto = new Puesto { LineaId = lineaId, Codigo = codigo ?? $"T{Guid.NewGuid():N}"[..15], NombrePuesto = "Puesto de prueba", Tipo = "rotativo" };
        db.Puestos.Add(puesto);
        await db.SaveChangesAsync();
        return puesto.Id;
    }

    private async Task<SqlConnection> AbrirComoCoordinadorAsync()
    {
        var conexion = new SqlConnection(factory.CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
        return conexion;
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
        var pRechazoPlan = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pRechazoPlan);
        await cmd.ExecuteNonQueryAsync();
        (pRechazoPlan.Value as string).Should().BeNull($"sp_PlanificarLinea no debe rechazar en el fixture de prueba (línea {lineaId})");
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
        var pRechazoConfirmar = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pRechazoConfirmar);
        cmd.Parameters.Add(new SqlParameter("@lineas_sin_supervisor", SqlDbType.VarChar, 200) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        (pRechazoConfirmar.Value as string).Should().BeNull($"sp_ConfirmarPlanificacion no debe rechazar en el fixture de prueba (turno {turnoId})");
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
        var pRechazoArranque = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pRechazoArranque);
        await cmd.ExecuteNonQueryAsync();
        (pRechazoArranque.Value as string).Should().BeNull($"sp_ArrancarTurno no debe rechazar en el fixture de prueba (turno {turnoId})");
    }

    private async Task<(byte Turno, int Sku)> PrepararTurnoConfirmadoYArrancadoAsync(
        byte lineaId, DateOnly dia, int actorUsuarioId, int supervisorUsuarioId)
    {
        var turno = await CrearTurnoAsync();
        var sku = await CrearSkuAsync();
        await PlanificarLineaAsync(lineaId, turno, dia, sku, supervisorUsuarioId, actorUsuarioId);
        await ConfirmarPlanificacionAsync(turno, dia, actorUsuarioId);
        await ArrancarTurnoAsync(turno, dia, actorUsuarioId);
        return (turno, sku);
    }

    // ═══ GET /api/asignaciones/sugerencia (E6.7) ═══

    [Fact]
    public async Task La_sugerencia_devuelve_un_puesto_libre_de_la_propia_linea_del_supervisor()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coord = await CrearUsuarioAsync("coordinador", "coord_sug_1");
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_sug_1", lineaSupervisada: 1);
        await PrepararTurnoConfirmadoYArrancadoAsync(1, dia, coord.usuarioId, supId);
        var persona = await CrearPersonaAsync();
        var puesto = await CrearPuestoRotativoAsync(1, codigo: "L1-SUG01");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-sug-1"));
        var respuesta = await cliente.GetAsync($"/api/asignaciones/sugerencia?personalId={persona}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("puestoId").GetInt32().Should().Be(puesto);
        cuerpo.GetProperty("nivel").GetByte().Should().Be(3);
    }

    [Fact]
    public async Task La_sugerencia_nunca_cruza_a_una_linea_ajena()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coord = await CrearUsuarioAsync("coordinador", "coord_sug_2");
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_sug_2", lineaSupervisada: 2);
        await PrepararTurnoConfirmadoYArrancadoAsync(2, dia, coord.usuarioId, supId);
        var persona = await CrearPersonaAsync();
        await CrearPuestoRotativoAsync(9, codigo: "L9-AJENO"); // libre, pero de OTRA línea (9, sin uso en otra prueba de esta clase)

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-sug-2"));
        var cuerpo = await (await cliente.GetAsync($"/api/asignaciones/sugerencia?personalId={persona}")).Content.ReadFromJsonAsync<JsonElement>();

        cuerpo.GetProperty("puestoId").ValueKind.Should().Be(JsonValueKind.Null);
        cuerpo.GetProperty("codigoRechazo").GetString().Should().Be("SIN_PUESTOS_LIBRES");
    }

    [Fact]
    public async Task Un_coordinador_no_puede_pedir_sugerencia_no_tiene_linea_propia()
    {
        var (_, username, password) = await CrearUsuarioAsync("coordinador", "coord_sug_3");
        var persona = await CrearPersonaAsync();

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-sug-3"));
        var respuesta = await cliente.GetAsync($"/api/asignaciones/sugerencia?personalId={persona}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══ POST /api/puestos/{id}/asignar (E6.8) ═══

    [Fact]
    public async Task Asignar_con_exito_devuelve_200_y_el_id_de_la_asignacion()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coord = await CrearUsuarioAsync("coordinador", "coord_asig_1");
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_asig_1", lineaSupervisada: 3);
        await PrepararTurnoConfirmadoYArrancadoAsync(3, dia, coord.usuarioId, supId);
        var persona = await CrearPersonaAsync();
        var puesto = await CrearPuestoRotativoAsync(3, codigo: "L3-ASIG01");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-asig-1"));
        var respuesta = await cliente.PostAsJsonAsync($"/api/puestos/{puesto}/asignar",
            new { personalId = persona, idempotencyKey = Guid.NewGuid() });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("asignacionId").GetInt64().Should().BePositive();
    }

    [Fact]
    public async Task Asignar_un_puesto_ya_ocupado_devuelve_409_con_mensaje_nominal()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coord = await CrearUsuarioAsync("coordinador", "coord_asig_2");
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_asig_2", lineaSupervisada: 4);
        await PrepararTurnoConfirmadoYArrancadoAsync(4, dia, coord.usuarioId, supId);
        var ganador = await CrearPersonaAsync();
        var perdedor = await CrearPersonaAsync();
        var puesto = await CrearPuestoRotativoAsync(4, codigo: "L4-ASIG02");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-asig-2"));
        await cliente.PostAsJsonAsync($"/api/puestos/{puesto}/asignar", new { personalId = ganador, idempotencyKey = Guid.NewGuid() });
        var respuesta = await cliente.PostAsJsonAsync($"/api/puestos/{puesto}/asignar", new { personalId = perdedor, idempotencyKey = Guid.NewGuid() });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("codigo").GetString().Should().Be("PUESTO_OCUPADO");
        cuerpo.GetProperty("mensaje").GetString().Should().Contain("por otro supervisor");
    }

    [Fact]
    public async Task Reintentar_con_la_misma_clave_de_idempotencia_no_duplica_la_asignacion()
    {
        var dia = new DateOnly(2026, 8, 10);
        var coord = await CrearUsuarioAsync("coordinador", "coord_asig_3");
        var (supId, username, password) = await CrearUsuarioAsync("supervisor", "sup_asig_3", lineaSupervisada: 5);
        await PrepararTurnoConfirmadoYArrancadoAsync(5, dia, coord.usuarioId, supId);
        var persona = await CrearPersonaAsync();
        var puesto = await CrearPuestoRotativoAsync(5, codigo: "L5-ASIG03");
        var clave = Guid.NewGuid();

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-asig-3"));
        var primero = await cliente.PostAsJsonAsync($"/api/puestos/{puesto}/asignar", new { personalId = persona, idempotencyKey = clave });
        var segundo = await cliente.PostAsJsonAsync($"/api/puestos/{puesto}/asignar", new { personalId = persona, idempotencyKey = clave });

        var idPrimero = (await primero.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("asignacionId").GetInt64();
        var idSegundo = (await segundo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("asignacionId").GetInt64();
        idSegundo.Should().Be(idPrimero);
    }

    [Fact]
    public async Task Un_supervisor_no_puede_asignar_en_un_puesto_de_otra_linea_la_ve_como_inexistente()
    {
        // La RLS de Puesto (04 §6.3) filtra la fila antes de que el
        // endpoint llegue a comparar alcances — "no existe" y "existe
        // pero no la veo" son indistinguibles a propósito, no es una fuga
        // de información sobre qué puestos tiene la planta.
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_asig_4", lineaSupervisada: 6);
        var persona = await CrearPersonaAsync();
        var puestoAjeno = await CrearPuestoRotativoAsync(7, codigo: "L7-ASIG04");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-asig-4"));
        var respuesta = await cliente.PostAsJsonAsync($"/api/puestos/{puestoAjeno}/asignar",
            new { personalId = persona, idempotencyKey = Guid.NewGuid() });

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

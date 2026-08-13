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
/// UT-E14.3 (docs/PROGRESO.md): "Histórico y auditoría consultable"
/// (§2.1.11, §12.7) contra la Api real de punta a punta — HTTP + JWT +
/// rol. Mismo patrón que <c>AsignacionEndpointTests</c> (E6.8).
/// </summary>
public class HistoricoYAuditoriaEndpointTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private const short CategoriaMecanico = 1;
    private const short CausaAveriaMaquina = 1;

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

    private async Task<int> PrepararJornadaArrancadaAsync(byte lineaId, DateOnly dia, int actorUsuarioId)
    {
        var (supervisorId, _, _) = await CrearUsuarioAsync("supervisor", $"sup_hist_l{lineaId}", lineaSupervisada: lineaId);
        var turno = await CrearTurnoAsync();
        var sku = await CrearSkuAsync();
        await PlanificarLineaAsync(lineaId, turno, dia, sku, supervisorId, actorUsuarioId);
        await ConfirmarPlanificacionAsync(turno, dia, actorUsuarioId);
        await ArrancarTurnoAsync(turno, dia, actorUsuarioId);

        // JornadaLinea lleva RLS (04 §6.3) — un scope ad-hoc sin pasar por
        // ContextoSesionMiddleware no ve ninguna fila (por diseño, "cierra
        // en falso"); se lee por la conexión cruda de coordinador, mismo
        // patrón que CierreDeTurnoConListaExactaDeBloqueosTests (E14.1).
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT Id FROM JornadaLinea WHERE linea_id = @lineaId AND dia_operacion = @dia";
        cmd.Parameters.AddWithValue("@lineaId", lineaId);
        cmd.Parameters.AddWithValue("@dia", dia.ToDateTime(TimeOnly.MinValue));
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task CerrarTurnoAsync(int jornadaLineaId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CerrarTurno";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@jornada_linea_id", jornadaLineaId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@justificacion_motivo_id", DBNull.Value);
        cmd.Parameters.AddWithValue("@justificacion_texto", DBNull.Value);
        cmd.Parameters.Add(new SqlParameter("@bloqueos", SqlDbType.NVarChar, -1) { Direction = ParameterDirection.Output });
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        (pCodigo.Value as string).Should().BeNull("sp_CerrarTurno no debe rechazar en el fixture de prueba (sin bloqueos)");
    }

    /// <summary>
    /// 00 §C5, literal: <c>sp_ArrancarTurno</c> abre el lote número 1 de
    /// cada jornada automáticamente — <c>PrepararJornadaArrancadaAsync</c>
    /// siempre deja uno abierto, que bloquearía cualquier cierre limpio
    /// de turno (E14.1) si no se cierra antes.
    /// </summary>
    private async Task<int> ObtenerLoteAbiertoIdAsync(int jornadaLineaId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Lote WHERE jornada_linea_id = @jornadaId AND cerrado_en IS NULL";
        cmd.Parameters.AddWithValue("@jornadaId", jornadaLineaId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<int?> CerrarLoteAsync(int loteId, decimal produccionReal, decimal danoOrigen, decimal danoProceso, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CerrarLote";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@lote_id", loteId);
        cmd.Parameters.AddWithValue("@produccion_real", produccionReal);
        cmd.Parameters.AddWithValue("@dano_origen", danoOrigen);
        cmd.Parameters.AddWithValue("@dano_proceso", danoProceso);
        cmd.Parameters.AddWithValue("@justificacion", DBNull.Value);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pDesperdicioId = new SqlParameter("@desperdicio_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pDesperdicioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        (pCodigo.Value as string).Should().BeNull("sp_CerrarLote no debe rechazar en el fixture de prueba");
        return pDesperdicioId.Value as int?;
    }

    // ═══ GET /api/historico/jornadas ═══

    [Fact]
    public async Task El_coordinador_ve_las_jornadas_cerradas_pero_no_las_que_siguen_abiertas()
    {
        var dia = new DateOnly(2026, 8, 12);
        var coord = await CrearUsuarioAsync("coordinador", "coord_hist_1");
        var jornadaCerrada = await PrepararJornadaArrancadaAsync(1, dia, coord.usuarioId);
        await CerrarLoteAsync(await ObtenerLoteAbiertoIdAsync(jornadaCerrada), 100, 0, 0, coord.usuarioId);
        await CerrarTurnoAsync(jornadaCerrada, coord.usuarioId);
        var jornadaAbierta = await PrepararJornadaArrancadaAsync(2, dia, coord.usuarioId);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, coord.username, coord.password, "device-hist-1"));
        var respuesta = await cliente.GetAsync("/api/historico/jornadas?lineaId=1");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var filas = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var ids = filas.EnumerateArray().Select(f => f.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain(jornadaCerrada);
        ids.Should().NotContain(jornadaAbierta, "una jornada en curso no es histórico todavía");
    }

    [Fact]
    public async Task Un_supervisor_no_puede_consultar_el_historico()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_hist_1", lineaSupervisada: 3);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-hist-2"));
        var respuesta = await cliente.GetAsync("/api/historico/jornadas");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden, "§2.1.11 es función del Coordinador — §2.2 no la lista para el Supervisor");
    }

    [Fact]
    public async Task El_cierre_forzado_queda_marcado_en_el_resumen_del_historico()
    {
        var dia = new DateOnly(2026, 8, 12);
        var coord = await CrearUsuarioAsync("coordinador", "coord_hist_2");
        var jornada = await PrepararJornadaArrancadaAsync(4, dia, coord.usuarioId);

        // Fuerza el cierre con el lote número 1 que sp_ArrancarTurno ya
        // deja abierto (00 §C5) — mismo bloqueo de prueba que
        // CierreForzadoConJustificacionTests, E14.2, sin necesidad de un
        // segundo lote (UX_Lote exige número único por jornada).
        await using (var conexion = await AbrirComoCoordinadorAsync())
        {
            await using var cmd = conexion.CreateCommand();
            cmd.CommandText = "sp_CerrarTurno";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@jornada_linea_id", jornada);
            cmd.Parameters.AddWithValue("@usuario_id", coord.usuarioId);
            cmd.Parameters.AddWithValue("@justificacion_motivo_id", (short)3);
            cmd.Parameters.AddWithValue("@justificacion_texto", "Forzando el cierre para la prueba del histórico.");
            cmd.Parameters.Add(new SqlParameter("@bloqueos", SqlDbType.NVarChar, -1) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output });
            await cmd.ExecuteNonQueryAsync();
        }

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, coord.username, coord.password, "device-hist-3"));
        var filas = await (await cliente.GetAsync("/api/historico/jornadas?lineaId=4")).Content.ReadFromJsonAsync<JsonElement>();
        var fila = filas.EnumerateArray().Single(f => f.GetProperty("id").GetInt32() == jornada);

        fila.GetProperty("cierreForzado").GetBoolean().Should().BeTrue();
    }

    // ═══ GET /api/historico/jornadas/{id} ═══

    [Fact]
    public async Task El_detalle_incluye_paros_desperdicio_y_eficiencia()
    {
        var dia = new DateOnly(2026, 8, 12);
        var coord = await CrearUsuarioAsync("coordinador", "coord_hist_3");
        var jornada = await PrepararJornadaArrancadaAsync(5, dia, coord.usuarioId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
            db.Paros.Add(new Paro
            {
                JornadaLineaId = jornada, CategoriaId = CategoriaMecanico, CausaId = CausaAveriaMaquina,
                Descripcion = "Correa transportadora se salió de su riel.", Inicio = DateTime.UtcNow.AddMinutes(-30),
                Fin = DateTime.UtcNow.AddMinutes(-20), RegistradoPor = coord.usuarioId,
            });
            await db.SaveChangesAsync();
        }

        // Cierra el lote número 1 que sp_ArrancarTurno ya dejó abierto
        // (00 §C5) por sp_CerrarLote real — deja producción y desperdicio
        // reales, y libera el bloqueo para el cierre de turno limpio.
        var loteId = await ObtenerLoteAbiertoIdAsync(jornada);
        await CerrarLoteAsync(loteId, produccionReal: 500, danoOrigen: 3.5m, danoProceso: 1.2m, coord.usuarioId);
        await CerrarTurnoAsync(jornada, coord.usuarioId);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, coord.username, coord.password, "device-hist-4"));
        var respuesta = await cliente.GetAsync($"/api/historico/jornadas/{jornada}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("jornada").GetProperty("id").GetInt32().Should().Be(jornada);

        var paros = cuerpo.GetProperty("paros").EnumerateArray().ToList();
        paros.Should().HaveCount(1);
        paros[0].GetProperty("descripcion").GetString().Should().Be("Correa transportadora se salió de su riel.");
        paros[0].GetProperty("duracionMin").GetInt32().Should().Be(10);

        var desperdicio = cuerpo.GetProperty("desperdicio").EnumerateArray().ToList();
        desperdicio.Should().HaveCount(1);
        desperdicio[0].GetProperty("loteId").GetInt32().Should().Be(loteId);
        desperdicio[0].GetProperty("danoOrigen").GetDecimal().Should().Be(3.5m);

        var eficiencia = cuerpo.GetProperty("eficiencia");
        eficiencia.GetProperty("produccionReal").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task Una_jornada_que_todavia_no_cerro_no_aparece_en_el_detalle_del_historico()
    {
        var dia = new DateOnly(2026, 8, 12);
        var coord = await CrearUsuarioAsync("coordinador", "coord_hist_4");
        var jornada = await PrepararJornadaArrancadaAsync(6, dia, coord.usuarioId);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, coord.username, coord.password, "device-hist-5"));
        var respuesta = await cliente.GetAsync($"/api/historico/jornadas/{jornada}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound, "\"anteriores\" son jornadas cerradas — una en curso no es histórico todavía");
    }

    [Fact]
    public async Task Una_jornada_inexistente_devuelve_404_en_el_detalle()
    {
        var coord = await CrearUsuarioAsync("coordinador", "coord_hist_5");

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, coord.username, coord.password, "device-hist-6"));
        var respuesta = await cliente.GetAsync("/api/historico/jornadas/999999");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ═══ GET /api/auditoria ═══

    [Fact]
    public async Task El_coordinador_consulta_la_auditoria_con_el_nombre_del_actor_resuelto()
    {
        var coord = await CrearUsuarioAsync("coordinador", "coord_aud_1");
        var actor = await CrearUsuarioAsync("supervisor", "actor_aud_1", lineaSupervisada: 7);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
            db.Auditorias.Add(new Auditoria
            {
                UsuarioId = actor.usuarioId, Rol = "supervisor", Accion = "ASIGNAR", Entidad = "Asignacion",
                LineaId = 7, Resultado = "OK", OcurridoEn = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, coord.username, coord.password, "device-aud-1"));
        var respuesta = await cliente.GetAsync("/api/auditoria?lineaId=7");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var filas = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var fila = filas.EnumerateArray().Single();
        fila.GetProperty("usuarioNombre").GetString().Should().Be(actor.username);
        fila.GetProperty("accion").GetString().Should().Be("ASIGNAR");
        fila.GetProperty("resultado").GetString().Should().Be("OK");
    }

    [Fact]
    public async Task La_auditoria_filtra_por_linea_sin_traer_las_de_otras_lineas()
    {
        var coord = await CrearUsuarioAsync("coordinador", "coord_aud_2");
        var actor = await CrearUsuarioAsync("supervisor", "actor_aud_2", lineaSupervisada: 8);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
            db.Auditorias.Add(new Auditoria { UsuarioId = actor.usuarioId, Rol = "supervisor", Accion = "ASIGNAR", Entidad = "Asignacion", LineaId = 8, Resultado = "OK", OcurridoEn = DateTime.UtcNow });
            db.Auditorias.Add(new Auditoria { UsuarioId = actor.usuarioId, Rol = "supervisor", Accion = "ASIGNAR", Entidad = "Asignacion", LineaId = 9, Resultado = "OK", OcurridoEn = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, coord.username, coord.password, "device-aud-2"));
        var filas = await (await cliente.GetAsync("/api/auditoria?lineaId=8")).Content.ReadFromJsonAsync<JsonElement>();

        filas.EnumerateArray().Should().OnlyContain(f => f.GetProperty("lineaId").GetByte() == 8);
    }

    [Fact]
    public async Task Un_supervisor_no_puede_consultar_la_auditoria()
    {
        var (_, username, password) = await CrearUsuarioAsync("supervisor", "sup_aud_1", lineaSupervisada: 10);

        using var cliente = factory.CreateClient();
        ConAutorizacion(cliente, await LoginAsync(cliente, username, password, "device-aud-3"));
        var respuesta = await cliente.GetAsync("/api/auditoria");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden, "§12.7 está bajo la sección Coordinador de 05_TRD.md §2.3");
    }
}

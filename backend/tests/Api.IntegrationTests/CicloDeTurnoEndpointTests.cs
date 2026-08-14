using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Application.Seguridad;
using SmartAssign.Application.Tiempo;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>: el ciclo diario entero
/// —planificar, confirmar, arrancar, cerrar— estaba construido y probado en
/// SQL desde E5 y E14, pero sin ninguna vía desde la app. Un turno real no
/// se podía correr de punta a punta desde un teléfono.
///
/// Lo que se prueba aquí es el <b>recorrido completo por HTTP</b>, no las
/// reglas: esas ya las cubren <c>PlanificacionYBarridoTests</c> y
/// <c>CierreDeTurnoTests</c> a nivel de procedimiento. Aquí importa que la
/// app llegue a ellas, con el rol correcto y con el rechazo legible.
/// </summary>
public class CicloDeTurnoEndpointTests(SmartAssignApiFactory fabrica) : IClassFixture<SmartAssignApiFactory>
{
    private const byte LineaDePrueba = 5;

    private async Task<(HttpClient Cliente, int UsuarioId)> ComoAsync(string rol, byte? lineaSupervisada = null)
    {
        string username, password;
        int usuarioId;

        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
            var credenciales = new ServicioCredenciales();
            password = $"Clave#{Guid.NewGuid():N}"[..16];
            var (hash, salt) = credenciales.HashConSal(password);
            username = $"ciclo_{Guid.NewGuid():N}"[..28];

            var usuario = new Usuario
            {
                Username = username, NombreCompleto = username, Rol = rol,
                OrigenIdentidad = "local", PasswordHash = hash, PasswordSalt = salt, Activo = true,
            };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            usuarioId = usuario.Id;

            if (lineaSupervisada is { } lineaId)
            {
                var previa = await db.Lineas.SingleOrDefaultAsync(l => l.SupervisorActualId == usuarioId);
                if (previa is not null) previa.SupervisorActualId = null;

                var linea = await db.Lineas.SingleAsync(l => l.Id == lineaId);
                linea.SupervisorActualId = usuarioId;
                await db.SaveChangesAsync();
            }
        }

        var cliente = fabrica.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username, password, deviceId = $"dev-{Guid.NewGuid():N}"[..20] });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cuerpo.GetProperty("accessToken").GetString()!);

        return (cliente, usuarioId);
    }

    /// <summary>
    /// Sin SKU la jornada no cuenta como línea activa —
    /// <c>sp_ArrancarTurno</c> rechaza con SIN_LINEAS_ACTIVAS y
    /// <c>sp_ConfirmarPlanificacion</c> no la mira— porque 00 §G3 dice que
    /// una línea sin SKU planificado queda inactiva y sus puestos pasan a
    /// fuera de operación. No es un detalle de la prueba: es la regla.
    /// </summary>
    private async Task<int> CrearSkuAsync()
    {
        using var scope = fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var sku = new Sku
        {
            Codigo = $"SKU{Guid.NewGuid():N}"[..14],
            Descripcion = "Producto de prueba",
            RitmoTeoricoHora = 1200m,
        };
        db.Skus.Add(sku);
        await db.SaveChangesAsync();
        return sku.Id;
    }

    private async Task<byte> CrearTurnoAsync(HttpClient coordinador)
    {
        var respuesta = await coordinador.PostAsJsonAsync("/api/maestros/turnos", new
        {
            nombre = $"T_{Guid.NewGuid():N}"[..12],
            horaInicio = "06:00:00",
            horaFin = "14:00:00",
        });
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return (byte)cuerpo.GetProperty("id").GetInt32();
    }

    // ── El recorrido que P-02 hacía imposible ───────────────────────────

    [Fact]
    public async Task Un_turno_se_planifica_confirma_y_arranca_entero_por_HTTP()
    {
        var (coordinador, _) = await ComoAsync("coordinador");
        var turnoId = await CrearTurnoAsync(coordinador);
        var dia = FechaPlanta.Hoy().AddDays(40); // día propio, para no chocar con otras pruebas

        // El supervisor del día se enlaza AL PLANIFICAR (E5.4 comprueba
        // JornadaLinea.supervisor_id, no Linea.supervisor_actual_id): es
        // cuando se decide quién lleva esa línea ese día.
        var (_, supervisorId) = await ComoAsync("supervisor", LineaDePrueba);

        var planificar = await coordinador.PostAsJsonAsync("/api/jornadas/planificar", new
        {
            lineaId = LineaDePrueba, turnoId, diaOperacion = dia, skuId = await CrearSkuAsync(), supervisorId,
        });
        planificar.StatusCode.Should().Be(HttpStatusCode.OK, await planificar.Content.ReadAsStringAsync());

        var jornada = (await planificar.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("jornadaLineaId").GetInt32();
        jornada.Should().BeGreaterThan(0);

        var confirmar = await coordinador.PostAsJsonAsync("/api/jornadas/confirmar", new { turnoId, diaOperacion = dia });
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK, await confirmar.Content.ReadAsStringAsync());

        var arrancar = await coordinador.PostAsJsonAsync("/api/jornadas/arrancar", new { turnoId, diaOperacion = dia });
        arrancar.StatusCode.Should().Be(HttpStatusCode.OK, await arrancar.Content.ReadAsStringAsync());

        using var scope = fabrica.Services.CreateScope();
        // Alcance de coordinador o la RLS de JornadaLinea (04 §6.3) esconde
        // la fila y la verificación fallaría por enmascaramiento, no porque
        // el arranque no ocurriera.
        scope.ServiceProvider.GetRequiredService<IContextoSesionActual>().Establecer("coordinador", null);
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var estado = await db.JornadasLinea.Where(j => j.Id == jornada).Select(j => j.Estado).SingleAsync();
        estado.Should().Be("arrancada", "§8.4: arrancar deja la jornada en marcha");
    }

    [Fact]
    public async Task Confirmar_sin_supervisor_nombra_la_linea_en_vez_de_fallar_en_seco()
    {
        // E5.4, rechazo nominal: "llama al supervisor de L2" solo se puede
        // decir si el servidor dice QUÉ línea, no un "no se pudo".
        var (coordinador, _) = await ComoAsync("coordinador");
        var turnoId = await CrearTurnoAsync(coordinador);
        var dia = FechaPlanta.Hoy().AddDays(41);

        await coordinador.PostAsJsonAsync("/api/jornadas/planificar", new
        {
            lineaId = (byte)7, turnoId, diaOperacion = dia, skuId = await CrearSkuAsync(), supervisorId = (int?)null,
        });

        var confirmar = await coordinador.PostAsJsonAsync("/api/jornadas/confirmar", new { turnoId, diaOperacion = dia });

        confirmar.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cuerpo = await confirmar.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("lineasSinSupervisor").GetString().Should().NotBeNullOrWhiteSpace(
            "el rechazo tiene que nombrar la línea concreta");
    }

    [Fact]
    public async Task El_cierre_devuelve_la_lista_exacta_de_bloqueos()
    {
        // 00 §C13 por HTTP: sp_ArrancarTurno abre el lote 1 automáticamente
        // (00 §C5), así que una jornada recién arrancada SIEMPRE tiene un
        // lote abierto — el bloqueo llega solo, sin construirlo a mano.
        var (coordinador, _) = await ComoAsync("coordinador");
        var turnoId = await CrearTurnoAsync(coordinador);
        var dia = FechaPlanta.Hoy().AddDays(42);

        var (_, supervisorId) = await ComoAsync("supervisor", (byte)9);

        var planificar = await coordinador.PostAsJsonAsync("/api/jornadas/planificar", new
        {
            lineaId = (byte)9, turnoId, diaOperacion = dia, skuId = await CrearSkuAsync(), supervisorId,
        });
        var jornada = (await planificar.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("jornadaLineaId").GetInt32();

        await coordinador.PostAsJsonAsync("/api/jornadas/confirmar", new { turnoId, diaOperacion = dia });
        await coordinador.PostAsJsonAsync("/api/jornadas/arrancar", new { turnoId, diaOperacion = dia });

        var cerrar = await coordinador.PostAsJsonAsync($"/api/jornadas/{jornada}/cerrar", new { });

        cerrar.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cuerpo = await cerrar.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("codigoRechazo").GetString().Should().Be("CIERRE_BLOQUEADO");
        cuerpo.GetProperty("bloqueos").ValueKind.Should().Be(JsonValueKind.Array,
            "C13 exige la lista, nunca un rechazo genérico");
        cuerpo.GetProperty("bloqueos").GetArrayLength().Should().BeGreaterThan(0);
    }

    // ── Quién puede hacer qué ───────────────────────────────────────────

    [Fact]
    public async Task Un_supervisor_no_planifica_ni_arranca_el_dia()
    {
        // Qué líneas operan lo decide el Coordinador (00 §G3).
        var (supervisor, _) = await ComoAsync("supervisor", (byte)6);

        var planificar = await supervisor.PostAsJsonAsync("/api/jornadas/planificar", new
        {
            lineaId = (byte)6, turnoId = (byte)1, diaOperacion = (DateOnly?)null,
            skuId = (int?)null, supervisorId = (int?)null,
        });
        var arrancar = await supervisor.PostAsJsonAsync("/api/jornadas/arrancar", new { turnoId = (byte)1, diaOperacion = (DateOnly?)null });

        planificar.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        arrancar.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_supervisor_no_cierra_la_jornada_de_otra_linea()
    {
        // El aislamiento de E2, aplicado al cierre: la línea sale de la
        // jornada, nunca de lo que afirme el cliente (§2.3).
        var (coordinador, _) = await ComoAsync("coordinador");
        var turnoId = await CrearTurnoAsync(coordinador);
        var dia = FechaPlanta.Hoy().AddDays(43);

        var planificar = await coordinador.PostAsJsonAsync("/api/jornadas/planificar", new
        {
            lineaId = (byte)4, turnoId, diaOperacion = dia, skuId = await CrearSkuAsync(), supervisorId = (int?)null,
        });
        var jornadaDeL4 = (await planificar.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("jornadaLineaId").GetInt32();

        var (ajeno, _) = await ComoAsync("supervisor", (byte)2);

        var cerrar = await ajeno.PostAsJsonAsync($"/api/jornadas/{jornadaDeL4}/cerrar", new { });

        // 404, no 403, y es lo correcto: JornadaLinea lleva RLS (04 §6.3),
        // así que para el supervisor de otra línea esa jornada sencillamente
        // NO EXISTE. Responder 403 confirmaría que existe una jornada de L4
        // — filtraría por el código de estado justo lo que el aislamiento
        // esconde en los datos.
        cerrar.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cerrar_una_jornada_que_no_existe_es_404_no_500()
    {
        var (coordinador, _) = await ComoAsync("coordinador");

        var cerrar = await coordinador.PostAsJsonAsync("/api/jornadas/999999/cerrar", new { });

        cerrar.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

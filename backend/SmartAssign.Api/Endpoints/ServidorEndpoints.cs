using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Preparacion;
using SmartAssign.Application.Tiempo;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// 05_TRD.md §2.3: "GET /servidor/info — Verificación al escanear el QR de
/// alta: confirma que la URL responde". Anónimo a propósito — se consulta
/// antes de que exista cualquier sesión (02 §1.0), justo después de
/// escanear el QR con la URL del servidor y antes de guardarla.
///
/// Revisión de producción, hallazgos <b>P-06</b> y <b>P-07</b>: <c>/info</c>
/// devolvía un objeto fijo sin tocar la base, así que respondía "OK" con la
/// base caída o sin migrar — el teléfono daba el alta por buena y después
/// fallaba todo lo demás sin relación aparente. Y nada comprobaba el
/// esquema al arrancar: se verificó que la Api levanta contra una base con
/// 15 de 50 migraciones y solo protesta en los logs de fondo.
/// </summary>
public static class ServidorEndpoints
{
    public record SaludBaseDatos(bool Alcanzable, int MigracionesPendientes, string? Error);
    public record SaludReloj(DateOnly FechaPlanta, string DesfaseUtc, bool ParecePuestoEnUtc);
    public record SaludParametro(string Clave, string ReglaDormida);

    public record SaludRespuesta(
        string Estado,
        SaludBaseDatos BaseDatos,
        SaludReloj Reloj,
        IReadOnlyList<SaludParametro> ParametrosSinConfigurar,
        IReadOnlyList<string> Avisos);

    public static IEndpointRouteBuilder MapServidorEndpoints(this IEndpointRouteBuilder app)
    {
        // El teléfono lo usa para verificar la URL antes de guardarla. Ahora
        // sí toca la base: decir "servidor OK" cuando la base no responde es
        // exactamente la deshonestidad de dato que §1.3 prohíbe.
        app.MapGet("/api/servidor/info", async (SmartAssignDbContext db, CancellationToken ct) =>
            {
                var alcanzable = await db.Database.CanConnectAsync(ct);
                return alcanzable
                    ? Results.Ok(new { servidor = "SmartAssign" })
                    : Results.Json(new { servidor = "SmartAssign", error = "base_de_datos_no_alcanzable" },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .AllowAnonymous();

        // El diagnóstico completo. Coordinador: dice qué reglas están
        // apagadas, que es información de operación, no de arranque.
        app.MapGet("/api/servidor/salud", async (SmartAssignDbContext db, CancellationToken ct) =>
            {
                var avisos = new List<string>();

                // ── Base de datos y esquema (P-07) ──────────────────────
                SaludBaseDatos baseDatos;
                var parametrosFaltantes = new List<SaludParametro>();
                try
                {
                    var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
                    baseDatos = new SaludBaseDatos(true, pendientes, null);

                    if (pendientes > 0)
                        avisos.Add($"El esquema tiene {pendientes} migración(es) sin aplicar. "
                                 + "Ejecuta 'dotnet ef database update' antes de operar.");

                    // ── Parámetros dormidos (P-04) ──────────────────────
                    var configuradas = await db.Parametros.Select(p => p.Clave).ToListAsync(ct);
                    parametrosFaltantes = CatalogoDeParametros.SinValorPorDefecto
                        .Where(p => !configuradas.Contains(p.Clave))
                        .Select(p => new SaludParametro(p.Clave, p.ReglaDormida))
                        .ToList();
                }
                catch (Exception ex)
                {
                    baseDatos = new SaludBaseDatos(false, 0, ex.GetBaseException().Message);
                    avisos.Add("No se pudo consultar la base de datos.");
                }

                // ── Reloj (P-01) ────────────────────────────────────────
                var desfase = FechaPlanta.DesfaseUtc();
                var enUtc = desfase == TimeSpan.Zero;
                if (enUtc)
                    avisos.Add("El reloj del servidor está en UTC. 00 §C6 dice que la hora es la del "
                             + "servidor y el servidor está en la planta: si la planta no opera en UTC, "
                             + "el día de operación y la vigencia de los dictámenes médicos saldrán corridos.");

                var reloj = new SaludReloj(FechaPlanta.Hoy(), desfase.ToString(@"hh\:mm"), enUtc);

                if (parametrosFaltantes.Count > 0)
                    avisos.Add($"{parametrosFaltantes.Count} parámetro(s) de planta sin configurar: "
                             + "las reglas que dependen de ellos no se aplican.");

                var estado = !baseDatos.Alcanzable || baseDatos.MigracionesPendientes > 0
                    ? "no_listo"
                    : avisos.Count > 0 ? "degradado" : "listo";

                var respuesta = new SaludRespuesta(estado, baseDatos, reloj, parametrosFaltantes, avisos);

                return estado == "no_listo"
                    ? Results.Json(respuesta, statusCode: StatusCodes.Status503ServiceUnavailable)
                    : Results.Ok(respuesta);
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        return app;
    }
}

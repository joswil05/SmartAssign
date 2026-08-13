using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Historico;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Endpoints;

/// <summary>
/// UT-E14.3 (docs/PROGRESO.md): "Histórico y auditoría consultable" —
/// §2.1.11, literal: "Consultar el histórico: jornadas anteriores,
/// paros, desperdicio y eficiencia" — función 11 del Coordinador, no del
/// Supervisor (§2.2 no la lista entre sus doce funciones). 05_TRD.md
/// §2.3 ya reserva la ruta (<c>GET /historico/...</c>, con puntos
/// suspensivos — la forma exacta queda deliberadamente sin especificar
/// ahí, es esta UT quien la decide). "Anteriores" = jornadas YA
/// CERRADAS; una jornada en curso no es histórico todavía, es la vista
/// en vivo que otras pantallas ya cubren — ni la lista ni el detalle
/// muestran una jornada que no haya cerrado (R2, no se inventa un
/// "histórico en vivo" que ninguna fuente pide).
///
/// Todo lo que compone la respuesta ya existía, construido y verificado
/// en UTs previas — esta UT no inventa cálculos nuevos, solo los hace
/// consultables agrupados por jornada: <c>Paro</c> (E11.1), <c>Lote</c>/
/// <c>Desperdicio</c> (E11.3), <c>sp_CalcularEficiencia</c> (E11.5/E11.7,
/// vía <c>IServicioHistorico</c>, mismo patrón Dapper que
/// <c>IServicioAsignacion</c>).
/// </summary>
public static class HistoricoEndpoints
{
    public record JornadaHistoricoResumen(
        int Id, byte LineaId, string LineaCodigo, string LineaNombre,
        byte TurnoId, string TurnoNombre, DateOnly DiaOperacion,
        int? SupervisorId, string? SupervisorNombre,
        DateTime? ArrancadoEn, DateTime? CerradoEn, bool CierreForzado);

    public record ParoHistorico(
        int Id, short CategoriaId, string CategoriaNombre, short CausaId, string CausaNombre,
        string Descripcion, DateTime Inicio, DateTime? Fin, int? DuracionMin);

    public record DesperdicioHistorico(int LoteId, short LoteNumero, decimal DanoOrigen, decimal DanoProceso, string? Justificacion);

    public record EficienciaHistoricaRespuesta(
        decimal? EficienciaPct, string? Tramo, decimal ProduccionReal, int TiempoEfectivoMarchaMin,
        decimal? RitmoTeoricoHora, int ParosAcumuladosMin, DateTime? UltimaActualizacionProduccion);

    public record JornadaHistoricoDetalle(
        JornadaHistoricoResumen Jornada, IReadOnlyList<ParoHistorico> Paros,
        IReadOnlyList<DesperdicioHistorico> Desperdicio, EficienciaHistoricaRespuesta Eficiencia);

    public static IEndpointRouteBuilder MapHistoricoEndpoints(this IEndpointRouteBuilder app)
    {
        // §2.1.11: solo el Coordinador tiene esta función — §2.2 no la
        // lista entre las doce del Supervisor, cuyo aislamiento (04 §6.1)
        // es "solo su línea", nunca "su línea en el pasado" tampoco.
        app.MapGet("/api/historico/jornadas", async (
                byte? lineaId, DateOnly? desde, DateOnly? hasta, SmartAssignDbContext db, CancellationToken ct) =>
            {
                var consulta = db.JornadasLinea.Where(j => j.Estado == "cerrada");
                if (lineaId is { } l) consulta = consulta.Where(j => j.LineaId == l);
                if (desde is { } d) consulta = consulta.Where(j => j.DiaOperacion >= d);
                if (hasta is { } h) consulta = consulta.Where(j => j.DiaOperacion <= h);

                var filas = await consulta
                    .OrderByDescending(j => j.DiaOperacion).ThenByDescending(j => j.Id)
                    .Select(j => new JornadaHistoricoResumen(
                        j.Id, j.LineaId, j.Linea!.Codigo, j.Linea.Nombre,
                        j.TurnoId, j.Turno!.Nombre, j.DiaOperacion,
                        j.SupervisorId, j.Supervisor == null ? null : j.Supervisor.NombreCompleto,
                        j.ArrancadoEn, j.CerradoEn, j.CerradoForzadoPor != null))
                    .ToListAsync(ct);

                return Results.Ok(filas);
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        app.MapGet("/api/historico/jornadas/{id:int}", async (
                int id, SmartAssignDbContext db, IServicioHistorico historico, CancellationToken ct) =>
            {
                var jornada = await db.JornadasLinea.Where(j => j.Id == id && j.Estado == "cerrada")
                    .Select(j => new JornadaHistoricoResumen(
                        j.Id, j.LineaId, j.Linea!.Codigo, j.Linea.Nombre,
                        j.TurnoId, j.Turno!.Nombre, j.DiaOperacion,
                        j.SupervisorId, j.Supervisor == null ? null : j.Supervisor.NombreCompleto,
                        j.ArrancadoEn, j.CerradoEn, j.CerradoForzadoPor != null))
                    .SingleOrDefaultAsync(ct);
                if (jornada is null) return Results.NotFound();

                var paros = await db.Paros.Where(p => p.JornadaLineaId == id)
                    .OrderBy(p => p.Inicio)
                    .Select(p => new ParoHistorico(
                        p.Id, p.CategoriaId, p.Categoria.Nombre, p.CausaId, p.Causa.Nombre,
                        p.Descripcion, p.Inicio, p.Fin,
                        p.Fin == null ? null : (int?)EF.Functions.DateDiffMinute(p.Inicio, p.Fin.Value)))
                    .ToListAsync(ct);

                // §11.3, C4: el desperdicio se registra por lote al cerrarlo
                // — un lote por fila, nunca agregado a nivel de jornada, la
                // separación origen/proceso (§4.3) es por lote también.
                var desperdicio = await db.Desperdicios
                    .Where(d => d.Lote.JornadaLineaId == id)
                    .OrderBy(d => d.Lote.Numero)
                    .Select(d => new DesperdicioHistorico(d.LoteId, d.Lote.Numero, d.DanoOrigen, d.DanoProceso, d.Justificacion))
                    .ToListAsync(ct);

                var eficiencia = await historico.CalcularEficienciaAsync(id, ct);
                var eficienciaRespuesta = new EficienciaHistoricaRespuesta(
                    eficiencia.EficienciaPct, eficiencia.Tramo, eficiencia.ProduccionReal,
                    eficiencia.TiempoEfectivoMarchaMin, eficiencia.RitmoTeoricoHora,
                    eficiencia.ParosAcumuladosMin, eficiencia.UltimaActualizacionProduccion);

                return Results.Ok(new JornadaHistoricoDetalle(jornada, paros, desperdicio, eficienciaRespuesta));
            })
            .RequireAuthorization(p => p.RequireRole("coordinador"));

        return app;
    }
}

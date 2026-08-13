using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Historico;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Historico;

/// <summary>
/// Invoca <c>sp_CalcularEficiencia</c> vía Dapper — mismo mecanismo que
/// <c>ServicioAsignacion</c> (E6.7/E6.8): abre con
/// <c>db.Database.OpenConnectionAsync()</c>, nunca con
/// <c>GetDbConnection().OpenAsync()</c> directo, para no saltarse el
/// pipeline de EF y con él <c>SessionContextConnectionInterceptor</c>
/// (04 §6.3) — <c>JornadaLinea</c> lleva RLS.
/// </summary>
public class ServicioHistorico(SmartAssignDbContext db) : IServicioHistorico
{
    public async Task<ResultadoEficienciaHistorica> CalcularEficienciaAsync(int jornadaLineaId, CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

        var parametros = new DynamicParameters();
        parametros.Add("jornada_linea_id", jornadaLineaId);
        parametros.Add("eficiencia_pct", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parametros.Add("tramo", dbType: DbType.String, size: 10, direction: ParameterDirection.Output);
        parametros.Add("produccion_real", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parametros.Add("tiempo_efectivo_marcha_min", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("ritmo_teorico_hora", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parametros.Add("ultima_actualizacion_produccion", dbType: DbType.DateTime2, direction: ParameterDirection.Output);
        parametros.Add("paros_acumulados_min", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        parametros.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_CalcularEficiencia", parametros, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoEficienciaHistorica(
            parametros.Get<decimal?>("eficiencia_pct"),
            parametros.Get<string?>("tramo"),
            parametros.Get<decimal?>("produccion_real") ?? 0m,
            parametros.Get<int?>("tiempo_efectivo_marcha_min") ?? 0,
            parametros.Get<decimal?>("ritmo_teorico_hora"),
            parametros.Get<int?>("paros_acumulados_min") ?? 0,
            parametros.Get<DateTime?>("ultima_actualizacion_produccion"),
            parametros.Get<string?>("codigo_rechazo"),
            parametros.Get<string?>("mensaje"));
    }
}

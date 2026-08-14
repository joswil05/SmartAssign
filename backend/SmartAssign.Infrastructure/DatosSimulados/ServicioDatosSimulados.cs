using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.DatosSimulados;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.DatosSimulados;

/// <summary>
/// Invoca <c>sp_VerificarSinDatosSimulados</c> / <c>sp_PurgarDatosSimulados</c>
/// vía Dapper, con el mismo cuidado que <c>ServicioHistorico</c>: abre con
/// <c>db.Database.OpenConnectionAsync()</c> y nunca con
/// <c>GetDbConnection().OpenAsync()</c>, para no saltarse el pipeline de EF
/// y con él <c>SessionContextConnectionInterceptor</c> (04 §6.3). Aquí
/// importa doblemente — <c>Puesto</c> lleva RLS, y sin SESSION_CONTEXT
/// ambos procedimientos rechazan con <c>ALCANCE_INSUFICIENTE</c> antes que
/// devolver un recuento que no vio media base.
/// </summary>
public class ServicioDatosSimulados(SmartAssignDbContext db) : IServicioDatosSimulados
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoVerificacionSimulados> VerificarAsync(CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

        var parametros = new DynamicParameters();
        parametros.Add("filas_simuladas", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("filas_placeholder", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("detalle", dbType: DbType.String, size: -1, direction: ParameterDirection.Output);
        parametros.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        parametros.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_VerificarSinDatosSimulados", parametros,
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoVerificacionSimulados(
            parametros.Get<int?>("filas_simuladas") ?? 0,
            parametros.Get<int?>("filas_placeholder") ?? 0,
            Deserializar<ConteoPorTabla>(parametros.Get<string?>("detalle")),
            parametros.Get<string?>("codigo_rechazo"),
            parametros.Get<string?>("mensaje"));
    }

    public async Task<ResultadoPurgaSimulados> PurgarAsync(CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

        var parametros = new DynamicParameters();
        parametros.Add("filas_purgadas", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("detalle", dbType: DbType.String, size: -1, direction: ParameterDirection.Output);
        parametros.Add("bloqueos", dbType: DbType.String, size: -1, direction: ParameterDirection.Output);
        parametros.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        parametros.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_PurgarDatosSimulados", parametros,
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoPurgaSimulados(
            parametros.Get<int?>("filas_purgadas") ?? 0,
            Deserializar<ConteoPorTabla>(parametros.Get<string?>("detalle")),
            Deserializar<BloqueoDePurga>(parametros.Get<string?>("bloqueos")),
            parametros.Get<string?>("codigo_rechazo"),
            parametros.Get<string?>("mensaje"));
    }

    private static IReadOnlyList<T> Deserializar<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<T>>(json, Json) ?? [];
}

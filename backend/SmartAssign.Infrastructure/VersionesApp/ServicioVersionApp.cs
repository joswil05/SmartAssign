using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.VersionesApp;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.VersionesApp;

/// <summary>
/// Invoca <c>sp_PublicarVersionApp</c> vía Dapper — mismo mecanismo que
/// <c>ServicioAsignacion</c>/<c>ServicioHistorico</c>: abre con
/// <c>db.Database.OpenConnectionAsync()</c>, nunca <c>GetDbConnection().OpenAsync()</c>
/// directo. <c>VersionApp</c> no lleva RLS, pero se mantiene el mismo
/// patrón por consistencia con el resto de servicios de la Api.
/// </summary>
public class ServicioVersionApp(SmartAssignDbContext db) : IServicioVersionApp
{
    public async Task<ResultadoPublicarVersion> PublicarVersionAsync(
        string versionNombre, int versionCodigo, string rutaApk, int versionMinimaApi, string? notas,
        CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

        var parametros = new DynamicParameters();
        parametros.Add("version_nombre", versionNombre);
        parametros.Add("version_codigo", versionCodigo);
        parametros.Add("ruta_apk", rutaApk);
        parametros.Add("version_minima_api", versionMinimaApi);
        parametros.Add("notas", notas);
        parametros.Add("version_app_id", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        parametros.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_PublicarVersionApp", parametros, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoPublicarVersion(
            parametros.Get<int?>("version_app_id"),
            parametros.Get<string?>("codigo_rechazo"),
            parametros.Get<string?>("mensaje"));
    }
}

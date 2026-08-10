using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Asignaciones;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Asignaciones;

/// <summary>
/// Invoca <c>sp_SugerirPuesto</c> (E6.7) y <c>sp_AsignarPersona</c> (E6.8)
/// vía Dapper — mismo mecanismo que ya usa <c>RegistradorAuditoria</c>
/// (E2.5) para llamar procedimientos con parámetros OUTPUT sin traer un
/// segundo ORM.
///
/// Abre la conexión con <c>db.Database.OpenConnectionAsync()</c>, nunca
/// con <c>GetDbConnection().OpenAsync()</c> directo: esto último evita
/// el pipeline de EF y con él <c>SessionContextConnectionInterceptor</c>
/// (04 §6.3) — sin <c>SESSION_CONTEXT</c> fijado, la RLS de
/// <c>JornadaLinea</c>/<c>Puesto</c> filtra todas las filas para
/// cualquiera que no sea coordinador. Bug real encontrado en esta UT:
/// <c>RegistradorAuditoria</c> (E2.5) tenía el mismo patrón, invisible
/// hasta ahora porque <c>Auditoria</c> no tiene RLS de fila.
/// </summary>
public class ServicioAsignacion(SmartAssignDbContext db) : IServicioAsignacion
{
    public async Task<ResultadoSugerencia> SugerirPuestoAsync(int personalId, byte lineaId, int usuarioId, CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

        var parametros = new DynamicParameters();
        parametros.Add("personal_id", personalId);
        parametros.Add("linea_id", lineaId);
        parametros.Add("usuario_id", usuarioId);
        parametros.Add("puesto_id_sugerido", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("nivel", dbType: DbType.Byte, direction: ParameterDirection.Output);
        parametros.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        parametros.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_SugerirPuesto", parametros, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoSugerencia(
            parametros.Get<int?>("puesto_id_sugerido"),
            parametros.Get<byte?>("nivel"),
            parametros.Get<string?>("codigo_rechazo"),
            parametros.Get<string?>("mensaje"));
    }

    public async Task<ResultadoAsignacion> AsignarPersonaAsync(
        int personalId, int puestoId, int usuarioId, Guid idempotencyKey, bool cederPerfil, CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

        // §7.3 exige jornada_linea_id, pero quien llama (la Api) solo
        // conoce el puesto — se resuelve aquí la jornada abierta de esa
        // línea. Sin ninguna, no tiene sentido intentar la asignación:
        // se rechaza antes de tocar el SP, con el mismo vocabulario de
        // código que el resto del motor.
        var jornadaLineaId = await conexion.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP (1) jl.Id FROM JornadaLinea jl
              JOIN Puesto p ON p.linea_id = jl.linea_id
             WHERE p.Id = @puestoId AND jl.cerrado_en IS NULL
            """,
            new { puestoId }, cancellationToken: ct));

        if (jornadaLineaId is null)
            return new ResultadoAsignacion(null, "SIN_JORNADA_ABIERTA", "No hay una jornada abierta para la línea de este puesto.");

        var parametros = new DynamicParameters();
        parametros.Add("personal_id", personalId);
        parametros.Add("puesto_id", puestoId);
        parametros.Add("usuario_id", usuarioId);
        parametros.Add("jornada_linea_id", jornadaLineaId);
        parametros.Add("origen", "manual_supervisor");
        parametros.Add("idempotency_key", idempotencyKey);
        parametros.Add("ceder_perfil", cederPerfil);
        parametros.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        parametros.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);
        parametros.Add("asignacion_id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_AsignarPersona", parametros, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoAsignacion(
            parametros.Get<long?>("asignacion_id"),
            parametros.Get<string?>("codigo_rechazo"),
            parametros.Get<string?>("mensaje"));
    }
}

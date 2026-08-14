using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.CicloDeTurno;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.CicloDeTurno;

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>. Invoca los cuatro
/// procedimientos del ciclo diario vía Dapper, con el mismo cuidado que
/// <c>ServicioAsignacion</c>: abre con
/// <c>db.Database.OpenConnectionAsync()</c> y nunca con
/// <c>GetDbConnection().OpenAsync()</c> directo, porque lo segundo se salta
/// el pipeline de EF y con él <c>SessionContextConnectionInterceptor</c>
/// (04 §6.3) — y los cuatro leen <c>JornadaLinea</c> y <c>Puesto</c>, que
/// llevan RLS.
/// </summary>
public class ServicioCicloDeTurno(SmartAssignDbContext db) : IServicioCicloDeTurno
{
    private async Task<IDbConnection> AbrirAsync(CancellationToken ct)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);
        return conexion;
    }

    public async Task<ResultadoPlanificacion> PlanificarLineaAsync(
        byte lineaId, byte turnoId, DateOnly diaOperacion, int? skuId, int? supervisorId,
        int usuarioId, CancellationToken ct = default)
    {
        var conexion = await AbrirAsync(ct);

        var p = new DynamicParameters();
        p.Add("linea_id", lineaId);
        p.Add("turno_id", turnoId);
        p.Add("dia_operacion", diaOperacion.ToDateTime(TimeOnly.MinValue), DbType.Date);
        p.Add("sku_id", skuId);
        p.Add("supervisor_id", supervisorId);
        p.Add("usuario_id", usuarioId);
        p.Add("jornada_linea_id", dbType: DbType.Int32, direction: ParameterDirection.Output);
        p.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_PlanificarLinea", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoPlanificacion(p.Get<int?>("jornada_linea_id"), p.Get<string?>("codigo_rechazo"));
    }

    public async Task<ResultadoConfirmacion> ConfirmarAsync(
        byte turnoId, DateOnly diaOperacion, int usuarioId, CancellationToken ct = default)
    {
        var conexion = await AbrirAsync(ct);

        var p = new DynamicParameters();
        p.Add("turno_id", turnoId);
        p.Add("dia_operacion", diaOperacion.ToDateTime(TimeOnly.MinValue), DbType.Date);
        p.Add("usuario_id", usuarioId);
        p.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        p.Add("lineas_sin_supervisor", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_ConfirmarPlanificacion", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoConfirmacion(
            p.Get<string?>("codigo_rechazo"), p.Get<string?>("lineas_sin_supervisor"));
    }

    public async Task<ResultadoArranque> ArrancarAsync(
        byte turnoId, DateOnly diaOperacion, int usuarioId, CancellationToken ct = default)
    {
        var conexion = await AbrirAsync(ct);

        var p = new DynamicParameters();
        p.Add("turno_id", turnoId);
        p.Add("dia_operacion", diaOperacion.ToDateTime(TimeOnly.MinValue), DbType.Date);
        p.Add("usuario_id", usuarioId);
        p.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_ArrancarTurno", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoArranque(p.Get<string?>("codigo_rechazo"));
    }

    public async Task<ResultadoCierre> CerrarTurnoAsync(
        int jornadaLineaId, int usuarioId, short? justificacionMotivoId, string? justificacionTexto,
        CancellationToken ct = default)
    {
        var conexion = await AbrirAsync(ct);

        var p = new DynamicParameters();
        p.Add("jornada_linea_id", jornadaLineaId);
        p.Add("usuario_id", usuarioId);
        p.Add("justificacion_motivo_id", justificacionMotivoId);
        p.Add("justificacion_texto", justificacionTexto);
        p.Add("bloqueos", dbType: DbType.String, size: -1, direction: ParameterDirection.Output);
        p.Add("codigo_rechazo", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        p.Add("mensaje", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            "dbo.sp_CerrarTurno", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return new ResultadoCierre(
            p.Get<string?>("codigo_rechazo"), p.Get<string?>("mensaje"), p.Get<string?>("bloqueos"));
    }
}

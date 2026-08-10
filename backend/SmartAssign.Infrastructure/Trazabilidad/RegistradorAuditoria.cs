using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Trazabilidad;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Trazabilidad;

/// <summary>
/// Único camino de escritura hacia Auditoria: llama a
/// dbo.sp_RegistrarAuditoria (04 §7.4) en vez de un INSERT directo desde
/// EF Core, para que la traza pase siempre por el mismo procedimiento
/// tanto si la llama la API como si algún día la llama otro cliente.
/// </summary>
public class RegistradorAuditoria(SmartAssignDbContext db) : IRegistradorAuditoria
{
    public async Task RegistrarAsync(
        int usuarioId, string rol, string accion, string entidad, long? entidadId,
        int? personalId, byte? lineaId, string resultado, string? codigoRechazo,
        string? deviceId, CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open)
            // db.Database.OpenConnectionAsync(), no conexion.OpenAsync()
            // directo: este último evita el pipeline de EF y con él
            // SessionContextConnectionInterceptor (04 §6.3) — inofensivo
            // aquí porque Auditoria no tiene RLS de fila, pero es el
            // mismo defecto real que sí rompía sp_SugerirPuesto/
            // sp_AsignarPersona en E6.8 (ServicioAsignacion).
            await db.Database.OpenConnectionAsync(ct);

        var parametros = new DynamicParameters();
        parametros.Add("usuario_id", usuarioId);
        parametros.Add("rol", rol);
        parametros.Add("accion", accion);
        parametros.Add("entidad", entidad);
        parametros.Add("entidad_id", entidadId);
        parametros.Add("personal_id", personalId);
        parametros.Add("linea_id", lineaId);
        parametros.Add("resultado", resultado);
        parametros.Add("codigo_rechazo", codigoRechazo);
        parametros.Add("datos_antes", (string?)null);
        parametros.Add("datos_despues", (string?)null);
        parametros.Add("justificacion_id", (long?)null);
        parametros.Add("device_id", deviceId);

        var comando = new CommandDefinition(
            "dbo.sp_RegistrarAuditoria",
            parametros,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        await conexion.ExecuteAsync(comando);
    }
}

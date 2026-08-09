using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartAssign.Application.Seguridad;

namespace SmartAssign.Infrastructure.Seguridad;

/// <summary>
/// Capa 3 del aislamiento (04 §6.3): en cuanto EF Core abre la conexión
/// física, fija <c>SESSION_CONTEXT('rol')</c> y <c>SESSION_CONTEXT('linea_id')</c>
/// para que la RLS de SQL Server los use. Se ejecuta en cada apertura —
/// no una sola vez por proceso — porque el *pooling* de conexiones puede
/// reutilizar una conexión física entre peticiones de usuarios distintos;
/// fiarse de que el contexto "ya estaba puesto" filtraría datos médicos
/// de la petición anterior.
///
/// Si <see cref="IContextoSesionActual.Rol"/> es nulo (migraciones,
/// semillas, pruebas sin autenticación) no se toca el contexto: por
/// diseño, sin contexto la política de seguridad no deja ver ninguna
/// fila — es el comportamiento "cierra en falso" que exige §2.2.
/// </summary>
public class SessionContextConnectionInterceptor(IContextoSesionActual contexto) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await AplicarAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        AplicarAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    private async Task AplicarAsync(DbConnection connection, CancellationToken ct)
    {
        if (contexto.Rol is null) return;

        await using var cmdRol = connection.CreateCommand();
        cmdRol.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = @p_rol, @read_only = 1;";
        var pRol = cmdRol.CreateParameter();
        pRol.ParameterName = "@p_rol";
        pRol.Value = contexto.Rol;
        cmdRol.Parameters.Add(pRol);
        await cmdRol.ExecuteNonQueryAsync(ct);

        await using var cmdLinea = connection.CreateCommand();
        cmdLinea.CommandText = "EXEC sys.sp_set_session_context @key = N'linea_id', @value = @p_linea, @read_only = 1;";
        var pLinea = cmdLinea.CreateParameter();
        pLinea.ParameterName = "@p_linea";
        pLinea.Value = (object?)contexto.LineaId ?? DBNull.Value;
        cmdLinea.Parameters.Add(pLinea);
        await cmdLinea.ExecuteNonQueryAsync(ct);
    }
}

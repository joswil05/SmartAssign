using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Procedimientos;

/// <summary>
/// Un parámetro de salida que se quiere leer tras ejecutar el procedimiento.
/// </summary>
public record Salida(string Nombre, DbType Tipo, int? Tamano = null)
{
    public static Salida Entero(string nombre) => new(nombre, DbType.Int32);
    public static Salida Largo(string nombre) => new(nombre, DbType.Int64);
    public static Salida Byte(string nombre) => new(nombre, DbType.Byte);
    public static Salida Bit(string nombre) => new(nombre, DbType.Boolean);
    public static Salida Texto(string nombre, int tamano = 400) => new(nombre, DbType.String, tamano);

    /// <summary>Los dos que devuelve casi todo procedimiento de este esquema.</summary>
    public static Salida[] Rechazo => [Texto("codigo_rechazo", 40), Texto("mensaje", 400)];
}

/// <summary>
/// Invoca un procedimiento con parámetros OUTPUT y devuelve lo que salió.
///
/// <b>Existe por una razón concreta, no por ahorrar líneas.</b> La conexión
/// tiene que abrirse con <c>db.Database.OpenConnectionAsync()</c> y nunca
/// con <c>GetDbConnection().OpenAsync()</c> directo: lo segundo se salta el
/// pipeline de EF y con él <c>SessionContextConnectionInterceptor</c>
/// (04 §6.3), así que la RLS de <c>Puesto</c>/<c>JornadaLinea</c> esconde
/// las filas y el procedimiento trabaja sobre una base vacía sin dar ningún
/// error. Ese fallo ya apareció tres veces en este proyecto —E6.8 en
/// <c>RegistradorAuditoria</c>, E14.4 midiendo <c>sp_CalcularEficiencia</c>,
/// y la revisión de producción en el barrido del motor—. Con un solo sitio
/// que abra conexiones, no puede volver a aparecer en un cuarto.
/// </summary>
public class EjecutorDeProcedimiento(SmartAssignDbContext db)
{
    public async Task<IReadOnlyDictionary<string, object?>> EjecutarAsync(
        string procedimiento,
        IReadOnlyDictionary<string, object?> entradas,
        IReadOnlyList<Salida> salidas,
        CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

        var parametros = new DynamicParameters();
        foreach (var (nombre, valor) in entradas) parametros.Add(nombre, valor);
        foreach (var salida in salidas)
            parametros.Add(salida.Nombre, dbType: salida.Tipo, size: salida.Tamano,
                direction: ParameterDirection.Output);

        await conexion.ExecuteAsync(new CommandDefinition(
            procedimiento, parametros, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return salidas.ToDictionary(s => s.Nombre, s => parametros.Get<object?>(s.Nombre));
    }
}

/// <summary>Lecturas cómodas del diccionario de salidas, sin castings sueltos por todo el código.</summary>
public static class SalidasExtensiones
{
    public static int? Entero(this IReadOnlyDictionary<string, object?> s, string nombre) =>
        s.TryGetValue(nombre, out var v) && v is not null ? Convert.ToInt32(v) : null;

    public static long? Largo(this IReadOnlyDictionary<string, object?> s, string nombre) =>
        s.TryGetValue(nombre, out var v) && v is not null ? Convert.ToInt64(v) : null;

    public static byte? Byte(this IReadOnlyDictionary<string, object?> s, string nombre) =>
        s.TryGetValue(nombre, out var v) && v is not null ? Convert.ToByte(v) : null;

    public static bool Bit(this IReadOnlyDictionary<string, object?> s, string nombre) =>
        s.TryGetValue(nombre, out var v) && v is not null && Convert.ToBoolean(v);

    public static string? Texto(this IReadOnlyDictionary<string, object?> s, string nombre) =>
        s.TryGetValue(nombre, out var v) ? v as string : null;
}

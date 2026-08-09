using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Importacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Importacion;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Semillas;

// Herramientas de un solo uso, nunca parte de la Api ni de una migración:
//
//   importar <ruta.xlsx> [conexion]   — carga inicial de datos reales (07 §4.3, UT-E3.6)
//   sembrar-adversaria [conexion]     — los 16 escenarios (07 §4.4, UT-E3.5). SOLO dev/test — 00 §G2.

const string ConexionPorDefecto = "Server=(localdb)\\MSSQLLocalDB;Database=SmartAssignDev;Trusted_Connection=True;TrustServerCertificate=True;";

if (args.Length < 1)
{
    Console.Error.WriteLine("Uso:");
    Console.Error.WriteLine("  ImportadorCli importar <ruta-al-excel> [cadena-de-conexion]");
    Console.Error.WriteLine("  ImportadorCli sembrar-adversaria [cadena-de-conexion]");
    return 1;
}

return args[0] switch
{
    "importar" => await EjecutarImportacionAsync(args),
    "sembrar-adversaria" => await EjecutarSiembraAdversariaAsync(args),
    var otro => Fallar($"Comando desconocido: '{otro}'"),
};

static int Fallar(string mensaje)
{
    Console.Error.WriteLine(mensaje);
    return 1;
}

static async Task<SmartAssignDbContext> AbrirConContextoCoordinadorAsync(string cadenaConexion)
{
    var opciones = new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(cadenaConexion).Options;
    var db = new SmartAssignDbContext(opciones);

    // Puesto tiene RLS (04 §6.3) — sin este contexto, ni la propia
    // herramienta vería las filas que acaba de escribir.
    var conexion = db.Database.GetDbConnection();
    await conexion.OpenAsync();
    await using var cmd = conexion.CreateCommand();
    cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
    await cmd.ExecuteNonQueryAsync();

    return db;
}

async Task<int> EjecutarImportacionAsync(string[] argumentos)
{
    if (argumentos.Length < 2) return Fallar("Uso: ImportadorCli importar <ruta-al-excel> [cadena-de-conexion]");

    var rutaExcel = argumentos[1];
    if (!File.Exists(rutaExcel)) return Fallar($"No se encontró el archivo: {rutaExcel}");

    var cadenaConexion = argumentos.Length > 2 ? argumentos[2] : ConexionPorDefecto;
    await using var db = await AbrirConContextoCoordinadorAsync(cadenaConexion);

    // AusenciaJustificada.registrado_por exige un Usuario real. Se usa (o
    // se crea) una cuenta administrativa dedicada a la carga inicial —
    // no es un usuario operativo, no tiene credenciales activas.
    var usuarioCarga = await db.Usuarios.SingleOrDefaultAsync(u => u.Username == "carga_inicial_datos");
    if (usuarioCarga is null)
    {
        usuarioCarga = new Usuario
        {
            Username = "carga_inicial_datos",
            NombreCompleto = "Carga inicial de datos reales (E3.6)",
            Rol = "coordinador",
            OrigenIdentidad = "local",
            Activo = false,
        };
        db.Usuarios.Add(usuarioCarga);
        await db.SaveChangesAsync();
    }

    var importador = new ImportadorDatosReales(db);
    var huboErrores = false;

    await using (var archivo = File.OpenRead(rutaExcel))
        huboErrores |= Reportar("Personal", await importador.ImportarPersonalAsync(archivo));

    await using (var archivo = File.OpenRead(rutaExcel))
        huboErrores |= Reportar("Puestos Fijos", await importador.ImportarPuestosFijosAsync(archivo));

    await using (var archivo = File.OpenRead(rutaExcel))
        huboErrores |= Reportar("Personal ausente", await importador.ImportarAusenciasAsync(archivo, usuarioCarga.Id));

    return huboErrores ? 1 : 0;
}

async Task<int> EjecutarSiembraAdversariaAsync(string[] argumentos)
{
    var cadenaConexion = argumentos.Length > 1 ? argumentos[1] : ConexionPorDefecto;

    if (cadenaConexion.Contains("Prod", StringComparison.OrdinalIgnoreCase))
        return Fallar("Cadena de conexión sospechosa de apuntar a producción — abortado (00 §G2: nunca a producción).");

    await using var db = await AbrirConContextoCoordinadorAsync(cadenaConexion);

    var usuarioSemilla = await db.Usuarios.SingleOrDefaultAsync(u => u.Username == "semilla_adversaria");
    if (usuarioSemilla is null)
    {
        usuarioSemilla = new Usuario
        {
            Username = "semilla_adversaria",
            NombreCompleto = "Semilla adversaria de desarrollo (E3.5)",
            Rol = "coordinador",
            OrigenIdentidad = "local",
            Activo = false,
        };
        db.Usuarios.Add(usuarioSemilla);
        await db.SaveChangesAsync();
    }

    var sembrador = new SembradorAdversario(db);
    var informe = await sembrador.SembrarAsync(usuarioSemilla.Id);

    Console.WriteLine("=== Semilla adversaria (07 §4.4) ===");
    Console.WriteLine(informe);
    return 0;
}

static bool Reportar(string hoja, ResultadoImportacion resultado)
{
    Console.WriteLine($"=== {hoja} ===");
    Console.WriteLine($"Filas leídas: {resultado.FilasLeidas}");
    if (resultado.Exitoso)
    {
        Console.WriteLine($"OK — {resultado.FilasImportadas} filas importadas/actualizadas.");
        Console.WriteLine();
        return false;
    }

    Console.WriteLine($"RECHAZADO — {resultado.Errores.Count} error(es). No se escribió ninguna fila de esta hoja.");
    foreach (var error in resultado.Errores)
        Console.WriteLine($"  Fila {error.Fila} [{error.Columna}]: {error.Mensaje}");
    Console.WriteLine();
    return true;
}

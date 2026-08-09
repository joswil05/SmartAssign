using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Importacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Importacion;

/// <summary>
/// Lee "Base de Datos.xlsx" (07 §4.1). Todo o nada por hoja: acumula
/// todos los errores de todas las filas antes de decidir — así el
/// informe describe el archivo completo, no solo la primera fila mala.
/// Si hay un solo error, no se escribe nada (§4.3, UT-E3.6).
/// </summary>
public class ImportadorDatosReales(SmartAssignDbContext db) : IImportadorDatosReales
{
    // 00 §G1 — columna real "Perfil" de la hoja Personal (que pese al
    // nombre es la categoría, no una preferencia). OPERADOR DE CALDERAS
    // y OPERARIO DE FILTROS Y TANQUERIA → Operador A, confirmado por el
    // cliente ("cuéntalos como operadores", 2026-08-09).
    private static readonly Dictionary<string, string> MapaCategoria = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OPERARIO"] = "operario",
        ["OPERADOR DE EQUIPOS"] = "operador_a",
        ["OPERADOR DE CALDERAS"] = "operador_a",
        ["OPERARIO DE FILTROS Y TANQUERIA"] = "operador_a",
        ["OPERARIO DE CONTROL DE AVERIAS"] = "averiero",
        ["SUPERVISOR DE LINEA"] = "liderazgo",
        ["AUXILIAR DE CONTROL DE MATERIALES"] = "liderazgo",
        ["ASISTENTE ADMINISTRATIVO"] = "liderazgo",
        ["COORDINADOR LINEAS DE ENVASADO"] = "liderazgo",
        ["COORDINADOR DE MATERIALES DE PRODUCCION"] = "liderazgo",
        ["ANALISTA DE PROCESOS"] = "liderazgo",
        ["JEFE DE EMBOTELLADO"] = "liderazgo",
    };

    // 00 §G3 — "Asignación Prspto": las 5 líneas reales + los 4 centros
    // de coste que NO son de las 10 líneas (quedan sin línea habitual).
    private static readonly Dictionary<string, byte?> MapaLineaHabitual = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LINEA 1"] = 1,
        ["LINEA 2"] = 2,
        ["LINEA 4"] = 4,
        ["LINEA 6"] = 6,
        ["LINEA 8"] = 8,
        ["MAQUILA"] = null,
        ["PET"] = null,
        ["1605"] = null,
        ["1606"] = null,
    };

    private static readonly HashSet<string> SexoPreferenteValido =
        new(StringComparer.OrdinalIgnoreCase) { "Indistinto", "Masculino", "Femenino" };

    // 00 §G6 — "Femenina" es error de tipeo por "Femenino" (confirmado
    // por el cliente, 2026-08-09: "la diferencia... no existe").
    private static readonly Dictionary<string, string> SinonimoSexoPreferente =
        new(StringComparer.OrdinalIgnoreCase) { ["Femenina"] = "Femenino" };

    // 00 §G6 — 8 filas de la Línea 6 tienen el valor de PerfilRequerido
    // arrastrado por error a la columna SexoPreferente (mismo patrón que
    // A13 ya detectaba como error, pero antes se rechazaba en vez de
    // repararse). Se repara por coincidencia exacta: en el resto del
    // archivo (91 filas limpias), cada uno de estos PerfilRequerido
    // tiene siempre el mismo SexoPreferente, sin una sola excepción
    // (Operador→Masculino en 15/15, Averiero→Masculino en 5/5,
    // Supervisor→Indistinto en 6/6, puestos "Estibador *"→Masculino en
    // 9/9). Confirmado por el cliente que el resto de la tabla ya
    // describe puestos y perfil técnico reales, no un error de captura
    // adicional — solo faltaba esta corrección puntual.
    private static readonly Dictionary<string, string> ReparacionSexoPreferentePorArrastre =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Supervisor"] = "Indistinto",
            ["Operador"] = "Masculino",
            ["Averiero"] = "Masculino",
            ["Estibador"] = "Masculino",
        };

    // Hoja "Personal ausente", columna CodSalida → AusenciaJustificada.Tipo
    // (04 §3.3, valores fijos del CHECK). "Emergencia" y "Consulta" no
    // tienen equivalente literal — inferencia propia, ver docs/PROGRESO.md.
    private static readonly Dictionary<string, string> MapaTipoAusencia = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Vacaciones"] = "vacaciones",
        ["Permiso"] = "permiso",
        ["Subsidio"] = "subsidio",
        ["Consulta"] = "cita_medica",
        ["Emergencia"] = "otro",
    };

    public async Task<ResultadoImportacion> ImportarPersonalAsync(Stream archivoExcel, CancellationToken ct = default)
    {
        using var libro = new XLWorkbook(archivoExcel);
        var hoja = libro.Worksheet("Personal");

        var errores = new List<ErrorImportacion>();
        var candidatos = new List<Personal>();
        var fichasVistas = new HashSet<string>();
        var filasLeidas = 0;

        foreach (var fila in hoja.RowsUsed().Skip(1))
        {
            var numeroFila = fila.RowNumber();
            var ficha = fila.Cell(1).GetString().Trim();
            if (ficha.Length == 0) continue; // fila en blanco al final de la hoja
            filasLeidas++;

            var nombre = fila.Cell(2).GetString().Trim();
            var sexoCrudo = fila.Cell(3).GetString().Trim();
            var perfilCrudo = fila.Cell(4).GetString().Trim();
            var asignacionCrudo = fila.Cell(8).GetString().Trim();
            var activo = fila.Cell(10).GetBoolean();

            if (!fichasVistas.Add(ficha))
                errores.Add(new ErrorImportacion(numeroFila, "CodEmpleado", $"Ficha duplicada en el archivo: {ficha}"));

            if (nombre.Length == 0)
                errores.Add(new ErrorImportacion(numeroFila, "Nombre Completo", "Nombre vacío"));

            string? sexo = null;
            if (string.Equals(sexoCrudo, "MASCULINO", StringComparison.OrdinalIgnoreCase)) sexo = "masculino";
            else if (string.Equals(sexoCrudo, "FEMENINO", StringComparison.OrdinalIgnoreCase)) sexo = "femenino";
            else if (sexoCrudo.Length > 0)
                errores.Add(new ErrorImportacion(numeroFila, "Sexo", $"Valor de sexo desconocido: '{sexoCrudo}'"));

            if (!MapaCategoria.TryGetValue(perfilCrudo, out var categoria))
            {
                errores.Add(new ErrorImportacion(numeroFila, "Perfil", $"Categoría desconocida, no está en el mapeo de 00 §G1: '{perfilCrudo}'"));
                categoria = null;
            }

            byte? lineaHabitual = null;
            if (!MapaLineaHabitual.TryGetValue(asignacionCrudo, out lineaHabitual) && asignacionCrudo.Length > 0)
                errores.Add(new ErrorImportacion(numeroFila, "Asignación Prspto", $"Asignación de presupuesto desconocida: '{asignacionCrudo}'"));

            if (categoria is not null)
            {
                candidatos.Add(new Personal
                {
                    Ficha = ficha,
                    NombreCompleto = nombre,
                    Categoria = categoria,
                    Sexo = sexo,
                    LineaHabitual = lineaHabitual,
                    Activo = activo,
                });
            }
        }

        if (errores.Count > 0)
            return ResultadoImportacion.Rechazo(filasLeidas, errores);

        var importados = 0;
        foreach (var candidato in candidatos)
        {
            var existente = await db.Personas.SingleOrDefaultAsync(p => p.Ficha == candidato.Ficha, ct);
            if (existente is null)
            {
                db.Personas.Add(candidato);
            }
            else
            {
                existente.NombreCompleto = candidato.NombreCompleto;
                existente.Categoria = candidato.Categoria;
                existente.Sexo = candidato.Sexo;
                existente.LineaHabitual = candidato.LineaHabitual;
                existente.Activo = candidato.Activo;
            }
            importados++;
        }

        await db.SaveChangesAsync(ct);
        return ResultadoImportacion.Exito(filasLeidas, importados);
    }

    public async Task<ResultadoImportacion> ImportarPuestosFijosAsync(Stream archivoExcel, CancellationToken ct = default)
    {
        using var libro = new XLWorkbook(archivoExcel);
        var hoja = libro.Worksheet("Puestos Fijos");

        var errores = new List<ErrorImportacion>();
        var candidatos = new List<Puesto>();
        var filasLeidas = 0;

        foreach (var fila in hoja.RowsUsed().Skip(1))
        {
            var numeroFila = fila.RowNumber();
            var idPuesto = fila.Cell(1).GetString().Trim();
            if (idPuesto.Length == 0) continue;
            filasLeidas++;

            var nombrePuesto = fila.Cell(2).GetString().Trim();
            var sexoPreferenteCrudo = fila.Cell(3).GetString().Trim();
            var tiempoEnPuesto = LeerEnteroONulo(fila.Cell(4));
            var tiempoDeRecup = LeerEnteroONulo(fila.Cell(5));
            var perfilRequerido = fila.Cell(6).GetString().Trim();

            byte? lineaId = null;
            if (idPuesto.Length >= 3 && idPuesto[0] == 'L' && byte.TryParse(idPuesto.AsSpan(1, 2), out var numeroLinea) && numeroLinea is >= 1 and <= 10)
                lineaId = numeroLinea;
            else
                errores.Add(new ErrorImportacion(numeroFila, "IdPuesto", $"No se pudo derivar la línea de '{idPuesto}' (se esperaba 'L0N...')"));

            if (nombrePuesto.Length == 0)
                errores.Add(new ErrorImportacion(numeroFila, "NombrePuesto", "Nombre de puesto vacío"));

            var sexoPreferenteNormalizado = SinonimoSexoPreferente.GetValueOrDefault(sexoPreferenteCrudo, sexoPreferenteCrudo);

            string? sexoPreferente = null;
            if (SexoPreferenteValido.Contains(sexoPreferenteNormalizado))
                sexoPreferente = char.ToUpperInvariant(sexoPreferenteNormalizado[0]) + sexoPreferenteNormalizado[1..].ToLowerInvariant();
            else if (ReparacionSexoPreferentePorArrastre.TryGetValue(sexoPreferenteCrudo, out var reparado))
                sexoPreferente = reparado; // 00 §G6
            else if (sexoPreferenteCrudo.Length > 0)
                errores.Add(new ErrorImportacion(numeroFila, "SexoPreferente",
                    $"'{sexoPreferenteCrudo}' no es Indistinto/Masculino/Femenino — parece dato de PerfilRequerido mezclado en la columna equivocada (00 §A13)"));

            if (perfilRequerido.Length == 0)
                errores.Add(new ErrorImportacion(numeroFila, "PerfilRequerido", "PerfilRequerido vacío"));

            if (lineaId is not null && errores.Count(e => e.Fila == numeroFila) == 0)
            {
                candidatos.Add(new Puesto
                {
                    LineaId = lineaId.Value,
                    Codigo = idPuesto,
                    NombrePuesto = nombrePuesto,
                    // A12: sin TiempoEnPuesto → fijo; con valor → rotativo.
                    Tipo = tiempoEnPuesto is null ? "fijo" : "rotativo",
                    CategoriaTitular = null, // 00 §G5: no derivable de PerfilRequerido, no se inventa
                    PerfilRequerido = perfilRequerido,
                    SexoPreferente = sexoPreferente,
                    HorasEnPuesto = (short?)tiempoEnPuesto,
                    HorasRecuperacion = (short?)tiempoDeRecup,
                });
            }
        }

        if (errores.Count > 0)
            return ResultadoImportacion.Rechazo(filasLeidas, errores);

        var importados = 0;
        foreach (var candidato in candidatos)
        {
            var existente = await db.Puestos.SingleOrDefaultAsync(p => p.LineaId == candidato.LineaId && p.Codigo == candidato.Codigo, ct);
            if (existente is null)
            {
                db.Puestos.Add(candidato);
            }
            else
            {
                existente.NombrePuesto = candidato.NombrePuesto;
                existente.Tipo = candidato.Tipo;
                existente.PerfilRequerido = candidato.PerfilRequerido;
                existente.SexoPreferente = candidato.SexoPreferente;
                existente.HorasEnPuesto = candidato.HorasEnPuesto;
                existente.HorasRecuperacion = candidato.HorasRecuperacion;
            }
            importados++;
        }

        await db.SaveChangesAsync(ct);
        return ResultadoImportacion.Exito(filasLeidas, importados);
    }

    public async Task<ResultadoImportacion> ImportarAusenciasAsync(Stream archivoExcel, int usuarioId, CancellationToken ct = default)
    {
        using var libro = new XLWorkbook(archivoExcel);
        var hoja = libro.Worksheet("Personal ausente");

        var errores = new List<ErrorImportacion>();
        var candidatos = new List<(string Ficha, AusenciaJustificada Ausencia)>();
        var filasLeidas = 0;

        foreach (var fila in hoja.RowsUsed().Skip(1))
        {
            var numeroFila = fila.RowNumber();
            var ficha = fila.Cell(1).GetString().Trim();
            if (ficha.Length == 0) continue;
            filasLeidas++;

            var codSalida = fila.Cell(3).GetString().Trim();
            var fechaSalida = fila.Cell(4).GetDateTime();
            var fechaEntradaCelda = fila.Cell(6);
            DateOnly? fechaFin = fechaEntradaCelda.IsEmpty() ? null : DateOnly.FromDateTime(fechaEntradaCelda.GetDateTime());

            if (!MapaTipoAusencia.TryGetValue(codSalida, out var tipo))
            {
                errores.Add(new ErrorImportacion(numeroFila, "CodSalida", $"Motivo de ausencia desconocido: '{codSalida}'"));
                continue;
            }

            var personal = await db.Personas.SingleOrDefaultAsync(p => p.Ficha == ficha, ct);
            if (personal is null)
            {
                errores.Add(new ErrorImportacion(numeroFila, "CodEmpleado", $"Ficha {ficha} no está en Personal — importe Personal primero"));
                continue;
            }

            candidatos.Add((ficha, new AusenciaJustificada
            {
                PersonalId = personal.Id,
                Tipo = tipo,
                FechaInicio = DateOnly.FromDateTime(fechaSalida),
                FechaFin = fechaFin,
                RegistradoPor = usuarioId,
            }));
        }

        if (errores.Count > 0)
            return ResultadoImportacion.Rechazo(filasLeidas, errores);

        var importados = 0;
        foreach (var (_, ausencia) in candidatos)
        {
            var yaExiste = await db.AusenciasJustificadas.AnyAsync(a =>
                a.PersonalId == ausencia.PersonalId && a.Tipo == ausencia.Tipo && a.FechaInicio == ausencia.FechaInicio, ct);
            if (yaExiste) continue;

            db.AusenciasJustificadas.Add(ausencia);
            importados++;
        }

        await db.SaveChangesAsync(ct);
        return ResultadoImportacion.Exito(filasLeidas, importados);
    }

    private static int? LeerEnteroONulo(IXLCell celda) =>
        celda.IsEmpty() ? null : (int)celda.GetDouble();
}

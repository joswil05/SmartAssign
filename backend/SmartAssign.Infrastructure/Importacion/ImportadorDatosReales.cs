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

    // 00 §G5, resuelto por el cliente (2026-08-09): "por ahora tómalos a
    // todos esos como si fuese operador A, no hay operadores C ni B por
    // los momentos". "Averiero" es mapeo literal (mismo valor, sin
    // ambigüedad) — el resto (Operador/Genérico/Operador de filtro) son
    // los tres PerfilRequerido que el cliente confirmó tratar como
    // operador_a. "Supervisor" queda FUERA a propósito: es personal de
    // liderazgo, nunca se asigna automáticamente (§4.1) — sp_BarridoPuestosFijos
    // (E5.5) excluye esos puestos del barrido en vez de necesitar una categoría.
    private static readonly Dictionary<string, string> MapaCategoriaTitularPorPerfil =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Averiero"] = "averiero",
            ["Operador"] = "operador_a",
            ["Genérico"] = "operador_a",
            ["Operador de filtro"] = "operador_a",
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

    // 00 §A14/§B6: TipoActividad agrupa puestos que son "la misma tarea
    // desgastante" (B6) para fn_ViolaNoRepeticion24h. "Girar botellas" es
    // el ÚNICO caso que A14 confirma con dato real (las tres filas
    // "Girar botellas 1/2/3" comparten TiempoDeRecup=24) — no se extiende
    // esta agrupación a otros nombres de puesto por similitud (p. ej.
    // "Limpieza" tiene 5h en una línea y 48h en otras dos: agrupar por
    // nombre inventaría una equivalencia que el dato no respalda).
    private const string ActividadGirarBotellas = "Girar botellas";

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
                    // 00 §G5, resuelto: solo en fijos, mapeado del PerfilRequerido
                    // real (Averiero literal; Operador/Genérico/Operador de filtro
                    // → operador_a, confirmado por el cliente). "Supervisor" no
                    // está en el mapa: queda NULL a propósito.
                    CategoriaTitular = tiempoEnPuesto is null
                        ? MapaCategoriaTitularPorPerfil.GetValueOrDefault(perfilRequerido)
                        : null,
                    PerfilRequerido = perfilRequerido,
                    SexoPreferente = sexoPreferente,
                    HorasEnPuesto = (short?)tiempoEnPuesto,
                    HorasRecuperacion = (short?)tiempoDeRecup,
                });
            }
        }

        if (errores.Count > 0)
            return ResultadoImportacion.Rechazo(filasLeidas, errores);

        bool EsGirarBotellas(Puesto p) => p.NombrePuesto.StartsWith(ActividadGirarBotellas, StringComparison.OrdinalIgnoreCase);

        short? tipoActividadGirarBotellasId = null;
        if (candidatos.Exists(EsGirarBotellas))
        {
            var tipoActividad = await db.TiposActividad.SingleOrDefaultAsync(t => t.Nombre == ActividadGirarBotellas, ct);
            if (tipoActividad is null)
            {
                tipoActividad = new TipoActividad { Nombre = ActividadGirarBotellas };
                db.TiposActividad.Add(tipoActividad);
                await db.SaveChangesAsync(ct);
            }
            tipoActividadGirarBotellasId = tipoActividad.Id;
        }

        var importados = 0;
        foreach (var candidato in candidatos)
        {
            candidato.TipoActividadId = EsGirarBotellas(candidato) ? tipoActividadGirarBotellasId : null;

            var existente = await db.Puestos.SingleOrDefaultAsync(p => p.LineaId == candidato.LineaId && p.Codigo == candidato.Codigo, ct);
            if (existente is null)
            {
                db.Puestos.Add(candidato);
            }
            else
            {
                existente.NombrePuesto = candidato.NombrePuesto;
                existente.Tipo = candidato.Tipo;
                existente.CategoriaTitular = candidato.CategoriaTitular;
                existente.PerfilRequerido = candidato.PerfilRequerido;
                existente.SexoPreferente = candidato.SexoPreferente;
                existente.HorasEnPuesto = candidato.HorasEnPuesto;
                existente.HorasRecuperacion = candidato.HorasRecuperacion;
                existente.TipoActividadId = candidato.TipoActividadId;
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

    // 00 §G4: "Programa" es la única hoja limpia con SKU real — Item
    // (código), Producto (descripción) y Velocidad (ritmo teórico, §11.4).
    // Todo o nada: a diferencia de "Puestos SKU", esta hoja SÍ está
    // completa (22 filas reales, ninguna ambigua).
    public async Task<ResultadoImportacion> ImportarSkuAsync(Stream archivoExcel, CancellationToken ct = default)
    {
        using var libro = new XLWorkbook(archivoExcel);
        var hoja = libro.Worksheet("Programa");

        var errores = new List<ErrorImportacion>();
        var porCodigo = new Dictionary<string, Sku>(StringComparer.OrdinalIgnoreCase);
        var filasLeidas = 0;

        foreach (var fila in hoja.RowsUsed().Skip(1))
        {
            var numeroFila = fila.RowNumber();
            var item = fila.Cell(6).GetString().Trim();       // Item
            if (item.Length == 0) continue;
            filasLeidas++;

            var producto = fila.Cell(10).GetString().Trim();  // Producto
            var velocidadCelda = fila.Cell(12);                // Velocidad

            if (producto.Length == 0)
                errores.Add(new ErrorImportacion(numeroFila, "Producto", $"Producto vacío para el SKU '{item}'"));

            decimal? ritmo = null;
            if (velocidadCelda.IsEmpty() || velocidadCelda.GetDouble() <= 0)
                errores.Add(new ErrorImportacion(numeroFila, "Velocidad", $"Velocidad inválida para el SKU '{item}'"));
            else
                ritmo = (decimal)velocidadCelda.GetDouble();

            if (producto.Length == 0 || ritmo is null) continue;

            if (porCodigo.TryGetValue(item, out var yaVisto))
            {
                if (yaVisto.Descripcion != producto || yaVisto.RitmoTeoricoHora != ritmo)
                    errores.Add(new ErrorImportacion(numeroFila, "Item",
                        $"El SKU '{item}' aparece con Producto/Velocidad distintos en más de una fila"));
                continue;
            }

            porCodigo[item] = new Sku { Codigo = item, Descripcion = producto, RitmoTeoricoHora = ritmo.Value };
        }

        if (errores.Count > 0)
            return ResultadoImportacion.Rechazo(filasLeidas, errores);

        var importados = 0;
        foreach (var candidato in porCodigo.Values)
        {
            var existente = await db.Skus.SingleOrDefaultAsync(s => s.Codigo == candidato.Codigo, ct);
            if (existente is null)
            {
                db.Skus.Add(candidato);
            }
            else
            {
                existente.Descripcion = candidato.Descripcion;
                existente.RitmoTeoricoHora = candidato.RitmoTeoricoHora;
            }
            importados++;
        }

        await db.SaveChangesAsync(ct);
        return ResultadoImportacion.Exito(filasLeidas, importados);
    }

    // 00 §G4: "Puestos SKU" trae solo 18 filas y la mayoría incompletas —
    // decisión ya cerrada de NO rechazar el lote entero por eso (a
    // diferencia del resto de hojas). Solo Línea 1 (9 filas, 3 puestos ×
    // 3 SKU reales) tiene al menos un Item verificable contra el
    // catálogo; el resto (L02 sin Item, L04 con Item="Alcohol" que no es
    // un código real, L05 sin ningún dato) no genera ningún Puesto ni
    // enlace — un puesto sin un solo SKU verificado se leería como "no
    // depende del SKU" (justo lo contrario de lo que dice esta hoja).
    public async Task<ResultadoImportacion> ImportarPuestosSkuAsync(Stream archivoExcel, CancellationToken ct = default)
    {
        using var libro = new XLWorkbook(archivoExcel);
        var hoja = libro.Worksheet("Puestos SKU");

        var filasLeidas = 0;
        var grupos = new Dictionary<(byte LineaId, string IdPuesto), List<FilaPuestoSku>>();

        foreach (var fila in hoja.RowsUsed().Skip(1))
        {
            var idPuesto = fila.Cell(4).GetString().Trim();   // IdPuesto
            if (idPuesto.Length == 0) continue;
            filasLeidas++;

            // Mismo patrón que "Puestos Fijos": la línea se deriva del
            // prefijo del código ('L0N...'), no de la columna de texto.
            if (idPuesto.Length < 3 || idPuesto[0] != 'L' ||
                !byte.TryParse(idPuesto.AsSpan(1, 2), out var lineaId) || lineaId is < 1 or > 10)
                continue; // código no ubicable en una línea real — se omite

            var item = fila.Cell(2).GetString().Trim();
            var nombre = fila.Cell(5).GetString().Trim();
            var sexoPreferente = fila.Cell(6).GetString().Trim();
            var tiempoEnPuesto = LeerEnteroONulo(fila.Cell(7));
            var tiempoDeRecup = LeerEnteroONulo(fila.Cell(8));
            var perfilRequerido = fila.Cell(9).GetString().Trim();

            var clave = (lineaId, idPuesto);
            if (!grupos.TryGetValue(clave, out var filasGrupo))
                grupos[clave] = filasGrupo = [];

            filasGrupo.Add(new FilaPuestoSku(
                item.Length > 0 ? item : null,
                nombre.Length > 0 ? nombre : null,
                sexoPreferente.Length > 0 ? sexoPreferente : null,
                tiempoEnPuesto, tiempoDeRecup,
                perfilRequerido.Length > 0 ? perfilRequerido : null));
        }

        var enlacesImportados = 0;
        foreach (var ((lineaId, idPuesto), filas) in grupos)
        {
            // Sin ninguna fila con detalle real (nombre), no hay puesto
            // que crear — es exactamente el hueco que G4 ya documentó
            // (L02S001 y L05S00x llegan así: solo el código, nada más).
            var detalle = filas.FirstOrDefault(f => f.Nombre is not null);
            if (detalle.Nombre is null) continue;

            // Resuelve los SKU verificables ANTES de tocar Puesto: un
            // puesto sin ningún enlace real a PuestoSKU se leería como
            // "no depende del SKU, siempre disponible" (fn_PuestoFueraDeOperacion,
            // 04 §2.5) — exactamente lo contrario de lo que esta hoja
            // dice de él. Mejor no crearlo que crearlo mal caracterizado
            // (L04S001 "Sticker 1" con Item="Alcohol" cae aquí: tiene
            // nombre real, pero cero SKU verificable — se omite entero).
            var skuIds = new List<int>();
            foreach (var fila in filas)
            {
                if (fila.Item is null) continue;
                var sku = await db.Skus.SingleOrDefaultAsync(s => s.Codigo == fila.Item, ct);
                if (sku is not null) skuIds.Add(sku.Id);
            }
            if (skuIds.Count == 0) continue;

            var sexoNormalizado = detalle.SexoPreferente is { Length: > 0 } s
                ? char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant()
                : null;

            var puesto = await db.Puestos.SingleOrDefaultAsync(p => p.LineaId == lineaId && p.Codigo == idPuesto, ct);
            if (puesto is null)
            {
                puesto = new Puesto { LineaId = lineaId, Codigo = idPuesto };
                db.Puestos.Add(puesto);
            }

            puesto.NombrePuesto = detalle.Nombre;
            puesto.Tipo = "rotativo";       // trae TiempoEnPuesto (A12) — nunca fijo (C12)
            puesto.CategoriaTitular = null; // C12: prohibido en rotativos
            puesto.PerfilRequerido = detalle.PerfilRequerido;
            puesto.SexoPreferente = sexoNormalizado;
            puesto.HorasEnPuesto = (short?)detalle.TiempoEnPuesto;
            puesto.HorasRecuperacion = (short?)detalle.TiempoDeRecup;
            await db.SaveChangesAsync(ct);

            foreach (var skuId in skuIds.Distinct())
            {
                var yaVinculado = await db.PuestosSku.AnyAsync(ps => ps.PuestoId == puesto.Id && ps.SkuId == skuId, ct);
                if (yaVinculado) continue;

                db.PuestosSku.Add(new PuestoSku { PuestoId = puesto.Id, SkuId = skuId });
                enlacesImportados++;
            }
        }

        await db.SaveChangesAsync(ct);
        return ResultadoImportacion.Exito(filasLeidas, enlacesImportados);
    }

    private readonly record struct FilaPuestoSku(
        string? Item, string? Nombre, string? SexoPreferente,
        int? TiempoEnPuesto, int? TiempoDeRecup, string? PerfilRequerido);

    private static int? LeerEnteroONulo(IXLCell celda) =>
        celda.IsEmpty() ? null : (int)celda.GetDouble();
}

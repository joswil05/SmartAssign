using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Importacion;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E3.6 (docs/PROGRESO.md): "fila inválida → se rechaza el lote
/// entero, con informe". Usa libros construidos en memoria (no el Excel
/// real del cliente, que nunca debe depender el CI de un archivo con PII
/// fuera del repositorio) con la misma forma exacta de columnas.
/// </summary>
public class ImportadorDatosRealesTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    /// <summary>
    /// Puesto tiene RLS (04 §6.3, etapa E2). El importador es en la
    /// práctica una operación de Coordinador (§2.1.10: "editar todas las
    /// tablas de datos maestros") — sin fijar ese contexto, ni siquiera
    /// el propio importador vería las filas que acaba de insertar al
    /// intentar actualizarlas en una reimportación. Se fija aquí a mano
    /// porque la prueba no pasa por el pipeline HTTP real que lo haría
    /// automáticamente (ContextoSesionMiddleware, etapa E2).
    /// </summary>
    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != System.Data.ConnectionState.Open) await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InitializeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    private static MemoryStream LibroPersonal(params object[][] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Personal");
        var encabezados = new[] { "CodEmpleado", "Nombre Completo", "Sexo", "Perfil", "Cédula", "INSS", "FechaNac", "Asignación Prspto", "FechaIng", "Activo" };
        for (var i = 0; i < encabezados.Length; i++) hoja.Cell(1, i + 1).Value = encabezados[i];

        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);

        var mem = new MemoryStream();
        libro.SaveAs(mem);
        mem.Position = 0;
        return mem;
    }

    private static MemoryStream LibroPuestosFijos(params object[][] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Puestos Fijos");
        var encabezados = new[] { "IdPuesto", "NombrePuesto", "SexoPreferente", "TiempoEnPuesto", "TiempoDeRecup", "PerfilRequerido" };
        for (var i = 0; i < encabezados.Length; i++) hoja.Cell(1, i + 1).Value = encabezados[i];

        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);

        var mem = new MemoryStream();
        libro.SaveAs(mem);
        mem.Position = 0;
        return mem;
    }

    private static MemoryStream LibroPrograma(params object[][] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Programa");
        var encabezados = new[] { "OrdenProceso", "FechaProd", "Comentario", "Semana", "Linea", "Item", "Cajas", "Botellas", "TipoBot", "Producto", "Turno", "Velocidad" };
        for (var i = 0; i < encabezados.Length; i++) hoja.Cell(1, i + 1).Value = encabezados[i];

        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);

        var mem = new MemoryStream();
        libro.SaveAs(mem);
        mem.Position = 0;
        return mem;
    }

    private static MemoryStream LibroPuestosSku(params object[][] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Puestos SKU");
        var encabezados = new[] { "Linea", "Item", "TipoBot", "IdPuesto", "NombrePuesto", "SexoPreferente", "TiempoEnPuesto", "TiempoDeRecup", "PerfilRequerido" };
        for (var i = 0; i < encabezados.Length; i++) hoja.Cell(1, i + 1).Value = encabezados[i];

        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);

        var mem = new MemoryStream();
        libro.SaveAs(mem);
        mem.Position = 0;
        return mem;
    }

    private static MemoryStream LibroAusencias(params object[][] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Personal ausente");
        var encabezados = new[] { "CodEmpleado", "Nombre Completo", "CodSalida", "Fecha Salida", "Hora Salida", "Fecha Entrada", "Hora Entrada" };
        for (var i = 0; i < encabezados.Length; i++) hoja.Cell(1, i + 1).Value = encabezados[i];

        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);

        var mem = new MemoryStream();
        libro.SaveAs(mem);
        mem.Position = 0;
        return mem;
    }

    // ═══ Personal ═══

    [Fact]
    public async Task Importa_personal_valido_con_categoria_sexo_y_linea_mapeados()
    {
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPersonal(
            new object[] { "3558", "Juan Perez", "MASCULINO", "OPERARIO", "", "", "", "LINEA 1", "", true },
            new object[] { "97983", "Jose Flores", "MASCULINO", "OPERADOR DE EQUIPOS", "", "", "", "LINEA 1", "", true },
            new object[] { "40001", "Ana Solis", "FEMENINO", "OPERARIO", "", "", "", "MAQUILA", "", true });

        var resultado = await importador.ImportarPersonalAsync(libro);

        resultado.Exitoso.Should().BeTrue();
        resultado.FilasImportadas.Should().Be(3);

        var juan = await ctx.Personas.SingleAsync(p => p.Ficha == "3558");
        juan.Categoria.Should().Be("operario");
        juan.Sexo.Should().Be("masculino");
        juan.LineaHabitual.Should().Be((byte)1);

        var jose = await ctx.Personas.SingleAsync(p => p.Ficha == "97983");
        jose.Categoria.Should().Be("operador_a", "00 §G1: OPERADOR DE EQUIPOS mapea a Operador A");

        var ana = await ctx.Personas.SingleAsync(p => p.Ficha == "40001");
        ana.LineaHabitual.Should().BeNull("00 §G3: MAQUILA no es una de las 10 líneas");
    }

    [Theory]
    [InlineData("OPERADOR DE CALDERAS")]
    [InlineData("OPERARIO DE FILTROS Y TANQUERIA")]
    public async Task Mapea_calderas_y_filtros_a_operador_a_confirmado_por_el_cliente(string perfilCrudo)
    {
        // 00 §G1: "cuéntalos como operadores" (cliente, 2026-08-09).
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPersonal(
            new object[] { "1001", "Persona De Prueba", "MASCULINO", perfilCrudo, "", "", "", "LINEA 1", "", true });

        var resultado = await importador.ImportarPersonalAsync(libro);

        resultado.Exitoso.Should().BeTrue();
        (await ctx.Personas.SingleAsync()).Categoria.Should().Be("operador_a");
    }

    [Fact]
    public async Task Rechaza_el_lote_completo_si_una_sola_fila_tiene_categoria_desconocida()
    {
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPersonal(
            new object[] { "3558", "Juan Perez", "MASCULINO", "OPERARIO", "", "", "", "LINEA 1", "", true },
            new object[] { "99999", "Persona Rara", "MASCULINO", "CATEGORIA_QUE_NO_EXISTE", "", "", "", "LINEA 1", "", true });

        var resultado = await importador.ImportarPersonalAsync(libro);

        resultado.Exitoso.Should().BeFalse();
        resultado.Errores.Should().ContainSingle(e => e.Columna == "Perfil");

        // La fila válida NO se importó tampoco — todo o nada.
        (await ctx.Personas.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rechaza_fichas_duplicadas_dentro_del_mismo_archivo()
    {
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPersonal(
            new object[] { "3558", "Juan Perez", "MASCULINO", "OPERARIO", "", "", "", "LINEA 1", "", true },
            new object[] { "3558", "Otro Juan", "MASCULINO", "OPERARIO", "", "", "", "LINEA 2", "", true });

        var resultado = await importador.ImportarPersonalAsync(libro);

        resultado.Exitoso.Should().BeFalse();
        resultado.Errores.Should().ContainSingle(e => e.Mensaje.Contains("duplicada"));
    }

    [Fact]
    public async Task Reimportar_personal_actualiza_en_vez_de_duplicar()
    {
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using (var primero = LibroPersonal(new object[] { "3558", "Juan Perez", "MASCULINO", "OPERARIO", "", "", "", "LINEA 1", "", true }))
            await importador.ImportarPersonalAsync(primero);

        using (var segundo = LibroPersonal(new object[] { "3558", "Juan Perez Actualizado", "MASCULINO", "OPERARIO", "", "", "", "LINEA 2", "", true }))
            await importador.ImportarPersonalAsync(segundo);

        (await ctx.Personas.CountAsync()).Should().Be(1);
        var juan = await ctx.Personas.SingleAsync();
        juan.NombreCompleto.Should().Be("Juan Perez Actualizado");
        juan.LineaHabitual.Should().Be((byte)2);
    }

    // ═══ Puestos Fijos ═══

    [Fact]
    public async Task Reimportar_puestos_actualiza_en_vez_de_duplicar()
    {
        // Puesto tiene RLS (E2): sin contexto de coordinador, el propio
        // importador no vería la fila que insertó la primera vez al
        // reimportar, e intentaría un segundo INSERT que chocaría con
        // UQ_Puesto. Esta prueba existe para que ese escenario no vuelva.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using (var primero = LibroPuestosFijos(new object[] { "L01001", "Supervisor", "Indistinto", "", "", "Supervisor" }))
            await importador.ImportarPuestosFijosAsync(primero);

        using (var segundo = LibroPuestosFijos(new object[] { "L01001", "Supervisor Turno B", "Indistinto", "", "", "Supervisor" }))
            (await importador.ImportarPuestosFijosAsync(segundo)).Exitoso.Should().BeTrue();

        (await ctx.Puestos.CountAsync()).Should().Be(1);
        (await ctx.Puestos.SingleAsync()).NombrePuesto.Should().Be("Supervisor Turno B");
    }

    [Fact]
    public async Task Importa_puestos_derivando_fijo_o_rotativo_del_tiempo_en_puesto()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPuestosFijos(
            new object[] { "L01001", "Supervisor", "Indistinto", "", "", "Supervisor" },       // sin TiempoEnPuesto -> fijo
            new object[] { "L01010", "Girar botellas 1", "Femenino", 2, 24, "Indistinto" });    // con TiempoEnPuesto -> rotativo

        var resultado = await importador.ImportarPuestosFijosAsync(libro);

        resultado.Exitoso.Should().BeTrue();

        var fijo = await ctx.Puestos.SingleAsync(p => p.Codigo == "L01001");
        fijo.Tipo.Should().Be("fijo");
        fijo.LineaId.Should().Be((byte)1);
        fijo.CategoriaTitular.Should().BeNull("00 §G5: no se puede derivar de PerfilRequerido, no se inventa");

        var rotativo = await ctx.Puestos.SingleAsync(p => p.Codigo == "L01010");
        rotativo.Tipo.Should().Be("rotativo");
        rotativo.HorasRecuperacion.Should().Be((short)24);
    }

    [Theory]
    [InlineData("Averiero", "averiero")]
    [InlineData("Operador", "operador_a")]
    [InlineData("Genérico", "operador_a")]
    [InlineData("Operador de filtro", "operador_a")]
    public async Task Repara_categoria_titular_desde_perfil_requerido_confirmado_por_el_cliente(string perfilRequerido, string categoriaEsperada)
    {
        // 00 §G5, resuelto (2026-08-09): "por ahora tómalos a todos esos
        // como si fuese operador A, no hay operadores C ni B por los
        // momentos" — Averiero es mapeo literal, sin ambigüedad.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPuestosFijos(
            new object[] { "L01005", "Puesto de prueba", "Indistinto", "", "", perfilRequerido });

        var resultado = await importador.ImportarPuestosFijosAsync(libro);

        resultado.Exitoso.Should().BeTrue();
        (await ctx.Puestos.SingleAsync()).CategoriaTitular.Should().Be(categoriaEsperada);
    }

    [Fact]
    public async Task Puesto_supervisor_no_recibe_categoria_titular()
    {
        // "Supervisor" queda fuera del mapa a propósito: personal de
        // liderazgo nunca se asigna automáticamente (§4.1) —
        // sp_BarridoPuestosFijos (E5.5) excluye el puesto en vez de
        // necesitar una categoría inventada para él.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPuestosFijos(
            new object[] { "L01001", "Supervisor", "Indistinto", "", "", "Supervisor" });

        await importador.ImportarPuestosFijosAsync(libro);

        (await ctx.Puestos.SingleAsync()).CategoriaTitular.Should().BeNull();
    }

    [Fact]
    public async Task Rechaza_sexo_preferente_contaminado_sin_patron_de_reparacion_conocido()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPuestosFijos(
            new object[] { "L01002", "Armadora de Cajas", "Genérico", "", "", "Genérico" }); // sin patrón 100% consistente en los datos reales

        var resultado = await importador.ImportarPuestosFijosAsync(libro);

        resultado.Exitoso.Should().BeFalse();
        resultado.Errores.Should().ContainSingle(e => e.Columna == "SexoPreferente");
        (await ctx.Puestos.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("Supervisor", "Indistinto")]
    [InlineData("Operador", "Masculino")]
    [InlineData("Averiero", "Masculino")]
    [InlineData("Estibador", "Masculino")]
    public async Task Repara_sexo_preferente_arrastrado_de_perfil_requerido_por_patron_conocido(string valorArrastrado, string sexoEsperado)
    {
        // 00 §G6: dato real observado en la Línea 6 (mismas 8 filas que
        // antes se rechazaban por A13) — se repara en vez de rechazarse
        // porque el resto del archivo confirma el valor sin excepción,
        // y el cliente validó que no hay más error de captura ahí.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPuestosFijos(
            new object[] { "L06001", "Puesto de prueba", valorArrastrado, "", "", valorArrastrado });

        var resultado = await importador.ImportarPuestosFijosAsync(libro);

        resultado.Exitoso.Should().BeTrue();
        (await ctx.Puestos.SingleAsync()).SexoPreferente.Should().Be(sexoEsperado);
    }

    [Fact]
    public async Task Normaliza_femenina_a_femenino_por_error_de_tipeo_confirmado_por_el_cliente()
    {
        // 00 §G6: "la diferencia entre femenino y femenina no existe, es
        // un error de escritura" (cliente, 2026-08-09).
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPuestosFijos(
            new object[] { "L05008", "Revisión y empaque", "Femenina", 2, 2, "Indistinto" });

        var resultado = await importador.ImportarPuestosFijosAsync(libro);

        resultado.Exitoso.Should().BeTrue();
        (await ctx.Puestos.SingleAsync()).SexoPreferente.Should().Be("Femenino");
    }

    // ═══ Programa (SKU) — UT-E5.3 ═══

    [Fact]
    public async Task Importa_catalogo_sku_desde_programa_deduplicando_por_item()
    {
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPrograma(
            new object[] { 58204, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850EC0832L35", 200, 2400, "N", "Blanco Reserva EC", "1T8", 500 },
            // El mismo Item repetido con el mismo Producto/Velocidad no duplica el catálogo.
            new object[] { 58205, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850EC0832L35", 10, 20, "N", "Blanco Reserva EC", "1T8", 500 },
            new object[] { 58128, new DateTime(2026, 5, 28), "", new DateTime(2026, 5, 11), "Linea 4", "861NI3274I23", 33300, 799200, "N", "Plata Especial Suave NI", "2T12", 450 });

        var resultado = await importador.ImportarSkuAsync(libro);

        resultado.Exitoso.Should().BeTrue();
        (await ctx.Skus.CountAsync()).Should().Be(2);
        var sku = await ctx.Skus.SingleAsync(s => s.Codigo == "850EC0832L35");
        sku.Descripcion.Should().Be("Blanco Reserva EC");
        sku.RitmoTeoricoHora.Should().Be(500);
    }

    [Fact]
    public async Task Rechaza_el_lote_si_el_mismo_item_trae_velocidad_distinta_en_dos_filas()
    {
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPrograma(
            new object[] { 1, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850EC0832L35", 200, 2400, "N", "Blanco Reserva EC", "1T8", 500 },
            new object[] { 2, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850EC0832L35", 200, 2400, "N", "Blanco Reserva EC", "1T8", 999 });

        var resultado = await importador.ImportarSkuAsync(libro);

        resultado.Exitoso.Should().BeFalse();
        resultado.Errores.Should().ContainSingle(e => e.Columna == "Item");
        (await ctx.Skus.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reimportar_sku_actualiza_ritmo_en_vez_de_duplicar()
    {
        await using var ctx = CrearContexto();
        var importador = new ImportadorDatosReales(ctx);

        using (var primero = LibroPrograma(new object[] { 1, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850EC0832L35", 200, 2400, "N", "Blanco Reserva EC", "1T8", 500 }))
            await importador.ImportarSkuAsync(primero);

        using (var segundo = LibroPrograma(new object[] { 1, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850EC0832L35", 200, 2400, "N", "Blanco Reserva EC", "1T8", 650 }))
            (await importador.ImportarSkuAsync(segundo)).Exitoso.Should().BeTrue();

        (await ctx.Skus.CountAsync()).Should().Be(1);
        (await ctx.Skus.SingleAsync()).RitmoTeoricoHora.Should().Be(650);
    }

    // ═══ Puestos SKU — UT-E5.3 (00 §G4: hoja deliberadamente incompleta) ═══

    [Fact]
    public async Task Importa_solo_las_filas_de_puestos_sku_verificables_contra_el_catalogo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using (var programa = LibroPrograma(
            new object[] { 1, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850RM4H32L40", 1, 1, "R", "Perfect 10 RM", "1T8", 500 },
            new object[] { 2, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850SV4H32L40", 1, 1, "R", "Perfect 10 SV", "1T8", 500 }))
            (await importador.ImportarSkuAsync(programa)).Exitoso.Should().BeTrue();

        using var libro = LibroPuestosSku(
            // Línea 1: fila completa (con detalle) + fila de continuación (mismo puesto, otro SKU real) — ambas verificables.
            new object[] { "Linea 1", "850RM4H32L40", "R", "L01S001", "Lampara 1,2", "Femenino", 1, 3, "Indistinto" },
            new object[] { "Linea 1", "850SV4H32L40", "R", "L01S001", "", "", "", "", "" },
            // Línea 2: sin Item — no verificable, se omite.
            new object[] { "Linea 2", "", "R", "L02S001", "", "", "", "", "" },
            // Línea 4: Item "Alcohol" no es un código real del catálogo — se omite.
            new object[] { "Linea 4", "Alcohol", "", "L04S001", "Sticker 1", "Femenino", "", "", "" },
            // Línea 5: sin ningún dato — se omite entero (ni siquiera hay nombre para crear el puesto).
            new object[] { "Linea 5", "", "", "L05S001", "", "", "", "", "" });

        var resultado = await importador.ImportarPuestosSkuAsync(libro);

        resultado.Exitoso.Should().BeTrue("00 §G4: la hoja incompleta no rechaza el lote, solo omite lo no verificable");
        resultado.FilasLeidas.Should().Be(5);
        resultado.FilasImportadas.Should().Be(2, "dos enlaces puesto-SKU verificables (L01S001 con dos SKU reales de Línea 1)");

        (await ctx.Puestos.CountAsync(p => p.Codigo == "L01S001")).Should().Be(1);
        var puesto = await ctx.Puestos.SingleAsync(p => p.Codigo == "L01S001");
        puesto.Tipo.Should().Be("rotativo");
        puesto.NombrePuesto.Should().Be("Lampara 1,2");
        puesto.CategoriaTitular.Should().BeNull("C12: un rotativo nunca declara categoria_titular");
        (await ctx.PuestosSku.CountAsync(ps => ps.PuestoId == puesto.Id)).Should().Be(2);

        (await ctx.Puestos.AnyAsync(p => p.Codigo == "L02S001")).Should().BeFalse();
        (await ctx.Puestos.AnyAsync(p => p.Codigo == "L04S001")).Should().BeFalse();
        (await ctx.Puestos.AnyAsync(p => p.Codigo == "L05S001")).Should().BeFalse();
    }

    [Fact]
    public async Task Reimportar_puestos_sku_no_duplica_el_enlace()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using (var programa = LibroPrograma(new object[] { 1, new DateTime(2026, 5, 26), "", new DateTime(2026, 5, 18), "Linea 1", "850RM4H32L40", 1, 1, "R", "Perfect 10 RM", "1T8", 500 }))
            await importador.ImportarSkuAsync(programa);

        var fila = new object[] { "Linea 1", "850RM4H32L40", "R", "L01S001", "Lampara 1,2", "Femenino", 1, 3, "Indistinto" };
        using (var primero = LibroPuestosSku(fila))
            await importador.ImportarPuestosSkuAsync(primero);
        using (var segundo = LibroPuestosSku(fila))
            (await importador.ImportarPuestosSkuAsync(segundo)).FilasImportadas.Should().Be(0, "el enlace ya existía");

        (await ctx.PuestosSku.CountAsync()).Should().Be(1);
    }

    // ═══ Ausencias ═══

    [Fact]
    public async Task Importa_ausencias_de_personal_ya_existente()
    {
        await using var ctx = CrearContexto();
        var usuario = new Usuario { Username = "coord_import", NombreCompleto = "Coordinador", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var importador = new ImportadorDatosReales(ctx);
        using (var personal = LibroPersonal(new object[] { "11741", "Sonia Paz", "FEMENINO", "OPERARIO", "", "", "", "LINEA 1", "", true }))
            await importador.ImportarPersonalAsync(personal);

        using var ausencias = LibroAusencias(
            new object[] { "11741", "Sonia Paz", "Vacaciones", new DateTime(2026, 5, 27), new DateTime(1899, 12, 30, 6, 0, 0), new DateTime(2026, 6, 10), new DateTime(1899, 12, 30, 6, 0, 0) });

        var resultado = await importador.ImportarAusenciasAsync(ausencias, usuario.Id);

        resultado.Exitoso.Should().BeTrue();
        var ausencia = await ctx.AusenciasJustificadas.SingleAsync();
        ausencia.Tipo.Should().Be("vacaciones");
        ausencia.FechaInicio.Should().Be(new DateOnly(2026, 5, 27));
        ausencia.FechaFin.Should().Be(new DateOnly(2026, 6, 10));
    }

    [Fact]
    public async Task Rechaza_ausencia_de_ficha_que_no_existe_en_Personal()
    {
        await using var ctx = CrearContexto();
        var usuario = new Usuario { Username = "coord_import2", NombreCompleto = "Coordinador", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var importador = new ImportadorDatosReales(ctx);
        using var ausencias = LibroAusencias(
            new object[] { "99999999", "Nadie", "Vacaciones", new DateTime(2026, 5, 27), new DateTime(1899, 12, 30, 6, 0, 0), new DateTime(2026, 6, 10), new DateTime(1899, 12, 30, 6, 0, 0) });

        var resultado = await importador.ImportarAusenciasAsync(ausencias, usuario.Id);

        resultado.Exitoso.Should().BeFalse();
        resultado.Errores.Should().ContainSingle(e => e.Columna == "CodEmpleado");
    }
}

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

    [Fact]
    public async Task Rechaza_sexo_preferente_contaminado_con_valores_de_perfil_requerido()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var importador = new ImportadorDatosReales(ctx);

        using var libro = LibroPuestosFijos(
            new object[] { "L01002", "Armadora de Cajas", "Operador", "", "", "Operador" }); // dato real observado (00 §A13)

        var resultado = await importador.ImportarPuestosFijosAsync(libro);

        resultado.Exitoso.Should().BeFalse();
        resultado.Errores.Should().ContainSingle(e => e.Columna == "SexoPreferente");
        (await ctx.Puestos.CountAsync()).Should().Be(0);
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

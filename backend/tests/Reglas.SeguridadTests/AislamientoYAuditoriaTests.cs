using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Trazabilidad;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E2.4 y UT-E2.5 (docs/PROGRESO.md), como pruebas automatizadas
/// contra una base descartable — mismo patrón que EsquemaBaseTests.
/// </summary>
public class AislamientoYAuditoriaTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<SmartAssignDbContext>()
            .UseSqlServer(CadenaConexion)
            .Options;
        return new SmartAssignDbContext(options);
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

    // ═══ E2.4 — RLS: "con el filtro de aplicación desactivado, la
    // consulta sigue bloqueada" (docs/PROGRESO.md). Las tres pruebas de
    // abajo usan una SqlConnection cruda, sin pasar por ningún
    // repositorio ni interceptor de la aplicación. ═══

    [Fact]
    public async Task La_RLS_bloquea_Puesto_por_defecto_sin_ningun_contexto_de_sesion()
    {
        await SembrarDosPuestosDeLineasDistintasAsync();

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        var total = await ContarPuestosAsync(conexion);

        total.Should().Be(0, "sin SESSION_CONTEXT la política de RLS no debe dejar ver ninguna fila (04 §6.3)");
    }

    [Fact]
    public async Task La_RLS_deja_ver_todo_a_quien_tiene_contexto_de_coordinador()
    {
        await SembrarDosPuestosDeLineasDistintasAsync();

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await EjecutarAsync(conexion, "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';");

        var total = await ContarPuestosAsync(conexion);

        total.Should().Be(2, "el Coordinador tiene alcance sobre las 10 líneas, sin filtro (04 §6.2)");
    }

    [Fact]
    public async Task La_RLS_limita_a_un_supervisor_a_los_puestos_de_su_propia_linea()
    {
        await SembrarDosPuestosDeLineasDistintasAsync();

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await EjecutarAsync(conexion, "EXEC sys.sp_set_session_context @key = N'rol', @value = N'supervisor';");
        await EjecutarAsync(conexion, "DECLARE @l TINYINT = 1; EXEC sys.sp_set_session_context @key = N'linea_id', @value = @l;");

        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT linea_id FROM Puesto";
        await using var lector = await cmd.ExecuteReaderAsync();
        var lineasVisibles = new List<byte>();
        while (await lector.ReadAsync()) lineasVisibles.Add(lector.GetByte(0));

        lineasVisibles.Should().Equal([(byte)1], "un supervisor de L1 nunca debe poder leer puestos de L2, aunque el filtro del repositorio falle");
    }

    private async Task SembrarDosPuestosDeLineasDistintasAsync()
    {
        await using var ctx = CrearContexto();
        ctx.Puestos.Add(new Puesto { LineaId = 1, Codigo = "T1", NombrePuesto = "Prueba L1", Tipo = "rotativo" });
        ctx.Puestos.Add(new Puesto { LineaId = 2, Codigo = "T1", NombrePuesto = "Prueba L2", Tipo = "rotativo" });
        await ctx.SaveChangesAsync();
    }

    private static async Task<int> ContarPuestosAsync(SqlConnection conexion)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Puesto";
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task EjecutarAsync(SqlConnection conexion, string sql)
    {
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    // ═══ E2.5 — Auditoría: "una operación y un rechazo dejan fila"
    // (docs/PROGRESO.md). El login es la primera operación real
    // disponible en esta etapa — el motor de asignación llega en E4. ═══

    [Fact]
    public async Task Un_login_exitoso_deja_una_fila_OK_en_Auditoria()
    {
        await using var ctx = CrearContexto();
        var credenciales = new ServicioCredenciales();
        var (hash, salt) = credenciales.HashConSal("Clave#Segura123");
        ctx.Usuarios.Add(new Usuario
        {
            Username = "sup_auditoria_ok",
            NombreCompleto = "Prueba Auditoría",
            Rol = "supervisor",
            OrigenIdentidad = "local",
            PasswordHash = hash,
            PasswordSalt = salt,
            Activo = true,
        });
        await ctx.SaveChangesAsync();

        var servicio = CrearServicioAutenticacion(ctx, credenciales);
        var resultado = await servicio.IniciarSesionAsync("sup_auditoria_ok", "Clave#Segura123", "device-1");

        resultado.Exitoso.Should().BeTrue();

        var filas = await ctx.Auditorias.Where(a => a.Accion == "LOGIN" && a.Resultado == "OK").ToListAsync();
        filas.Should().ContainSingle();
    }

    [Fact]
    public async Task Un_login_con_clave_equivocada_deja_una_fila_RECHAZO_en_Auditoria()
    {
        await using var ctx = CrearContexto();
        var credenciales = new ServicioCredenciales();
        var (hash, salt) = credenciales.HashConSal("Clave#Correcta1");
        ctx.Usuarios.Add(new Usuario
        {
            Username = "sup_auditoria_rechazo",
            NombreCompleto = "Prueba Auditoría",
            Rol = "supervisor",
            OrigenIdentidad = "local",
            PasswordHash = hash,
            PasswordSalt = salt,
            Activo = true,
        });
        await ctx.SaveChangesAsync();

        var servicio = CrearServicioAutenticacion(ctx, credenciales);
        var resultado = await servicio.IniciarSesionAsync("sup_auditoria_rechazo", "clave-equivocada", "device-1");

        resultado.Exitoso.Should().BeFalse();
        resultado.CodigoRechazo.Should().Be(CodigosRechazoSesion.CredencialesInvalidas);

        var filas = await ctx.Auditorias.Where(a => a.Accion == "LOGIN" && a.Resultado == "RECHAZO").ToListAsync();
        filas.Should().ContainSingle();
        filas.Single().CodigoRechazo.Should().Be(CodigosRechazoSesion.CredencialesInvalidas);
    }

    private static ServicioAutenticacion CrearServicioAutenticacion(SmartAssignDbContext ctx, IServicioCredenciales credenciales)
    {
        var tokens = new ServicioTokensJwt(Options.Create(new JwtOptions
        {
            Emisor = "SmartAssign.Pruebas",
            Audiencia = "SmartAssign.Pruebas",
            ClaveSecreta = "clave-de-prueba-de-al-menos-32-bytes-de-largo-total",
            AccessMinutos = 15,
            RefreshHoras = 12,
        }));
        var auditoria = new RegistradorAuditoria(ctx);
        return new ServicioAutenticacion(ctx, credenciales, tokens, auditoria);
    }
}

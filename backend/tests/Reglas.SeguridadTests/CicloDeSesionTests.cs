using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Trazabilidad;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E2.2 (docs/PROGRESO.md): el ciclo completo de D6 — login, refresh
/// silencioso y PIN de reentrada, incluida la regla explícita de
/// 04 §6.4: "3 PIN fallidos → cierre de sesión y login completo".
/// </summary>
public class CicloDeSesionTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static readonly ServicioCredenciales Credenciales = new();

    private ServicioAutenticacion CrearServicio(SmartAssignDbContext ctx) => new(
        ctx, Credenciales,
        new ServicioTokensJwt(Options.Create(new JwtOptions
        {
            Emisor = "SmartAssign.Pruebas",
            Audiencia = "SmartAssign.Pruebas",
            ClaveSecreta = "clave-de-prueba-de-al-menos-32-bytes-de-largo-total",
            AccessMinutos = 15,
            RefreshHoras = 12,
        })),
        new RegistradorAuditoria(ctx));

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

    private async Task<int> CrearUsuarioConPinAsync(string username, string password, string pin)
    {
        await using var ctx = CrearContexto();
        var (hashPw, saltPw) = Credenciales.HashConSal(password);
        var (hashPin, saltPin) = Credenciales.HashConSal(pin);

        var usuario = new Usuario
        {
            Username = username, NombreCompleto = username, Rol = "supervisor",
            OrigenIdentidad = "local", PasswordHash = hashPw, PasswordSalt = saltPw,
            PinHash = hashPin, PinSalt = saltPin, Activo = true,
        };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        return usuario.Id;
    }

    [Fact]
    public async Task El_refresh_renueva_el_access_token_mientras_la_sesion_siga_activa()
    {
        await CrearUsuarioConPinAsync("u_refresh", "Clave#Uno12345", "1234");

        await using var ctx = CrearContexto();
        var servicio = CrearServicio(ctx);

        var login = await servicio.IniciarSesionAsync("u_refresh", "Clave#Uno12345", "device-1");
        login.Exitoso.Should().BeTrue();

        var renovado = await servicio.RenovarAsync(login.RefreshToken!, "device-1");

        renovado.Exitoso.Should().BeTrue();
        renovado.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task El_refresh_falla_si_el_device_id_no_coincide()
    {
        await CrearUsuarioConPinAsync("u_refresh_mal", "Clave#Uno12345", "1234");

        await using var ctx = CrearContexto();
        var servicio = CrearServicio(ctx);

        var login = await servicio.IniciarSesionAsync("u_refresh_mal", "Clave#Uno12345", "device-1");
        var renovado = await servicio.RenovarAsync(login.RefreshToken!, "device-DISTINTO");

        renovado.Exitoso.Should().BeFalse();
        renovado.CodigoRechazo.Should().Be(CodigosRechazoSesion.RefreshInvalido);
    }

    [Fact]
    public async Task El_PIN_correcto_reentra_sin_pedir_credenciales_completas()
    {
        var usuarioId = await CrearUsuarioConPinAsync("u_pin_ok", "Clave#Uno12345", "1234");

        await using var ctx = CrearContexto();
        var servicio = CrearServicio(ctx);
        await servicio.IniciarSesionAsync("u_pin_ok", "Clave#Uno12345", "device-1");

        var reentrada = await servicio.ReentrarConPinAsync(usuarioId, "1234", "device-1");

        reentrada.Exitoso.Should().BeTrue();
        reentrada.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Tres_PIN_fallidos_cierran_la_sesion_y_exigen_login_completo()
    {
        // 04 §6.4: "3 PIN fallidos → cierre de sesión y login completo".
        var usuarioId = await CrearUsuarioConPinAsync("u_pin_mal", "Clave#Uno12345", "1234");

        await using var ctx = CrearContexto();
        var servicio = CrearServicio(ctx);
        await servicio.IniciarSesionAsync("u_pin_mal", "Clave#Uno12345", "device-1");

        var primero = await servicio.ReentrarConPinAsync(usuarioId, "0000", "device-1");
        var segundo = await servicio.ReentrarConPinAsync(usuarioId, "0000", "device-1");
        var tercero = await servicio.ReentrarConPinAsync(usuarioId, "0000", "device-1");

        primero.CodigoRechazo.Should().Be(CodigosRechazoSesion.PinIncorrecto);
        segundo.CodigoRechazo.Should().Be(CodigosRechazoSesion.PinIncorrecto);
        tercero.CodigoRechazo.Should().Be(CodigosRechazoSesion.PinMaxIntentosSesionCerrada);

        // La sesión quedó revocada: ni el PIN correcto la reactiva.
        var conPinCorrectoTrasBloqueo = await servicio.ReentrarConPinAsync(usuarioId, "1234", "device-1");
        conPinCorrectoTrasBloqueo.Exitoso.Should().BeFalse(
            "el PIN nunca abre una sesión que ya se cerró — debe exigir login completo (04 §6.4)");
    }

    [Fact]
    public async Task El_PIN_nunca_abre_la_sesion_de_otro_usuario()
    {
        var idUno = await CrearUsuarioConPinAsync("u_pin_a", "Clave#Uno12345", "1111");
        await CrearUsuarioConPinAsync("u_pin_b", "Clave#Dos12345", "2222");

        await using var ctx = CrearContexto();
        var servicio = CrearServicio(ctx);
        await servicio.IniciarSesionAsync("u_pin_a", "Clave#Uno12345", "device-1");

        // El PIN de B nunca debería validar contra la sesión de A.
        var resultado = await servicio.ReentrarConPinAsync(idUno, "2222", "device-1");

        resultado.Exitoso.Should().BeFalse("D6: el PIN nunca abre la sesión de otro usuario");
    }

    [Fact]
    public async Task Cerrar_sesion_revoca_la_sesion_y_el_refresh_deja_de_servir()
    {
        await CrearUsuarioConPinAsync("u_logout", "Clave#Uno12345", "1234");

        await using var ctx = CrearContexto();
        var servicio = CrearServicio(ctx);
        var login = await servicio.IniciarSesionAsync("u_logout", "Clave#Uno12345", "device-1");

        await servicio.CerrarSesionAsync(login.UsuarioId!.Value, "device-1");

        var renovado = await servicio.RenovarAsync(login.RefreshToken!, "device-1");
        renovado.Exitoso.Should().BeFalse();
    }
}

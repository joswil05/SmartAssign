using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E2.2 (docs/PROGRESO.md): "prueba que inspecciona los claims y falla
/// si aparece linea_id". La línea de un supervisor nunca puede viajar en
/// el access token (§2.3, 04 §6.4: "claims: sub, rol, nombre — SIN
/// linea_id") — se resuelve en vivo en cada petición, nunca desde el token.
/// </summary>
public class TokenClaimsTests
{
    private static ServicioTokensJwt CrearServicio() => new(Options.Create(new JwtOptions
    {
        Emisor = "SmartAssign.Pruebas",
        Audiencia = "SmartAssign.Pruebas",
        ClaveSecreta = "clave-de-prueba-de-al-menos-32-bytes-de-largo-total",
        AccessMinutos = 15,
        RefreshHoras = 12,
    }));

    [Fact]
    public void El_access_token_nunca_lleva_linea_id()
    {
        var servicio = CrearServicio();
        var usuario = new Usuario { Id = 42, Username = "sup1", NombreCompleto = "Supervisor Uno", Rol = "supervisor" };

        var token = servicio.GenerarAccessToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Valor);

        jwt.Claims.Should().NotContain(c => c.Type == "linea_id",
            "§2.3: la línea del supervisor se resuelve en vivo, nunca se guarda en el token");
    }

    [Fact]
    public void El_access_token_lleva_exactamente_sub_rol_y_nombre()
    {
        var servicio = CrearServicio();
        var usuario = new Usuario { Id = 7, Username = "coord", NombreCompleto = "Coordinador Uno", Rol = "coordinador" };

        var token = servicio.GenerarAccessToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Valor);

        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == "7");
        jwt.Claims.Should().Contain(c => c.Type == "rol" && c.Value == "coordinador");
        jwt.Claims.Should().Contain(c => c.Type == "nombre" && c.Value == "Coordinador Uno");
    }

    [Fact]
    public void El_access_token_expira_en_15_minutos()
    {
        var servicio = CrearServicio();
        var usuario = new Usuario { Id = 1, Username = "x", NombreCompleto = "X", Rol = "coordinador" };

        var antes = DateTime.UtcNow;
        var token = servicio.GenerarAccessToken(usuario);

        token.ExpiraEn.Should().BeCloseTo(antes.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void El_refresh_token_es_opaco_no_un_jwt()
    {
        var servicio = CrearServicio();

        var refresh = servicio.GenerarRefreshTokenOpaco();

        // Un JWT tiene tres segmentos separados por punto; el refresh no.
        refresh.Split('.').Should().HaveCount(1, "el refresh token no lleva claims — es un secreto opaco (04 §6.4)");
    }
}

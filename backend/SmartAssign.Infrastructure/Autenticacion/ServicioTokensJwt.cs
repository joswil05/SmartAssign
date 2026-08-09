using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Domain.Entities;

namespace SmartAssign.Infrastructure.Autenticacion;

/// <summary>
/// Emite el access token con exactamente los claims de
/// <see cref="ClaimsSmartAssign"/> — <b>nunca</b> <c>linea_id</c> (§2.3,
/// 04 §6.4) — y un refresh token opaco (no JWT) de 256 bits de entropía.
/// </summary>
public class ServicioTokensJwt(IOptions<JwtOptions> opciones) : IServicioTokens
{
    private readonly JwtOptions _opciones = opciones.Value;

    public TimeSpan DuracionRefresh => TimeSpan.FromHours(_opciones.RefreshHoras);

    public TokenEmitido GenerarAccessToken(Usuario usuario)
    {
        var expiraEn = DateTime.UtcNow.AddMinutes(_opciones.AccessMinutos);

        var claims = new[]
        {
            new Claim(ClaimsSmartAssign.UsuarioId, usuario.Id.ToString()),
            new Claim(ClaimsSmartAssign.Rol, usuario.Rol),
            new Claim(ClaimsSmartAssign.Nombre, usuario.NombreCompleto),
        };

        var clave = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_opciones.ClaveSecreta));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            expires: expiraEn,
            signingCredentials: credenciales);

        return new TokenEmitido(new JwtSecurityTokenHandler().WriteToken(token), expiraEn);
    }

    public string GenerarRefreshTokenOpaco() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

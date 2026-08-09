using SmartAssign.Domain.Entities;

namespace SmartAssign.Application.Autenticacion;

/// <summary>Un token ya emitido, con su vencimiento (04 §6.4: 15 min de vida el access token).</summary>
public record TokenEmitido(string Valor, DateTime ExpiraEn);

/// <summary>
/// Emite los dos tokens del ciclo de D6: el access token JWT de vida
/// corta con los claims de <see cref="ClaimsSmartAssign"/>, y el refresh
/// token opaco de alta entropía ligado al dispositivo (04 §6.4).
/// </summary>
public interface IServicioTokens
{
    TokenEmitido GenerarAccessToken(Usuario usuario);

    /// <summary>Cadena aleatoria de alta entropía — no es un JWT, no lleva claims.</summary>
    string GenerarRefreshTokenOpaco();

    TimeSpan DuracionRefresh { get; }
}

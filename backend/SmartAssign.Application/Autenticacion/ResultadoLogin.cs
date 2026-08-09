namespace SmartAssign.Application.Autenticacion;

/// <summary>
/// Resultado de cualquier operación del ciclo de sesión. Cuando
/// <see cref="Exitoso"/> es false, <see cref="CodigoRechazo"/> siempre
/// tiene valor — nunca un texto libre, para que el llamador pueda
/// decidir sin analizar mensajes (mismo principio que sp_ValidarAsignacion, 04 §7.2).
/// </summary>
public record ResultadoLogin
{
    public bool Exitoso { get; init; }
    public string? CodigoRechazo { get; init; }

    public int? UsuarioId { get; init; }
    public string? Rol { get; init; }
    public string? NombreCompleto { get; init; }

    public string? AccessToken { get; init; }
    public DateTime? AccessExpiraEn { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? RefreshExpiraEn { get; init; }

    public static ResultadoLogin Rechazo(string codigo) => new() { Exitoso = false, CodigoRechazo = codigo };

    public static ResultadoLogin Exito(
        int usuarioId, string rol, string nombreCompleto,
        string accessToken, DateTime accessExpiraEn,
        string? refreshToken = null, DateTime? refreshExpiraEn = null) => new()
    {
        Exitoso = true,
        UsuarioId = usuarioId,
        Rol = rol,
        NombreCompleto = nombreCompleto,
        AccessToken = accessToken,
        AccessExpiraEn = accessExpiraEn,
        RefreshToken = refreshToken,
        RefreshExpiraEn = refreshExpiraEn,
    };
}

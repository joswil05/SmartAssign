namespace SmartAssign.Application.Autenticacion;

/// <summary>
/// El ciclo completo de D6 / 04 §6.4: login inicial contra AD/Entra ID o
/// credenciales locales, refresh silencioso mientras la sesión siga
/// activa, y PIN de reentrada tras un bloqueo de inactividad —
/// <b>el PIN nunca abre la sesión de otro usuario</b> (D6) y tres PIN
/// fallidos fuerzan login completo (04 §6.4).
/// </summary>
public interface IServicioAutenticacion
{
    Task<ResultadoLogin> IniciarSesionAsync(string username, string password, string deviceId, CancellationToken ct = default);

    Task<ResultadoLogin> RenovarAsync(string refreshToken, string deviceId, CancellationToken ct = default);

    Task<ResultadoLogin> ReentrarConPinAsync(int usuarioId, string pin, string deviceId, CancellationToken ct = default);

    Task CerrarSesionAsync(int usuarioId, string deviceId, CancellationToken ct = default);
}

/// <summary>Códigos de rechazo estables del ciclo de sesión — nunca texto libre.</summary>
public static class CodigosRechazoSesion
{
    public const string CredencialesInvalidas = "CREDENCIALES_INVALIDAS";
    public const string UsuarioInactivo = "USUARIO_INACTIVO";
    public const string UsuarioBloqueado = "USUARIO_BLOQUEADO";
    public const string OrigenAdNoDisponible = "ORIGEN_AD_NO_DISPONIBLE";
    public const string RefreshInvalido = "REFRESH_INVALIDO";
    public const string RefreshExpirado = "REFRESH_EXPIRADO";
    public const string PinNoConfigurado = "PIN_NO_CONFIGURADO";
    public const string PinIncorrecto = "PIN_INCORRECTO";
    public const string PinMaxIntentosSesionCerrada = "PIN_MAX_INTENTOS_SESION_CERRADA";
}

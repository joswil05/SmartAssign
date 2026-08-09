namespace SmartAssign.Application.Seguridad;

/// <summary>
/// Estado ambiental de la petición en curso: el rol y la línea (ya
/// resuelta en vivo, nunca del token) del usuario autenticado. Lo llena
/// un middleware de la Api tras autenticar, y lo lee el interceptor que
/// fija <c>SESSION_CONTEXT</c> para la RLS de la capa 3 (04 §6.3, §6.4).
/// Con instancia con alcance de petición (scoped): cada petición HTTP
/// tiene la suya.
/// </summary>
public interface IContextoSesionActual
{
    string? Rol { get; }
    byte? LineaId { get; }
    void Establecer(string rol, byte? lineaId);
}

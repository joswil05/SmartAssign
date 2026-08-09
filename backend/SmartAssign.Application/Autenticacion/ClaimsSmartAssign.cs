namespace SmartAssign.Application.Autenticacion;

/// <summary>
/// Nombres de claims del access token (04 §6.1, §6.4):
/// <c>"claims: sub, rol, nombre — SIN linea_id (§2.3)"</c>.
///
/// La línea de un supervisor NUNCA viaja en el token. Guardarla ahí
/// permitiría que una reasignación del Coordinador tardara hasta la
/// expiración del access token en surtir efecto; en su lugar, se
/// resuelve en vivo en cada petición desde <c>Linea.SupervisorActualId</c>.
/// <see cref="LineaIdProhibido"/> existe solo para que las pruebas busquen
/// ese nombre exacto y fallen si alguna vez aparece.
/// </summary>
public static class ClaimsSmartAssign
{
    public const string UsuarioId = "sub";
    public const string Rol = "rol";
    public const string Nombre = "nombre";
    public const string DeviceId = "device_id";

    public const string LineaIdProhibido = "linea_id";
}

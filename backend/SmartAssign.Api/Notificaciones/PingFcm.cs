namespace SmartAssign.Api.Notificaciones;

/// <summary>
/// La "campana vacía" de D5/05 §2.5 — el ÚNICO objeto que de verdad sale
/// hacia FCM. Un solo campo, deliberadamente: <see cref="E"/>, el id
/// opaco de <c>Notificacion</c> (como texto). Ni nombre, ni ficha, ni
/// línea, ni puesto — el ejemplo literal de la fuente es
/// <c>{"data": {"e": "a91f3c"}}</c>. E12.5 construye la prueba dedicada
/// que falla el build si este contrato alguna vez gana un campo de
/// negocio; esta UT solo lo define y lo usa.
/// </summary>
public record PingFcm(string E);

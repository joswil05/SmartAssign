namespace SmartAssign.Application.Tiempo;

/// <summary>
/// El "hoy" de la planta — 00 §C6: <i>"la hora es siempre la del servidor.
/// El reloj del dispositivo no se usa para ninguna decisión"</i>.
///
/// <b>Por qué existe esta clase en vez de llamar a DateTime donde haga
/// falta.</b> Hasta la revisión de producción, dos endpoints calculaban el
/// día con <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>. Con el servidor
/// en UTC−6 eso devuelve la fecha de MAÑANA desde las 18:00 hasta
/// medianoche, y esa fecha se comparaba contra <c>RestriccionMedica</c>,
/// que es la regla dura del §7.2. Una restricción que vencía hoy dejaba de
/// bloquear seis horas antes de tiempo, todos los días.
///
/// <b>Un instante no es una fecha.</b> Las marcas de tiempo siguen siendo
/// UTC en toda la base (725 usos de <c>SYSUTCDATETIME()</c>) y así deben
/// seguir: un instante es absoluto. Pero un DÍA DE CALENDARIO —el día de
/// operación (C6), la vigencia de un dictamen médico, la caducidad de un
/// descarte (B10)— es una fecha de la planta, y la planta no vive en UTC.
///
/// El espejo en SQL es <c>dbo.fn_FechaPlanta()</c>. Las dos tienen que dar
/// lo mismo, y <c>FechaDePlantaTests</c> lo comprueba.
///
/// <b>Requisito de despliegue:</b> el reloj del servidor de planta va en la
/// zona horaria de la planta. <c>GET /api/servidor/salud</c> publica el
/// desfase que ve, para que un servidor puesto en UTC se detecte antes de
/// que decida sobre nadie.
/// </summary>
public static class FechaPlanta
{
    /// <summary>El día de calendario en curso, en hora de la planta.</summary>
    public static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Desfase del reloj del servidor respecto de UTC, para diagnóstico.</summary>
    public static TimeSpan DesfaseUtc() => TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
}

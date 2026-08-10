namespace SmartAssign.Domain.Entities;

/// <summary>
/// Catálogo de turnos (00 §C6). <see cref="HoraInicio"/>/<see cref="HoraFin"/>
/// son el dato de configuración del cliente — se siembra **vacío** a
/// propósito: el archivo real trae códigos de turno en la hoja "Programa"
/// (1T8, 2T8, 1T12, 2T12 — turno y duración en horas) pero ningún reloj
/// concreto, y C6 prohíbe inventar una hora. El Coordinador crea las filas
/// reales desde la aplicación cuando el cliente confirme los horarios.
/// Ver docs/04_ESQUEMA_BACKEND.md §4.1.
/// </summary>
public class Turno
{
    public byte Id { get; set; }
    public string Nombre { get; set; } = default!;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    /// <summary>
    /// Columna calculada en la base (<c>hora_fin &lt;= hora_inicio</c>).
    /// Un turno que cruza medianoche pertenece ENTERO a su fecha de
    /// inicio (00 §C6) — <see cref="JornadaLinea.DiaOperacion"/> nunca se
    /// recalcula por esto, es la fecha con la que se planificó.
    /// </summary>
    public bool CruzaMedianoche { get; private set; }

    public bool Activo { get; set; } = true;
}

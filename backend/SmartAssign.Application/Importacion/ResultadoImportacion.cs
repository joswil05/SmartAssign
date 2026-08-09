namespace SmartAssign.Application.Importacion;

/// <summary>Un problema en una fila concreta — nunca detiene la lectura, se acumula para el informe completo.</summary>
public record ErrorImportacion(int Fila, string Columna, string Mensaje);

/// <summary>
/// 07 §4.3 / docs/PROGRESO.md UT-E3.6: "fila inválida → se rechaza el
/// lote entero, con informe". Todo o nada por hoja — nunca una carga a
/// medias que deje el padrón en un estado que nadie decidió.
/// </summary>
public record ResultadoImportacion
{
    public bool Exitoso { get; init; }
    public int FilasLeidas { get; init; }
    public int FilasImportadas { get; init; }
    public IReadOnlyList<ErrorImportacion> Errores { get; init; } = [];

    public static ResultadoImportacion Rechazo(int filasLeidas, IReadOnlyList<ErrorImportacion> errores) => new()
    {
        Exitoso = false,
        FilasLeidas = filasLeidas,
        FilasImportadas = 0,
        Errores = errores,
    };

    public static ResultadoImportacion Exito(int filasLeidas, int filasImportadas) => new()
    {
        Exitoso = true,
        FilasLeidas = filasLeidas,
        FilasImportadas = filasImportadas,
    };
}

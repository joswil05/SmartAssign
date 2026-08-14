namespace SmartAssign.Application.DatosSimulados;

/// <summary>Una tabla vigilada y cuántas filas no reales tiene ahora mismo.</summary>
public record ConteoPorTabla(string Tabla, int Filas);

/// <summary>Quién apunta a una fila simulada, y desde qué columna (por qué la purga se niega).</summary>
public record BloqueoDePurga(string Tabla, string Columna, string ApuntaA, int Filas);

/// <summary>
/// Resultado de <c>sp_VerificarSinDatosSimulados</c>. <c>EstaLimpia</c> es
/// la respuesta que 07 §4.4 pide de una base de producción: ni una sola
/// fila simulada. El desglose viene entero, con los ceros, para que se
/// vea qué se vigiló y no solo qué falló.
///
/// <c>FilasPlaceholder</c> va aparte de <c>FilasSimuladas</c> porque son
/// dos problemas distintos con dos arreglos distintos: las filas simuladas
/// se BORRAN (la purga), y el vocabulario de capacidades físicas se
/// REEMPLAZA cuando llegue H6. Sumarlos escondería que uno de los dos no
/// se arregla con <c>PurgarAsync</c>.
/// </summary>
public record ResultadoVerificacionSimulados(
    int FilasSimuladas, int FilasPlaceholder, IReadOnlyList<ConteoPorTabla> Detalle,
    string? CodigoRechazo, string? Mensaje)
{
    /// <summary>Ni filas fabricadas ni catálogo de desarrollo: lista para producción.</summary>
    public bool EstaLimpia => CodigoRechazo is null && FilasSimuladas == 0 && FilasPlaceholder == 0;

    /// <summary>Lo que sí arregla <c>PurgarAsync</c> — el resto necesita H6.</summary>
    public bool SinFilasSimuladas => FilasSimuladas == 0;
}

/// <summary>
/// Resultado de <c>sp_PurgarDatosSimulados</c>. Con
/// <c>CodigoRechazo = "PURGA_BLOQUEADA"</c>, <c>Bloqueos</c> trae la lista
/// exacta de quién impide la limpieza — nunca un rechazo genérico
/// (§1.3, §12.4), mismo criterio que <c>sp_CerrarTurno</c>.
/// </summary>
public record ResultadoPurgaSimulados(
    int FilasPurgadas, IReadOnlyList<ConteoPorTabla> Detalle, IReadOnlyList<BloqueoDePurga> Bloqueos,
    string? CodigoRechazo, string? Mensaje);

/// <summary>
/// UT-E14.7 (07 §4.3, §4.4, §9): el par verificar/purgar que separa lo
/// simulado de producción. Los datos reales entran por
/// <c>IImportadorDatosReales</c> (E3.6, tarea H5 del cliente); esto es la
/// otra mitad — comprobar que no queda nada fabricado, y quitarlo cuando
/// aún lo hay.
/// </summary>
public interface IServicioDatosSimulados
{
    Task<ResultadoVerificacionSimulados> VerificarAsync(CancellationToken ct = default);
    Task<ResultadoPurgaSimulados> PurgarAsync(CancellationToken ct = default);
}

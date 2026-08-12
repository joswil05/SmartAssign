using SmartAssign.Api.Endpoints;

namespace SmartAssign.Api.TiempoReal;

/// <summary>
/// UT-E12.2 (docs/PROGRESO.md): el catálogo de eventos de 05_TRD.md §2.4,
/// tabla literal — un registro por evento, con exactamente el "Contenido"
/// que la tabla declara, ni un campo más.
///
/// Deliberadamente **sin envío real todavía**: 05 §4.1 dice que el patrón
/// Bandeja de salida "garantiza que el evento se emite si y solo si la
/// transacción confirmó" — eso es E12.3. Emitir ya mismo desde los SP/
/// servicios existentes rompería exactamente esa garantía (C4 punto 1:
/// "emite un evento... DENTRO de la misma transacción" — un `SendAsync`
/// de SignalR no es transaccional con SQL). Esta UT define el CONTRATO;
/// quien construya la bandeja de salida en E12.3 lo usa para serializar.
///
/// Cada tipo trae solo los campos que su fila de la tabla autoriza —
/// nunca de más (R2): añadir un campo no pedido a <see cref="AvisoFatigaPlantaEvento"/>
/// o a <see cref="RelevoEnColaEvento"/> sería exactamente la fuga de
/// identidad que 00_DECISIONES.md §D1/§D2 prohíben; hay pruebas que lo
/// verifican por reflexión (<c>CatalogoDeEventosTests</c>).
/// </summary>
public static class Eventos
{
    /// <summary>
    /// <c>PuestoActualizado</c> → <c>linea:{id}</c>. Contenido literal de
    /// la tabla: "Estado, ocupante, fatiga, micro-copia" — mismo shape que
    /// <c>LineaEndpoints.PuestoMallaRespuesta</c> (E6.4), sin duplicar
    /// <c>Codigo</c>/<c>NombrePuesto</c> que el cliente ya tiene cacheados
    /// desde la carga de la malla.
    /// </summary>
    public record PuestoActualizadoEvento(
        int PuestoId, string Situacion, LineaEndpoints.OcupanteRespuesta? Ocupante,
        string? NivelFatiga, decimal? ExcesoFatiga, string MicroCopia);

    /// <summary>
    /// <c>FatigaAvanzada</c> → <c>linea:{id}</c>. Contenido literal:
    /// "Exceso relativo (A4)" — <c>fn_ExcesoRelativoFatiga</c> (E7.2).
    /// </summary>
    public record FatigaAvanzadaEvento(int PuestoId, decimal ExcesoRelativo);

    /// <summary>
    /// <c>AvisoFatigaPlanta</c> → <c>avisos</c>. 00 §D2, literal:
    /// <c>"L4 · Puesto 3 — relevo sugerido · 62 min"</c>, "ninguna
    /// identidad de persona". <see cref="Texto"/> es el ÚNICO campo a
    /// propósito — compuesto por <c>fn_TextoAvisoFatiga</c> (E9.3), la
    /// misma función que ese UT ya dejó lista "para cuando exista" esta
    /// etapa. Un supervisor que lo abre no debe poder llegar a más datos
    /// que el propio texto (§D2: "no obtiene más detalle... al abrirlo").
    /// </summary>
    public record AvisoFatigaPlantaEvento(string Texto);

    /// <summary>
    /// <c>RelevoEnCola</c> → <c>bolson</c>. 00 §D1, literal — el mínimo
    /// estricto que la L8 ve de un puesto ajeno: línea + puesto, tipo,
    /// fatiga, capacidades que exige, perfil preferente si lo declara.
    /// Nunca nombre/ficha/foto ni restricciones médicas de personal ajeno
    /// (§D1: "el emparejamiento lo hace el servidor; la L8 recibe el
    /// resultado, no los insumos").
    /// </summary>
    public record RelevoEnColaEvento(
        byte LineaId, int PuestoId, string TipoPuesto, string? NivelFatiga, decimal? ExcesoRelativo,
        IReadOnlyList<string> CapacidadesRequeridas, string? PerfilPreferente);

    /// <summary>
    /// <c>TransitoEntrante</c> → <c>linea:{id}</c> (la línea destino).
    /// Contenido literal: "Nombre — el destino sí lo ve, va a recibirla".
    /// A propósito lleva identidad — es la única línea de la tabla donde
    /// la fuente lo pide explícitamente, contraste deliberado con
    /// <see cref="AvisoFatigaPlantaEvento"/>/<see cref="RelevoEnColaEvento"/>.
    /// </summary>
    public record TransitoEntranteEvento(long MovimientoId, int PersonalId, string NombreCompleto, byte LineaOrigen, byte LineaDestino);

    /// <summary>
    /// <c>TransitoDemorado</c> → <c>linea:{id}</c>, <c>bolson</c>, <c>planta</c>.
    /// 00 §B11, literal: al vencer <c>duracion_maxima_transito</c>, alerta
    /// a origen, destino y Coordinador; el puesto queda "Relevista demorado".
    /// </summary>
    public record TransitoDemoradoEvento(long MovimientoId, int PersonalId, byte LineaOrigen, byte LineaDestino, int MinutosEnTransito);

    /// <summary>
    /// <c>EstadisticaActualizada</c> → <c>linea:{id}</c>, <c>planta</c>.
    /// Contenido literal: "Recalculada en servidor (C4)" — exactamente lo
    /// que <c>sp_CalcularEficiencia</c> (E11.7/E11.8) ya devuelve; no se
    /// añaden campos de desperdicio/cobertura/fatiga agregados que ningún
    /// procedimiento calcula todavía (E11.8, hueco conocido de la fuente).
    /// </summary>
    public record EstadisticaActualizadaEvento(
        byte LineaId, decimal? EficienciaPct, string? Tramo, decimal ProduccionReal,
        int TiempoEfectivoMarchaMin, int ParosAcumuladosMin, DateTime? UltimaActualizacionProduccion);

    /// <summary>
    /// <c>ParoIniciado</c> → <c>linea:{id}</c>, <c>planta</c>. §11.1 — mismos
    /// datos que <c>sp_RegistrarParo</c> (E11.1) ya persiste.
    /// </summary>
    public record ParoIniciadoEvento(int ParoId, byte LineaId, string Categoria, string Causa, string Descripcion);

    /// <summary>
    /// <c>ParoReanudado</c> → <c>linea:{id}</c>, <c>planta</c>. §11.1 —
    /// <c>sp_ReanudarProduccion</c> (E11.1) ya lo cierra; el minuto exacto
    /// que duró el paro es dato ya persistido en <c>Paro.fin - Paro.inicio</c>,
    /// no se recalcula aquí.
    /// </summary>
    public record ParoReanudadoEvento(int ParoId, byte LineaId);

    /// <summary>
    /// <c>AlertaCoordinador</c> → <c>planta</c>. Contenido literal:
    /// "Escalados, planta agotada, supervisor no localizable" — tres
    /// escenarios que E12.6 ("Acuse, escalado y 'supervisor no
    /// localizable'") todavía no construye. Se deja deliberadamente
    /// genérico (un mensaje ya compuesto, sin un enum de "Tipo" con
    /// valores que nadie ha definido todavía) para no inventar ahora la
    /// taxonomía exacta que esa UT decidirá — hueco conocido, no de esta UT.
    /// </summary>
    public record AlertaCoordinadorEvento(string Mensaje);
}

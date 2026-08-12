namespace SmartAssign.Api.TiempoReal;

/// <summary>
/// A qué clase de grupo va cada evento — abstracto porque
/// <see cref="TipoGrupo.LineaDelEvento"/> depende de qué línea trae el
/// propio evento (nunca un nombre de grupo fijo); resuelto a la cadena
/// real de <see cref="NombresDeGrupo"/> con <see cref="CatalogoEventos.NombresDeGrupoPara"/>.
/// </summary>
public enum TipoGrupo
{
    LineaDelEvento,
    Planta,
    Bolson,
    Avisos,
}

/// <summary>
/// UT-E12.2: la columna "Grupo" de la tabla de 05_TRD.md §2.4, transcrita
/// tal cual — una sola fuente de verdad para "a quién le llega este
/// evento", que tanto la futura bandeja de salida (E12.3) como cualquier
/// prueba pueden consultar sin repetir la tabla.
/// </summary>
public static class CatalogoEventos
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<TipoGrupo>> GruposPorEvento =
        new Dictionary<string, IReadOnlyList<TipoGrupo>>
        {
            [nameof(Eventos.PuestoActualizadoEvento)] = [TipoGrupo.LineaDelEvento],
            [nameof(Eventos.FatigaAvanzadaEvento)] = [TipoGrupo.LineaDelEvento],
            [nameof(Eventos.AvisoFatigaPlantaEvento)] = [TipoGrupo.Avisos],
            [nameof(Eventos.RelevoEnColaEvento)] = [TipoGrupo.Bolson],
            [nameof(Eventos.TransitoEntranteEvento)] = [TipoGrupo.LineaDelEvento],
            [nameof(Eventos.TransitoDemoradoEvento)] = [TipoGrupo.LineaDelEvento, TipoGrupo.Bolson, TipoGrupo.Planta],
            [nameof(Eventos.EstadisticaActualizadaEvento)] = [TipoGrupo.LineaDelEvento, TipoGrupo.Planta],
            [nameof(Eventos.ParoIniciadoEvento)] = [TipoGrupo.LineaDelEvento, TipoGrupo.Planta],
            [nameof(Eventos.ParoReanudadoEvento)] = [TipoGrupo.LineaDelEvento, TipoGrupo.Planta],
            [nameof(Eventos.AlertaCoordinadorEvento)] = [TipoGrupo.Planta],
        };

    /// <summary>
    /// Resuelve los <see cref="TipoGrupo"/> de un evento a los nombres de
    /// grupo reales que <c>PlantaHub</c> usó en <c>Groups.AddToGroupAsync</c>.
    /// <paramref name="lineaId"/> es obligatorio solo si el evento incluye
    /// <see cref="TipoGrupo.LineaDelEvento"/> — pedirlo sin necesitarlo, o
    /// necesitarlo sin que lo pasen, es un error de quien llama, no un caso
    /// silencioso (nunca se manda un evento "a nadie" por un parámetro
    /// olvidado).
    /// </summary>
    public static IReadOnlyList<string> NombresDeGrupoPara(string nombreEvento, byte? lineaId = null)
    {
        if (!GruposPorEvento.TryGetValue(nombreEvento, out var tipos))
            throw new ArgumentOutOfRangeException(nameof(nombreEvento), nombreEvento, "Evento no está en el catálogo de 05 §2.4.");

        return tipos.Select(tipo => tipo switch
        {
            TipoGrupo.LineaDelEvento => NombresDeGrupo.DeLinea(lineaId
                ?? throw new ArgumentNullException(nameof(lineaId), $"{nombreEvento} necesita la línea para resolver su grupo.")),
            TipoGrupo.Planta => NombresDeGrupo.Planta,
            TipoGrupo.Bolson => NombresDeGrupo.Bolson,
            TipoGrupo.Avisos => NombresDeGrupo.Avisos,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
        }).ToList();
    }
}

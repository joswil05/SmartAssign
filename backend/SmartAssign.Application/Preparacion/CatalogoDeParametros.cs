namespace SmartAssign.Application.Preparacion;

/// <summary>
/// Un parámetro de planta y, sobre todo, <b>qué deja de ocurrir mientras
/// no esté configurado</b>.
/// </summary>
/// <param name="Clave">La clave tal como la lee el procedimiento.</param>
/// <param name="ReglaDormida">
/// Lo que el sistema NO hace sin este valor, en términos de operación, no
/// de código. Es la parte que importa: un parámetro ausente no da error,
/// simplemente apaga una regla en silencio.
/// </param>
/// <param name="TieneValorPorDefecto">
/// Si el procedimiento aplica un valor propio cuando falta. Solo dos lo
/// hacen; el resto se quedan sin regla, que es lo correcto según R2 —
/// nunca se inventa un umbral de negocio.
/// </param>
public record ParametroDePlanta(string Clave, string ReglaDormida, bool TieneValorPorDefecto = false);

/// <summary>
/// Revisión de producción, hallazgo <b>P-04</b>. Los parámetros se siembran
/// vacíos a propósito (07 §2.1 R2, 06 §P5.1: <i>"NO inventes umbrales por
/// defecto"</i>) y eso está bien. Lo que faltaba era que <b>alguien avisara
/// de cuáles faltan y qué queda apagado por eso</b>: sin este catálogo, una
/// planta puede arrancar creyendo que la fatiga se vigila y que las
/// notificaciones críticas escalan, cuando ninguna de las dos cosas ocurre.
///
/// Este catálogo <b>no propone valores</b>. Solo nombra el hueco. Los
/// números los decide el cliente, y siguen entrando por
/// <c>POST /api/maestros/parametros</c> o por la tabla.
/// </summary>
public static class CatalogoDeParametros
{
    public static readonly IReadOnlyList<ParametroDePlanta> Todos =
    [
        new("ventana_arranque_min",
            "La ventana de arranque nunca se cierra: ventana_arranque_fin queda nula y el paso 7 de sp_ValidarAsignacion no bloquea a nadie por llegar tarde."),

        new("fatiga_sugerido_default_min",
            "Los puestos sin tiempo propio en el dato real (los fijos) no acumulan fatiga sugerida. Los rotativos sí: traen su tiempo del archivo del cliente."),

        new("fatiga_critico_default_min",
            "Lo mismo para el umbral crítico: sin él, un puesto fijo nunca llega a fatiga crítica."),

        new("notificacion_acuse_timeout_min",
            "Ninguna notificación crítica escala nunca. \"Supervisor no localizable\" (00 §D5) no se dispara en ningún caso."),

        new("eficiencia_umbral_aceptable_pct",
            "La eficiencia se calcula pero no se clasifica: los paneles no muestran tramo."),

        new("eficiencia_umbral_optimo_pct",
            "Igual que el anterior: sin los dos umbrales no hay clasificación posible."),

        new("umbral_desperdicio_justificacion_pct",
            "El desperdicio nunca exige justificación, por alto que sea el porcentaje."),

        new("minimo_operarios_default",
            "Las líneas sin mínimo propio se quedan sin piso de seguridad (00 §B5) al evaluar una extracción."),

        new("factor_doble_turno",
            "Sin configurar, la fatiga de quien encadena doble turno se cuenta igual que la de cualquiera (factor 1.0).",
            TieneValorPorDefecto: true),

        new("duracion_maxima_transito",
            "Sin configurar, un tránsito caduca a los 15 minutos.",
            TieneValorPorDefecto: true),
    ];

    /// <summary>Los que dejan una regla completamente apagada si faltan.</summary>
    public static IEnumerable<ParametroDePlanta> SinValorPorDefecto =>
        Todos.Where(p => !p.TieneValorPorDefecto);
}

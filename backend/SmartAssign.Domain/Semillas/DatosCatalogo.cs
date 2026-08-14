using SmartAssign.Domain.Entities;

namespace SmartAssign.Domain.Semillas;

/// <summary>
/// Datos "catálogo, editable" (04_ESQUEMA_BACKEND.md §11.3): van a
/// producción como punto de partida, pero el Coordinador puede ampliarlos
/// desde la aplicación (§2.1.10) — a diferencia de los estructurales, no
/// son inmutables.
///
/// El vocabulario de <see cref="Capacidades"/> es un PLACEHOLDER hasta que
/// el cliente aporte el vocabulario real acordado con Enfermería (G2, H6
/// del plan de ejecución). Sirve para construir y probar el mecanismo de
/// la regla médica — nunca se usa para decidir sobre una persona real.
/// </summary>
public static class DatosCatalogo
{
    public static CapacidadFisica[] Capacidades() =>
    [
        // origen_dato = 'simulado' (por defecto, ver CapacidadFisicaConfig):
        // estas seis palabras las escribió el desarrollo para poder probar
        // la regla médica, no Enfermería. Mientras sigan marcadas así,
        // sp_VerificarSinDatosSimulados se niega a dar la base por lista
        // para producción — es H6 convertido en una comprobación y no en
        // una línea de una tabla de bloqueos.
        new() { Id = 1, Codigo = "levantar_carga", Nombre = "Levantar carga" },
        new() { Id = 2, Codigo = "bipedestacion_prolongada", Nombre = "Bipedestación prolongada" },
        new() { Id = 3, Codigo = "movimiento_repetitivo_mano", Nombre = "Movimiento repetitivo de mano" },
        new() { Id = 4, Codigo = "esfuerzo_lumbar", Nombre = "Esfuerzo lumbar" },
        new() { Id = 5, Codigo = "exposicion_quimicos", Nombre = "Exposición a químicos" },
        new() { Id = 6, Codigo = "trabajo_en_altura", Nombre = "Trabajo en altura" },
    ];

    public static MotivoExcepcion[] MotivosExcepcion() =>
    [
        new() { Id = 1, Nombre = "Acuerdo directo con el trabajador" },
        new() { Id = 2, Nombre = "Extracción de Operador B por vacante crítica" },
        new() { Id = 3, Nombre = "Forzar cierre de turno" },
        new() { Id = 4, Nombre = "Forzar por debajo del piso de seguridad" },
        new() { Id = 5, Nombre = "Saltar ventana de arranque" },
        new() { Id = 6, Nombre = "Cancelar tránsito caducado" },
        new() { Id = 7, Nombre = "Asignación manual de personal de liderazgo" },
    ];

    public static MotivoRechazoRecepcion[] MotivosRechazoRecepcion() =>
    [
        new() { Id = 1, Nombre = "La persona no se presentó" },
        new() { Id = 2, Nombre = "Persona incorrecta" },
        new() { Id = 3, Nombre = "El puesto ya no requiere relevo" },
        new() { Id = 4, Nombre = "Otro" },
    ];

    public static CategoriaParo[] CategoriasParo() =>
    [
        new() { Id = 1, Nombre = "Mecánico" },
        new() { Id = 2, Nombre = "Eléctrico" },
        new() { Id = 3, Nombre = "Calidad" },
        new() { Id = 4, Nombre = "Falta de material" },
    ];

    // Causas ilustrativas — el catálogo real (dato D10 del plan de
    // ejecución) se carga en la etapa E11, no aquí.
    public static CausaParo[] CausasParo() =>
    [
        new() { Id = 1, CategoriaId = 1, Nombre = "Avería de máquina" },
        new() { Id = 2, CategoriaId = 1, Nombre = "Ajuste mecánico" },
        new() { Id = 3, CategoriaId = 2, Nombre = "Corte de energía" },
        new() { Id = 4, CategoriaId = 3, Nombre = "Producto fuera de especificación" },
        new() { Id = 5, CategoriaId = 4, Nombre = "Desabasto de insumo" },
    ];
}

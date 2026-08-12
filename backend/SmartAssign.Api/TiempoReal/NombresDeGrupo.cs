namespace SmartAssign.Api.TiempoReal;

/// <summary>
/// Los cuatro nombres de grupo de <c>PlantaHub</c> (05_TRD.md §2.4),
/// en un solo lugar — <see cref="Hubs.PlantaHub"/> (quién se une) y
/// <see cref="CatalogoEventos"/> (a quién se le manda cada evento, E12.2)
/// tienen que estar de acuerdo en la cadena exacta; declararla dos veces
/// arriesga que un día diverjan.
/// </summary>
public static class NombresDeGrupo
{
    public const string Planta = "planta";
    public const string Bolson = "bolson";
    public const string Avisos = "avisos";

    public static string DeLinea(byte lineaId) => $"linea:{lineaId}";
}

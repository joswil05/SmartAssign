namespace SmartAssign.Domain.Entities;

/// <summary>
/// Exactamente dos roles (Parte II): coordinador o supervisor. El
/// supervisor de la L8 NO es un tercer rol — sus capacidades adicionales
/// se derivan de que su línea asignada tiene <see cref="Linea.EsBolson"/>
/// en true, nunca de un permiso distinto (04 §6.1).
///
/// La línea de un supervisor NUNCA vive aquí ni en el token (§2.3, D6):
/// se resuelve en cada petición desde <see cref="Linea.SupervisorActualId"/>.
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = default!;
    public string NombreCompleto { get; set; } = default!;

    /// <summary>"coordinador" | "supervisor" (04 §6.1).</summary>
    public string Rol { get; set; } = default!;

    /// <summary>"ad" | "local" (D6). Hoy solo hay implementación local — ver ServicioAutenticacion.</summary>
    public string OrigenIdentidad { get; set; } = "local";

    /// <summary>Solo si OrigenIdentidad = "local" (D6).</summary>
    public byte[]? PasswordHash { get; set; }
    public byte[]? PasswordSalt { get; set; }

    /// <summary>PIN de 4-6 dígitos para reentrar durante el turno sin repetir credenciales (D6).</summary>
    public byte[]? PinHash { get; set; }
    public byte[]? PinSalt { get; set; }

    /// <summary>
    /// FK a Personal(Id) — la tabla Personal no existe hasta la etapa E3.
    /// Vincula al usuario con su ficha cuando además es personal de planta
    /// (liderazgo). Se deja sin restricción de clave foránea hasta entonces.
    /// </summary>
    public int? PersonalId { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime? BloqueadoHasta { get; set; }
    public byte IntentosFallidos { get; set; }
}

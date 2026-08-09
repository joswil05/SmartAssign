namespace SmartAssign.Domain.Entities;

/// <summary>
/// Un refresh token vivo, ligado a un usuario y a un dispositivo concreto
/// (D6, 04 §6.1). El teléfono se trata como compartido por línea: la
/// sesión identifica al usuario que hoy lo tiene en la mano, no al
/// teléfono en sí.
/// </summary>
public class SesionDispositivo
{
    public Guid Id { get; set; }
    public int UsuarioId { get; set; }
    public string DeviceId { get; set; } = default!;

    /// <summary>Nunca se guarda el refresh token en claro, solo su hash (04 §6.1).</summary>
    public byte[] RefreshTokenHash { get; set; } = default!;

    public DateTime EmitidoEn { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
    public DateTime UltimaActividad { get; set; }

    public Usuario Usuario { get; set; } = default!;
}

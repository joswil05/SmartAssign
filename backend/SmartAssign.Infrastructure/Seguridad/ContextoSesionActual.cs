using SmartAssign.Application.Seguridad;

namespace SmartAssign.Infrastructure.Seguridad;

/// <summary>Registrado como Scoped: una instancia por petición HTTP (o por prueba).</summary>
public class ContextoSesionActual : IContextoSesionActual
{
    public string? Rol { get; private set; }
    public byte? LineaId { get; private set; }

    public void Establecer(string rol, byte? lineaId)
    {
        Rol = rol;
        LineaId = lineaId;
    }
}

namespace SmartAssign.Application.Autenticacion;

/// <summary>
/// Hashing de contraseña y PIN (D6). PBKDF2 con sal propia por usuario
/// para credenciales de baja entropía elegidas por una persona; hash
/// simple sin sal para el refresh token opaco, que ya es un secreto de
/// alta entropía generado por el servidor — salarlo no añade seguridad.
/// </summary>
public interface IServicioCredenciales
{
    (byte[] Hash, byte[] Salt) HashConSal(string valorPlano);
    bool Verificar(string valorPlano, byte[] hash, byte[] salt);
    byte[] HashOpaco(string valorPlano);
}

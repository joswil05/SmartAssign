using System.Security.Cryptography;
using System.Text;
using SmartAssign.Application.Autenticacion;

namespace SmartAssign.Infrastructure.Autenticacion;

/// <summary>
/// PBKDF2-SHA256 (100 000 iteraciones, sal de 16 bytes, hash de 32 bytes
/// — ambos caben en las columnas VARBINARY declaradas en 04 §6.1) para
/// contraseña y PIN. SHA-256 simple para el refresh token opaco: ya es un
/// secreto de 256 bits generado por el servidor, no un valor de baja
/// entropía elegido por una persona — salarlo no aporta nada.
/// </summary>
public class ServicioCredenciales : IServicioCredenciales
{
    private const int Iteraciones = 100_000;
    private const int TamanoSalBytes = 16;
    private const int TamanoHashBytes = 32;

    public (byte[] Hash, byte[] Salt) HashConSal(string valorPlano)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanoSalBytes);
        var hash = Derivar(valorPlano, salt);
        return (hash, salt);
    }

    public bool Verificar(string valorPlano, byte[] hash, byte[] salt)
    {
        var calculado = Derivar(valorPlano, salt);
        return CryptographicOperations.FixedTimeEquals(calculado, hash);
    }

    public byte[] HashOpaco(string valorPlano) => SHA256.HashData(Encoding.UTF8.GetBytes(valorPlano));

    private static byte[] Derivar(string valorPlano, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(valorPlano), salt, Iteraciones, HashAlgorithmName.SHA256, TamanoHashBytes);
}

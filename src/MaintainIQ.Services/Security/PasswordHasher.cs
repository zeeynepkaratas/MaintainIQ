using System.Security.Cryptography;
using System.Text;
using MaintainIQ.Domain.Common;

namespace MaintainIQ.Services.Security;

public sealed record PasswordCredential(string Hash, string Salt);

public interface IPasswordHasher
{
    PasswordCredential Hash(string password);
    bool Verify(string password, string hash, string salt);
}

public sealed class PasswordHasher : IPasswordHasher
{
    public const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public PasswordCredential Hash(string password)
    {
        ValidatePassword(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt);
        return new(Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            var expected = Convert.FromBase64String(hash);
            var saltBytes = Convert.FromBase64String(salt);
            var actual = Derive(password, saltBytes);
            return expected.Length == actual.Length
                && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new DomainValidationException("Parola en az 8 karakter olmalıdır.");
        }
    }
}

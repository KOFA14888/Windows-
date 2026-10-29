using System.Security.Cryptography;

namespace Win10Sim.Core.Accounts;

/// <summary>
/// Хэширование паролей учётных записей.
///
/// В state.json никогда не попадает открытый пароль: только соль, число
/// итераций и PBKDF2-хэш (HMAC-SHA256). Это не «настоящая» Windows (там
/// используется NTLM/DPAPI), но правильная схема хранения секретов —
/// и её видно в файле состояния, что важно для демонстрации.
/// </summary>
public static class PasswordHasher
{
    /// <summary>Рекомендация OWASP (2023) для PBKDF2-HMAC-SHA256 — 600 000 итераций.</summary>
    public const int DefaultIterations = 600_000;

    private const int SaltSize = 16;
    private const int HashSize = 32;

    public readonly record struct HashResult(string HashBase64, string SaltBase64, int Iterations);

    public static HashResult Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1000);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return new HashResult(Convert.ToBase64String(hash), Convert.ToBase64String(salt), iterations);
    }

    /// <summary>
    /// Проверка пароля. Сравнение — с постоянным временем выполнения
    /// (<see cref="CryptographicOperations.FixedTimeEquals"/>), чтобы по времени
    /// ответа нельзя было подбирать пароль побайтово.
    /// </summary>
    public static bool Verify(string password, string hashBase64, string saltBase64, int iterations)
    {
        if (string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(saltBase64) || iterations < 1000)
            return false;

        byte[] expected;
        byte[] salt;

        try
        {
            expected = Convert.FromBase64String(hashBase64);
            salt = Convert.FromBase64String(saltBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

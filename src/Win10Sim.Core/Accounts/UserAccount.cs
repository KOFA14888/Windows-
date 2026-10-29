using System.Text.Json.Serialization;

namespace Win10Sim.Core.Accounts;

/// <summary>
/// Учётная запись, созданная в OOBE. Хранится в state.json в том же виде,
/// в каком её увидит экран блокировки на ЭТАПЕ 3: имя, отображаемое имя,
/// соль и хэш пароля, контрольный вопрос.
/// </summary>
public sealed class UserAccount
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>PBKDF2-хэш (base64). Пустая строка — вход без пароля.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;
    public int PasswordIterations { get; set; }

    public string? SecurityQuestion { get; set; }
    public string? SecurityAnswerHash { get; set; }
    public string? SecurityAnswerSalt { get; set; }
    public int SecurityAnswerIterations { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    [JsonIgnore]
    public string PictureGlyph => "\uE77B";          // Segoe MDL2: Contact

    /// <summary>Профиль пользователя на «диске C:» — как в настоящей Windows.</summary>
    [JsonIgnore]
    public string ProfilePath => $@"C:\Users\{UserName}";

    public void SetPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            PasswordHash = string.Empty;
            PasswordSalt = string.Empty;
            PasswordIterations = 0;
            return;
        }

        PasswordHasher.HashResult result = PasswordHasher.Hash(password);
        PasswordHash = result.HashBase64;
        PasswordSalt = result.SaltBase64;
        PasswordIterations = result.Iterations;
    }

    public bool CheckPassword(string password)
        => HasPassword && PasswordHasher.Verify(password, PasswordHash, PasswordSalt, PasswordIterations);

    public void SetSecurityAnswer(string question, string answer)
    {
        SecurityQuestion = question;

        if (string.IsNullOrWhiteSpace(answer))
        {
            SecurityAnswerHash = null;
            SecurityAnswerSalt = null;
            SecurityAnswerIterations = 0;
            return;
        }

        // Ответ сравнивается без учёта регистра и пробелов — как в Windows.
        PasswordHasher.HashResult result = PasswordHasher.Hash(NormalizeAnswer(answer));
        SecurityAnswerHash = result.HashBase64;
        SecurityAnswerSalt = result.SaltBase64;
        SecurityAnswerIterations = result.Iterations;
    }

    public bool CheckSecurityAnswer(string answer)
        => !string.IsNullOrEmpty(SecurityAnswerHash) &&
           PasswordHasher.Verify(NormalizeAnswer(answer), SecurityAnswerHash!, SecurityAnswerSalt!, SecurityAnswerIterations);

    private static string NormalizeAnswer(string answer) => answer.Trim().ToLowerInvariant();
}

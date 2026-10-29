using System.Buffers;

namespace Win10Sim.Core.Accounts;

public enum AccountValidationError
{
    None = 0,
    Empty,
    TooLong,
    InvalidCharacters,
    EndsWithDotOrSpace,
    ReservedName,
    AllDots,
    PasswordTooShort,
    PasswordMismatch,
    PasswordHintTooLong
}

public readonly record struct AccountValidation(
    bool IsValid,
    AccountValidationError Error,
    string Preview);

public static class AccountValidator
{
    public const int MaxUserNameLength = 20;          // ограничение локальной учётной записи Windows
    public const int MinPasswordLength = 4;
    public const int MaxPasswordHintLength = 60;

    /// <summary>
    /// Запрещённые символы в имени учётной записи Windows:
    /// " / \ [ ] : ; | = , + * ? &lt; &gt; @
    /// SearchValues — кэшированный набор, поиск по нему заметно быстрее
    /// (и это единственный вариант, который не ругает анализатор CA1870).
    /// </summary>
    private static readonly SearchValues<char> ForbiddenCharacters =
        SearchValues.Create("\"/\\[]:;|=,+*?<>@");

    /// <summary>Зарезервированные имена DOS: CON, PRN, AUX, NUL, COM1..9, LPT1..9.</summary>
    private static readonly HashSet<string> ReservedNames = BuildReservedNames();

    /// <summary>
    /// Проверка имени учётной записи ровно в том виде, в каком его ввели.
    /// Ввод не «подчищается»: если пользователь поставил пробел или точку в
    /// конце, это ошибка (как в Windows), а не повод молча их удалить.
    /// </summary>
    public static AccountValidation ValidateUserName(string? userName)
    {
        string name = userName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            return new AccountValidation(false, AccountValidationError.Empty, name);

        if (name.Length > MaxUserNameLength)
            return new AccountValidation(false, AccountValidationError.TooLong, name);

        if (name.AsSpan().IndexOfAny(ForbiddenCharacters) >= 0)
            return new AccountValidation(false, AccountValidationError.InvalidCharacters, name);

        // «...» — это частный случай точки в конце, поэтому проверяется раньше:
        // пользователю полезнее узнать, что имя не может состоять из точек.
        if (name.All(c => c == '.'))
            return new AccountValidation(false, AccountValidationError.AllDots, name);

        if (name.EndsWith('.') || name.EndsWith(' '))
            return new AccountValidation(false, AccountValidationError.EndsWithDotOrSpace, name);

        // Windows регистронезависим для имён: "con" — тоже зарезервированное имя,
        // и "NUL.txt" тоже.
        string stem = name.Split('.')[0].ToUpperInvariant();
        if (ReservedNames.Contains(stem))
            return new AccountValidation(false, AccountValidationError.ReservedName, name);

        return new AccountValidation(true, AccountValidationError.None, name);
    }

    /// <summary>
    /// Пароль. Пустой пароль допустим в симуляторе (как «вход без пароля» в
    /// локальной учётной записи), но если он задан — минимум 4 символа.
    /// </summary>
    public static AccountValidation ValidatePassword(string? password, string? confirmation, bool allowEmpty = true)
    {
        string value = password ?? string.Empty;

        if (value.Length == 0)
            return allowEmpty
                ? new AccountValidation(true, AccountValidationError.None, string.Empty)
                : new AccountValidation(false, AccountValidationError.PasswordTooShort, string.Empty);

        if (value.Length < MinPasswordLength)
            return new AccountValidation(false, AccountValidationError.PasswordTooShort, string.Empty);

        if (!string.Equals(value, confirmation ?? string.Empty, StringComparison.Ordinal))
            return new AccountValidation(false, AccountValidationError.PasswordMismatch, string.Empty);

        return new AccountValidation(true, AccountValidationError.None, string.Empty);
    }

    public static AccountValidation ValidateHint(string? hint)
    {
        string value = (hint ?? string.Empty).Trim();

        return value.Length > MaxPasswordHintLength
            ? new AccountValidation(false, AccountValidationError.PasswordHintTooLong, value)
            : new AccountValidation(true, AccountValidationError.None, value);
    }

    private static HashSet<string> BuildReservedNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CLOCK$"
        };

        for (int i = 1; i <= 9; i++)
        {
            names.Add("COM" + i);
            names.Add("LPT" + i);
        }

        return names;
    }
}

namespace Win10Sim.Core.Setup;

public enum ProductKeyError
{
    None = 0,
    Empty,
    WrongLength,
    InvalidCharacters,
    InvalidKey
}

public readonly record struct ProductKeyValidation(
    bool IsValid,
    ProductKeyError Error,
    string NormalizedKey,
    WindowsEdition Edition);

/// <summary>
/// Проверка ключа продукта. Настоящая криптография ключей Windows (product key
/// algorithm) недоступна, поэтому правила приближены к наблюдаемому поведению:
///
///   * 25 символов, пять групп по пять;
///   * допустимый алфавит — base-24 без визуально неоднозначных символов;
///   * известные generic-ключи Windows 10 дают соответствующую редакцию;
///   * остальные корректные по форме ключи детерминированно (по хэшу) мапятся
///     на редакцию, чтобы поведение было воспроизводимо между запусками.
/// </summary>
public static class ProductKeyValidator
{
    public const int KeyLength = 25;
    public const int GroupSize = 5;

    /// <summary>Алфавит настоящих ключей: без 0/O, 1/I/L, 5/S, 8/B и т. п.</summary>
    private const string AllowedAlphabet = "23456789BCDFGHJKMPQRTVWXY";

    /// <summary>Generic-ключи Windows 10 (используются для установки без активации).</summary>
    private static readonly Dictionary<string, WindowsEdition> GenericKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["YTMG3-N6DKC-DKB77-7M9GH-8HVX7"] = WindowsEdition.Home,
        ["VK7JG-NPHTM-C97JM-9MPGT-3V66T"] = WindowsEdition.Pro,
        ["NPPR9-FWDCX-D2C8J-H872K-2YT43"] = WindowsEdition.Enterprise
    };

    /// <summary>Убирает пробелы и дефисы, приводит к верхнему регистру и разбивает на группы.</summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        Span<char> buffer = stackalloc char[KeyLength];
        int length = 0;

        foreach (char c in raw)
        {
            if (c is ' ' or '-' or '\t' or '\r' or '\n' or '\u00A0')
                continue;

            if (length == KeyLength)
                return raw.Trim().ToUpperInvariant();   // слишком длинный — вернём как есть, валидация отклонит

            buffer[length++] = char.ToUpperInvariant(c);
        }

        string flat = new string(buffer[..length]);
        if (flat.Length != KeyLength)
            return flat;

        return string.Create(KeyLength + 4, flat, static (span, source) =>
        {
            int s = 0;
            for (int group = 0; group < 5; group++)
            {
                if (group > 0)
                    span[s++] = '-';

                source.AsSpan(group * GroupSize, GroupSize).CopyTo(span[s..]);
                s += GroupSize;
            }
        });
    }

    /// <summary>
    /// Форматирование «на лету» для поля ввода: верхний регистр, только
    /// допустимые символы, автоматические дефисы каждые пять знаков.
    /// Возвращает строку ровно в том виде, в котором её нужно показать в TextBox.
    /// </summary>
    public static string FormatAsYouType(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var builder = new System.Text.StringBuilder(KeyLength + 4);
        int significant = 0;

        foreach (char c in input)
        {
            char upper = char.ToUpperInvariant(c);
            if (upper is '-' or ' ' or '\u00A0')
                continue;

            if (!AllowedAlphabet.Contains(upper, StringComparison.Ordinal))
                continue;                        // мусор просто не появляется в поле

            if (significant == KeyLength)
                break;

            if (significant > 0 && significant % GroupSize == 0)
                builder.Append('-');

            builder.Append(upper);
            significant++;
        }

        return builder.ToString();
    }

    /// <summary>Проверка введённого пользователем ключа.</summary>
    public static ProductKeyValidation Validate(string? raw)
    {
        string normalized = Normalize(raw);

        if (string.IsNullOrEmpty(normalized))
            return new ProductKeyValidation(false, ProductKeyError.Empty, string.Empty, WindowsEdition.Pro);

        string flat = normalized.Replace("-", string.Empty, StringComparison.Ordinal);

        if (flat.Length != KeyLength)
            return new ProductKeyValidation(false, ProductKeyError.WrongLength, normalized, WindowsEdition.Pro);

        foreach (char c in flat)
        {
            if (!AllowedAlphabet.Contains(c, StringComparison.Ordinal))
                return new ProductKeyValidation(false, ProductKeyError.InvalidCharacters, normalized, WindowsEdition.Pro);
        }

        // «AAAAAAAAA...» формально проходит алфавит, но настоящим ключом быть не может.
        if (flat.Distinct().Count() < 6)
            return new ProductKeyValidation(false, ProductKeyError.InvalidKey, normalized, WindowsEdition.Pro);

        // Последовательности вида 23456-23456-... — тоже отбрасываем.
        for (int offset = 0; offset + GroupSize <= KeyLength; offset += GroupSize)
        {
            if (flat.AsSpan(offset, GroupSize).SequenceEqual(flat.AsSpan(0, GroupSize)) && offset > 0)
                return new ProductKeyValidation(false, ProductKeyError.InvalidKey, normalized, WindowsEdition.Pro);
        }

        WindowsEdition edition = GenericKeys.TryGetValue(normalized, out WindowsEdition generic)
            ? generic
            : EditionFromHash(flat);

        return new ProductKeyValidation(true, ProductKeyError.None, normalized, edition);
    }

    /// <summary>Детерминированный выбор редакции: одинаковый ключ всегда даёт одну и ту же редакцию.</summary>
    private static WindowsEdition EditionFromHash(string flatKey)
    {
        uint hash = 2166136261;                      // FNV-1a
        foreach (char c in flatKey)
        {
            hash ^= (uint)c;                         // явное приведение: char -> uint неявно не приводится
            hash *= 16777619;
        }

        return (hash % 10) switch
        {
            <= 5 => WindowsEdition.Pro,             // Pro — самая частая в retail
            <= 8 => WindowsEdition.Home,
            _ => WindowsEdition.Enterprise
        };
    }

    /// <summary>Ключ, который предлагается кнопкой «У меня нет ключа продукта».</summary>
    public static string SampleGenericKey(WindowsEdition edition) => edition switch
    {
        WindowsEdition.Home => "YTMG3-N6DKC-DKB77-7M9GH-8HVX7",
        WindowsEdition.Enterprise => "NPPR9-FWDCX-D2C8J-H872K-2YT43",
        _ => "VK7JG-NPHTM-C97JM-9MPGT-3V66T"
    };
}

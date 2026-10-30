using System.Buffers;

namespace Win10Sim.Core.FileSystem;

/// <summary>
/// Работа с путями виртуального диска. Отдельный класс, потому что
/// <see cref="System.IO.Path"/> на Linux ведёт себя иначе (разделители,
/// буква диска), а «диск C:» должен работать одинаково везде, где запущен
/// симулятор, и в тестах на CI-раннере.
/// </summary>
public static class VfsPath
{
    /// <summary>
    /// Символы, запрещённые в именах Windows. Кэшированная таблица поиска:
    /// проверка выполняется на каждое создание/переименование файла.
    /// </summary>
    private static readonly SearchValues<char> InvalidNameChars =
        SearchValues.Create("\"/\\[]:;|=,+*?<>");

    public const string Root = @"C:\";
    public const string Drive = "C:";

    /// <summary>Приводит любой ввод к каноническому виду: "C:\Users\ivan\Documents".</summary>
    public static string Normalize(string? path, string? currentDirectory = null)
    {
        string value = (path ?? string.Empty).Trim().Replace('/', '\\');

        // Пустая строка и "." — текущий каталог.
        if (value.Length == 0 || value == ".")
            return Normalize(currentDirectory ?? Root);

        // "C:" — текущий каталог на диске C: (в cmd это именно так).
        // Диск в симуляторе один, поэтому «текущий каталог диска» — корень.
        if (value.Length == 2 && value[1] == ':' && char.IsLetter(value[0]))
            return Root;

        // "C:Users\ivan" — путь относительно текущего каталога диска C:.
        if (value.Length > 2 && value[1] == ':' && char.IsLetter(value[0]) && value[2] != '\\')
            value = value[..2] + "\\" + value[2..];

        // ".." и относительные пути раскрываются от текущего каталога.
        if (!IsAbsolute(value))
        {
            string basePath = currentDirectory ?? Root;
            value = Combine(basePath, value);
        }

        // Убираем "C:" и ведущие слэши, дальше работаем с сегментами.
        if (value.StartsWith(Drive, StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        string[] segments = value
            .Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s != ".")
            .ToArray();

        var stack = new List<string>();
        foreach (string segment in segments)
        {
            if (segment == "..")
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);

                continue;
            }

            stack.Add(segment);
        }

        return stack.Count == 0 ? Root : Drive + "\\" + string.Join('\\', stack);
    }

    public static bool IsAbsolute(string path)
    {
        string value = path.Replace('/', '\\');
        return value.StartsWith(Drive + "\\", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("\\\\", StringComparison.Ordinal);
    }

    /// <summary>Склеивает два пути с нормализацией.</summary>
    public static string Combine(string directory, string name)
    {
        string left = (directory ?? Root).TrimEnd('\\');
        string right = (name ?? string.Empty).TrimStart('\\');

        if (IsAbsolute(right))
            return Normalize(right);

        return Normalize(left + "\\" + right);
    }

    /// <summary>Родительский каталог; у корня родителя нет — возвращается корень.</summary>
    public static string GetParent(string path)
    {
        string normalized = Normalize(path);
        if (normalized == Root)
            return Root;

        int index = normalized.LastIndexOf('\\');
        return index <= 2 ? Root : normalized[..index];
    }

    /// <summary>Имя последнего сегмента; для корня — "C:\".</summary>
    public static string GetName(string path)
    {
        string normalized = Normalize(path);
        if (normalized == Root)
            return Root;

        int index = normalized.LastIndexOf('\\');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    public static string GetExtension(string path)
    {
        string name = GetName(path);
        int index = name.LastIndexOf('.');
        return index <= 0 ? string.Empty : name[index..].ToLowerInvariant();
    }

    /// <summary>Проверка имени файла/каталога по правилам Windows.</summary>
    public static bool IsValidName(string? name, out string error)
    {
        error = string.Empty;
        string value = name ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Имя не может быть пустым.";
            return false;
        }

        if (value.AsSpan().IndexOfAny(InvalidNameChars) >= 0)
        {
            error = "Имя содержит недопустимые символы.";
            return false;
        }

        if (value.EndsWith('.') || value.EndsWith(' '))
        {
            error = "Имя не может заканчиваться точкой или пробелом.";
            return false;
        }

        string stem = value.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && char.IsDigit(stem[3])))
        {
            error = "Это имя зарезервировано системой.";
            return false;
        }

        return true;
    }
}

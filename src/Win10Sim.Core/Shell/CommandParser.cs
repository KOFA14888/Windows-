using System.Text;

namespace Win10Sim.Core.Shell;

/// <summary>
/// Разбор командной строки: кавычки, экранирование, редирект.
/// Редирект поддерживается только для echo ("> file.txt" и ">> file.txt"),
/// как самое частое, что пробуют в симуляторе.
/// </summary>
public static class CommandParser
{
    public readonly record struct ParsedCommand(
        string Name,
        IReadOnlyList<string> Arguments,
        string? RedirectPath,
        bool Append);

    public static ParsedCommand Parse(string line)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        string? redirectPath = null;
        bool append = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && (c == '>' || c == '<'))
            {
                Flush(arguments, current);

                if (c == '<')
                {
                    // Ввод из файла в симуляторе не поддерживается: возвращаем как есть.
                    current.Append(c);
                    continue;
                }

                append = i + 1 < line.Length && line[i + 1] == '>';
                if (append)
                    i++;

                redirectPath = line[(i + 1)..].Trim().Trim('"');
                break;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                Flush(arguments, current);
                continue;
            }

            current.Append(c);
        }

        Flush(arguments, current);

        string name = arguments.Count > 0 ? arguments[0].ToLowerInvariant() : string.Empty;
        if (arguments.Count > 0)
            arguments.RemoveAt(0);

        return new ParsedCommand(name, arguments, string.IsNullOrWhiteSpace(redirectPath) ? null : redirectPath, append);
    }

    private static void Flush(List<string> arguments, StringBuilder current)
    {
        if (current.Length == 0)
            return;

        arguments.Add(current.ToString());
        current.Clear();
    }
}

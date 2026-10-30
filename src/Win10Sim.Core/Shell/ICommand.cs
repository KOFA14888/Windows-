namespace Win10Sim.Core.Shell;

/// <summary>
/// Команда cmd. Отдельный класс на команду — чтобы команды можно было
/// тестировать по одной и легко добавлять новые.
/// </summary>
public interface ICommand
{
    /// <summary>Имя команды строчными буквами ("dir").</summary>
    string Name { get; }

    /// <summary>Альтернативные имена ("chdir" для "cd").</summary>
    IEnumerable<string> Aliases => Array.Empty<string>();

    /// <summary>Строка справки для `help`.</summary>
    string Help { get; }

    /// <summary>Развёрнутая справка для `help <команда>`.</summary>
    string Usage => Help;

    Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

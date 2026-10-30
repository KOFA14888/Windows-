using System.Globalization;

namespace Win10Sim.Core.Shell.Commands;

/// <summary>help — список команд симулятора.</summary>
public sealed class HelpCommand : ICommand
{
    private readonly CommandProcessor _processor;

    public HelpCommand(CommandProcessor processor) => _processor = processor;

    public string Name => "help";
    public string Help => "HELP         Вывод списка команд симулятора.";
    public string Usage => "HELP [команда]";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count > 0)
        {
            ICommand? command = _processor.Find(arguments[0].TrimStart('/').ToLowerInvariant());
            return Task.FromResult(command is null
                ? CommandResult.FromLine($"Команда не найдена: {arguments[0]}")
                : new CommandResult(command.Usage.Replace("\r\n", "\n").Split('\n')));
        }

        var lines = new List<string>
        {
            string.Empty,
            "Доступные команды симулятора:",
            string.Empty
        };

        foreach (ICommand command in _processor.Commands)
            lines.Add("  " + command.Help);

        lines.Add(string.Empty);
        lines.Add("Поддерживается редирект вывода: ECHO текст > файл.txt");
        lines.Add("Виртуальный диск: C:\\ (данные хранятся в state-каталоге симулятора).");
        lines.Add(string.Empty);

        return Task.FromResult(new CommandResult(lines));
    }
}

/// <summary>cls — очистка экрана.</summary>
public sealed class ClsCommand : ICommand
{
    public string Name => "cls";
    public string Help => "CLS          Очистка экрана.";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => Task.FromResult(new CommandResult(Array.Empty<string>(), clearScreen: true));
}

/// <summary>ver — версия Windows.</summary>
public sealed class VerCommand : ICommand
{
    public string Name => "ver";
    public string Help => "VER          Отображение версии Windows.";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CommandResult(new[]
        {
            string.Empty,
            $"Microsoft Windows [Version {CommandProcessor.WindowsVersion}]"
        }));
    }
}

/// <summary>whoami.</summary>
public sealed class WhoAmICommand : ICommand
{
    public string Name => "whoami";
    public string Help => "WHOAMI       Вывод имени текущего пользователя.";
    public string Usage => "WHOAMI [/UPN] [/FQDN]";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string format = arguments.Count > 0 ? arguments[0].ToUpperInvariant() : string.Empty;

        return Task.FromResult(CommandResult.FromLine(format switch
        {
            "/UPN" => $"{context.UserName}@desktop-sim",
            "/FQDN" => $"desktop-sim\\{context.UserName}",
            _ => $"desktop-sim\\{context.UserName}"
        }));
    }
}

/// <summary>hostname.</summary>
public sealed class HostnameCommand : ICommand
{
    public string Name => "hostname";
    public string Help => "HOSTNAME     Вывод имени компьютера.";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => Task.FromResult(CommandResult.FromLine("DESKTOP-SIM"));
}

/// <summary>date.</summary>
public sealed class DateCommand : ICommand
{
    public string Name => "date";
    public string Help => "DATE         Отображение текущей даты.";
    public string Usage => "DATE [/T]";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string text = DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
        return Task.FromResult(CommandResult.FromLine(
            arguments.Any(a => a.Equals("/t", StringComparison.OrdinalIgnoreCase))
                ? text
                : "Текущая дата: " + text + " (изменение в симуляторе недоступно)"));
    }
}

/// <summary>time.</summary>
public sealed class TimeCommand : ICommand
{
    public string Name => "time";
    public string Help => "TIME         Отображение текущего времени.";
    public string Usage => "TIME [/T]";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        return Task.FromResult(CommandResult.FromLine(
            arguments.Any(a => a.Equals("/t", StringComparison.OrdinalIgnoreCase))
                ? text
                : "Текущее время: " + text + " (изменение в симуляторе недоступно)"));
    }
}

/// <summary>
/// exit — закрыть окно командной строки. Одна из немногих команд, у которой
/// есть смысл в GUI-симуляторе: окно действительно закрывается.
/// </summary>
public sealed class ExitCommand : ICommand
{
    public string Name => "exit";
    public string Help => "EXIT         Завершение работы командной строки (закрытие окна).";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => Task.FromResult(new CommandResult(Array.Empty<string>(), exitRequested: true));
}

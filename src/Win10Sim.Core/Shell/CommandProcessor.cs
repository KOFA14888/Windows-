using Win10Sim.Core.FileSystem;
using Win10Sim.Core.Shell.Commands;

namespace Win10Sim.Core.Shell;

/// <summary>
/// Разбор строки и вызов нужной команды. Знает про рабочую папку и историю,
/// но не знает ничего про UI: окно cmd просто печатает <see cref="CommandResult"/>.
/// </summary>
public sealed class CommandProcessor
{
    public const string WindowsVersion = "10.0.19045.5011";

    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _history = new();

    public CommandProcessor(IVirtualFileSystem fileSystem, string userName, string? currentDirectory = null)
    {
        UserName = string.IsNullOrWhiteSpace(userName) ? "user" : userName;
        Context = new CommandContext(
            fileSystem,
            VfsPath.Normalize(currentDirectory ?? $@"C:\Users\{UserName}"),
            UserName);

        Register(new HelpCommand(this));
        Register(new ClsCommand());
        Register(new DirCommand());
        Register(new CdCommand());
        Register(new MdCommand());
        Register(new RdCommand());
        Register(new DelCommand());
        Register(new CopyCommand());
        Register(new MoveCommand());
        Register(new RenCommand());
        Register(new TypeCommand());
        Register(new EchoCommand());
        Register(new VerCommand());
        Register(new WhoAmICommand());
        Register(new HostnameCommand());
        Register(new DateCommand());
        Register(new TimeCommand());
        Register(new PingCommand());
        Register(new ExitCommand());
    }

    public string UserName { get; }

    public CommandContext Context { get; }

    public IReadOnlyList<string> History => _history;

    public IEnumerable<ICommand> Commands => _commands.Values.Distinct().OrderBy(c => c.Name, StringComparer.Ordinal);

    public string Prompt => Context.Prompt;

    public static IReadOnlyList<string> Banner => CommandContext.Banner(WindowsVersion);

    public void Register(ICommand command)
    {
        _commands[command.Name] = command;
        foreach (string alias in command.Aliases)
            _commands[alias] = command;
    }

    public ICommand? Find(string name) => _commands.TryGetValue(name, out ICommand? command) ? command : null;

    public async Task<CommandResult> ExecuteAsync(string line, CancellationToken cancellationToken = default)
    {
        string trimmed = (line ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return CommandResult.Empty;

        _history.Add(trimmed);

        CommandParser.ParsedCommand parsed = CommandParser.Parse(trimmed);

        if (parsed.Name.Length == 0)
            return CommandResult.Empty;

        ICommand? command = Find(parsed.Name);
        if (command is null)
        {
            return new CommandResult(new[]
            {
                $"\"{parsed.Name}\" не является внутренней или внешней",
                "командой, исполняемой программой или пакетным файлом.",
                "Введите help, чтобы увидеть список команд симулятора."
            });
        }

        try
        {
            CommandResult result = await command
                .ExecuteAsync(Context, parsed.Arguments, cancellationToken)
                .ConfigureAwait(false);

            // Редирект: результат команды уходит в файл, на экран — ничего.
            if (parsed.RedirectPath is { Length: > 0 } target && !result.ClearScreen)
            {
                string path = VfsPath.Normalize(target, Context.CurrentDirectory);
                string text = string.Join(Environment.NewLine, result.Lines) + Environment.NewLine;

                if (parsed.Append && Context.FileSystem.Exists(path))
                    text = Context.FileSystem.ReadAllText(path) + text;

                Context.FileSystem.WriteAllText(path, text);
                return CommandResult.Empty;
            }

            return result;
        }
        catch (VfsException ex)
        {
            return CommandResult.FromLine(ex.Message);
        }
        catch (OperationCanceledException)
        {
            return CommandResult.FromLine("^C");
        }
    }

    /// <summary>Автодополнение по Tab: имена команд или файлов текущего каталога.</summary>
    public string? Complete(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string[] parts = input.Split(' ');
        if (parts.Length == 1)
        {
            string[] matches = _commands.Keys
                .Where(k => k.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            return matches.Length == 1 ? matches[0] + " " : null;
        }

        string prefix = parts[^1];
        string directory = Context.CurrentDirectory;

        try
        {
            IReadOnlyList<VfsNode> nodes = Context.FileSystem.Enumerate(directory);
            string[] matches = nodes
                .Where(n => n.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(n => n.Name + (n.IsDirectory ? "\\" : string.Empty))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (matches.Length == 1)
            {
                parts[^1] = matches[0];
                return string.Join(' ', parts);
            }
        }
        catch (VfsException)
        {
        }

        return null;
    }
}

namespace Win10Sim.Core.Shell;

/// <summary>Результат выполнения одной строки в cmd.</summary>
public sealed class CommandResult
{
    public CommandResult(IReadOnlyList<string> lines, bool clearScreen = false, bool exitRequested = false, int exitCode = 0)
    {
        Lines = lines;
        ClearScreen = clearScreen;
        ExitRequested = exitRequested;
        ExitCode = exitCode;
    }

    public IReadOnlyList<string> Lines { get; }

    /// <summary>Команда просит очистить экран (cls).</summary>
    public bool ClearScreen { get; }

    /// <summary>Команда exit — закрыть окно cmd.</summary>
    public bool ExitRequested { get; }

    public int ExitCode { get; }

    public static CommandResult Empty { get; } = new(Array.Empty<string>());

    public static CommandResult Single(string line, int exitCode = 0) => new(new[] { line }, exitCode: exitCode);
}

/// <summary>Контекст выполнения: рабочая папка и доступ к «железу» симулятора.</summary>
public sealed class CommandContext
{
    public CommandContext(FileSystem.IVirtualFileSystem fileSystem, string currentDirectory, string userName)
    {
        FileSystem = fileSystem;
        CurrentDirectory = currentDirectory;
        UserName = userName;
    }

    public FileSystem.IVirtualFileSystem FileSystem { get; }

    /// <summary>Текущий каталог. Команда cd меняет его — менять может только процессор команд.</summary>
    public string CurrentDirectory { get; set; }

    public string UserName { get; }

    public string Prompt => CurrentDirectory + ">";

    /// <summary>Баннер при запуске cmd — как у настоящей командной строки.</summary>
    public static IReadOnlyList<string> Banner(string version) => new[]
    {
        "Microsoft Windows [Version " + version + "]",
        "(c) Корпорация Майкрософт. Все права защищены.",
        string.Empty,
        "Симулятор: команды работают с виртуальным диском C:. Список команд — help."
    };
}

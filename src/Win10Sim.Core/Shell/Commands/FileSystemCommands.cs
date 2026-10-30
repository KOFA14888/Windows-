using System.Globalization;
using System.Text;
using Win10Sim.Core.FileSystem;

namespace Win10Sim.Core.Shell.Commands;

/// <summary>dir — список файлов и папок.</summary>
public sealed class DirCommand : ICommand
{
    public string Name => "dir";
    public string Help => "DIR          Вывод списка файлов и подкаталогов в указанном каталоге.";
    public string Usage =>
        "DIR [диск:][путь][имя_файла] [/A] [/S]\r\n" +
        "  /A  — показать скрытые и системные файлы (в симуляторе влияет только на размер списка)\r\n" +
        "  /S  — рекурсивный обход подкаталогов";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string? path = arguments.FirstOrDefault(a => !a.StartsWith('/'));
        bool recurse = arguments.Any(a => a.Equals("/s", StringComparison.OrdinalIgnoreCase));

        string directory = path is null
            ? context.CurrentDirectory
            : VfsPath.Normalize(path, context.CurrentDirectory);

        VfsNode node = context.FileSystem.Find(directory)
                       ?? throw new VfsException($"Не удаётся найти путь: {directory}");

        if (!node.IsDirectory)
        {
            return Task.FromResult(new CommandResult(new[]
            {
                string.Empty,
                $" Каталог: {VfsPath.Drive}\\{VfsPath.GetParent(directory)}",
                string.Empty,
                FormatFileLine(node),
                $"              1 файлов, {(node.SizeBytes / 1024.0):N0} КБ"
            }));
        }

        var lines = new List<string>
        {
            string.Empty,
            $" Каталог {directory}",
            string.Empty
        };

        List<VfsNode> children = context.FileSystem.Enumerate(directory).ToList();
        long totalFiles = 0;
        long totalSize = 0;

        foreach (VfsNode child in children)
        {
            lines.Add(child.IsDirectory ? FormatDirectoryLine(child) : FormatFileLine(child));

            if (!child.IsDirectory)
            {
                totalFiles++;
                totalSize += child.SizeBytes;
            }
        }

        if (children.Count == 0)
            lines.Add(" (пусто)");

        // Для /s дополнительно показываем содержимое подкаталогов.
        if (recurse)
        {
            foreach (VfsNode child in children.Where(c => c.IsDirectory).ToList())
                AppendRecursive(context, child, directory, lines);
        }

        lines.Add($"              {totalFiles} файлов, {(totalSize / 1024.0):N0} КБ");
        lines.Add($"              {children.Count(c => c.IsDirectory)} папок");
        lines.Add(string.Empty);

        return Task.FromResult(new CommandResult(lines));
    }

    private static void AppendRecursive(CommandContext context, VfsNode directory, string parentPath, List<string> lines)
    {
        string path = VfsPath.Combine(parentPath, directory.Name);
        lines.Add(string.Empty);
        lines.Add($" Каталог {path}");
        lines.Add(string.Empty);

        foreach (VfsNode child in context.FileSystem.Enumerate(path))
        {
            lines.Add(child.IsDirectory ? FormatDirectoryLine(child) : FormatFileLine(child));
            if (child.IsDirectory)
                AppendRecursive(context, child, path, lines);
        }
    }

    private static string FormatFileLine(VfsNode node)
    {
        string date = node.ModifiedUtc.ToString("dd.MM.yyyy  HH:mm", CultureInfo.CurrentCulture);
        string size = node.SizeBytes.ToString("N0", CultureInfo.CurrentCulture).PadLeft(14);
        return $"{date}   {size} {node.Name}";
    }

    private static string FormatDirectoryLine(VfsNode node)
    {
        string date = node.ModifiedUtc.ToString("dd.MM.yyyy  HH:mm", CultureInfo.CurrentCulture);
        return $"{date}   {"<ПАПКА>",14} {node.Name}";
    }
}

/// <summary>cd / chdir — смена текущего каталога.</summary>
public sealed class CdCommand : ICommand
{
    public string Name => "cd";
    public IEnumerable<string> Aliases => new[] { "chdir" };
    public string Help => "CD           Вывод имени либо смена текущей папки.";
    public string Usage => "CD [/D] [диск:][путь]\r\n  CD ..  — перейти в родительский каталог";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string? target = arguments.Count > 0 ? arguments[0] : null;

        if (string.IsNullOrWhiteSpace(target))
            return Task.FromResult(CommandResult.FromLine(context.CurrentDirectory));

        string path = VfsPath.Normalize(target, context.CurrentDirectory);
        VfsNode? node = context.FileSystem.Find(path);

        if (node is null)
            return Task.FromResult(CommandResult.FromLine("Системе не удается найти указанный путь."));

        if (!node.IsDirectory)
            return Task.FromResult(CommandResult.FromLine("Не удается выполнить команду: указанный путь не является папкой."));

        context.CurrentDirectory = path;
        return Task.FromResult(CommandResult.Empty);
    }
}

/// <summary>md / mkdir.</summary>
public sealed class MdCommand : ICommand
{
    public string Name => "md";
    public IEnumerable<string> Aliases => new[] { "mkdir" };
    public string Help => "MD           Создание каталога.";
    public string Usage => "MD [диск:]путь";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
            return Task.FromResult(CommandResult.FromLine("Синтаксис команды: MD [диск:]путь"));

        foreach (string argument in arguments)
            context.FileSystem.CreateDirectory(VfsPath.Normalize(argument, context.CurrentDirectory));

        return Task.FromResult(CommandResult.Empty);
    }
}

/// <summary>rd / rmdir.</summary>
public sealed class RdCommand : ICommand
{
    public string Name => "rd";
    public IEnumerable<string> Aliases => new[] { "rmdir" };
    public string Help => "RD           Удаление каталога.";
    public string Usage => "RD [/S] [/Q] [диск:]путь\r\n  /S — удалить каталог со всем содержимым";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        bool recursive = arguments.Any(a => a.Equals("/s", StringComparison.OrdinalIgnoreCase));
        string? path = arguments.FirstOrDefault(a => !a.StartsWith('/'));

        if (path is null)
            return Task.FromResult(CommandResult.FromLine("Синтаксис команды: RD [/S] [диск:]путь"));

        string normalized = VfsPath.Normalize(path, context.CurrentDirectory);
        VfsNode? node = context.FileSystem.Find(normalized) ?? throw new VfsException("Системе не удается найти указанный путь.");

        if (!node.IsDirectory)
            return Task.FromResult(CommandResult.FromLine("Указанный путь не является каталогом."));

        if (normalized.Equals(VfsPath.Normalize(context.CurrentDirectory), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(CommandResult.FromLine("Нельзя удалить текущий каталог."));

        context.FileSystem.Delete(normalized, recursive);
        return Task.FromResult(CommandResult.Empty);
    }
}

/// <summary>del / erase.</summary>
public sealed class DelCommand : ICommand
{
    public string Name => "del";
    public IEnumerable<string> Aliases => new[] { "erase" };
    public string Help => "DEL          Удаление одного или нескольких файлов.";
    public string Usage => "DEL [/F] [/Q] имя_файла [...]";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string[] targets = arguments.Where(a => !a.StartsWith('/')).ToArray();
        if (targets.Length == 0)
            return Task.FromResult(CommandResult.FromLine("Синтаксис команды: DEL имя_файла"));

        foreach (string target in targets)
        {
            string path = VfsPath.Normalize(target, context.CurrentDirectory);
            VfsNode? node = context.FileSystem.Find(path) ?? throw new VfsException($"Не удается найти {path}");

            if (node.IsDirectory)
                return Task.FromResult(CommandResult.FromLine($"{path} — это папка, используйте RD."));

            context.FileSystem.Delete(path);
        }

        return Task.FromResult(CommandResult.Empty);
    }
}

/// <summary>copy.</summary>
public sealed class CopyCommand : ICommand
{
    public string Name => "copy";
    public string Help => "COPY         Копирование одного или нескольких файлов.";
    public string Usage => "COPY источник назначение";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count < 2)
            return Task.FromResult(CommandResult.FromLine("Синтаксис команды: COPY источник назначение"));

        context.FileSystem.Copy(
            VfsPath.Normalize(arguments[0], context.CurrentDirectory),
            VfsPath.Normalize(arguments[1], context.CurrentDirectory));

        return Task.FromResult(CommandResult.FromLine("        1 файлов скопировано."));
    }
}

/// <summary>move.</summary>
public sealed class MoveCommand : ICommand
{
    public string Name => "move";
    public string Help => "MOVE         Перемещение файлов и переименование каталогов.";
    public string Usage => "MOVE источник назначение";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count < 2)
            return Task.FromResult(CommandResult.FromLine("Синтаксис команды: MOVE источник назначение"));

        context.FileSystem.Move(
            VfsPath.Normalize(arguments[0], context.CurrentDirectory),
            VfsPath.Normalize(arguments[1], context.CurrentDirectory));

        return Task.FromResult(CommandResult.FromLine("        1 файлов перемещено."));
    }
}

/// <summary>ren / rename.</summary>
public sealed class RenCommand : ICommand
{
    public string Name => "ren";
    public IEnumerable<string> Aliases => new[] { "rename" };
    public string Help => "REN          Переименование файла или каталога.";
    public string Usage => "REN старое_имя новое_имя";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count < 2)
            return Task.FromResult(CommandResult.FromLine("Синтаксис команды: REN старое_имя новое_имя"));

        string source = VfsPath.Normalize(arguments[0], context.CurrentDirectory);
        if (!VfsPath.IsValidName(arguments[1], out string error))
            return Task.FromResult(CommandResult.FromLine(error));

        if (arguments[1].Contains('\\') || arguments[1].Contains('/'))
            return Task.FromResult(CommandResult.FromLine("Новое имя не должно содержать путь."));

        string target = VfsPath.Combine(VfsPath.GetParent(source), arguments[1]);
        context.FileSystem.Move(source, target);
        return Task.FromResult(CommandResult.Empty);
    }
}

/// <summary>type — вывод содержимого текстового файла.</summary>
public sealed class TypeCommand : ICommand
{
    public string Name => "type";
    public string Help => "TYPE         Вывод содержимого текстового файла.";
    public string Usage => "TYPE [диск:][путь]имя_файла";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
            return Task.FromResult(CommandResult.FromLine("Синтаксис команды: TYPE имя_файла"));

        string path = VfsPath.Normalize(arguments[0], context.CurrentDirectory);
        string text = context.FileSystem.ReadAllText(path);

        return Task.FromResult(new CommandResult(text.Replace("\r\n", "\n").Split('\n')));
    }
}

/// <summary>echo.</summary>
public sealed class EchoCommand : ICommand
{
    public string Name => "echo";
    public string Help => "ECHO         Вывод сообщений и переключение отображения команд на экране.";
    public string Usage => "ECHO [сообщение]\r\n  ECHO текст > файл.txt   — записать текст в файл\r\n  ECHO текст >> файл.txt  — дописать в файл";

    public Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string text = string.Join(' ', arguments);

        if (text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CommandResult.Empty);
        }

        return Task.FromResult(CommandResult.FromLine(text));
    }
}

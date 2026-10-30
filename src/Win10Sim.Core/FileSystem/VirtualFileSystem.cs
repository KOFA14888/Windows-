using System.Text.Json;
using System.Text.Json.Serialization;

namespace Win10Sim.Core.FileSystem;

/// <summary>
/// Виртуальный диск C: в памяти + сохранение в JSON.
///
/// Правила:
///   * дерево одно на процесс, доступ синхронизирован (Проводник и cmd могут
///     обращаться одновременно);
///   * изменения сразу пишутся на диск (файл маленький, а потеря данных хуже
///     лишней записи);
///   * «форматирование» на ЭТАПЕ 1 = пересоздание дерева, а не удаление файлов
///     на реальном диске.
/// </summary>
public sealed class VirtualFileSystem : IVirtualFileSystem
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly string? _storagePath;

    public VirtualFileSystem(string? storagePath = null, string userName = "user")
    {
        _storagePath = storagePath;
        VfsNode? loaded = TryLoad(storagePath);
        Root = loaded ?? VfsSeeder.CreateDefault(userName);
    }

    public VfsNode Root { get; }

    /// <summary>Свободное место на «диске C:» — константа, как у настоящего раздела.</summary>
    public long FreeSpaceBytes => 402L * 1000 * 1000 * 1000;

    public event EventHandler? Changed;

    public VfsNode? Get(string path)
    {
        lock (_gate)
        {
            return Resolve(path);
        }
    }

    public bool Exists(string path) => Get(path) is not null;

    public VfsNode CreateDirectory(string path)
    {
        lock (_gate)
        {
            string normalized = VfsPath.Normalize(path);
            VfsNode? existing = Resolve(normalized);
            if (existing is not null)
                throw new VfsException($"Папка уже существует: {normalized}");

            string parentPath = VfsPath.GetParent(normalized);
            VfsNode parent = Resolve(parentPath) ?? throw new VfsException($"Путь не найден: {parentPath}");
            if (!parent.IsDirectory)
                throw new VfsException($"Не папка: {parentPath}");

            string name = VfsPath.GetName(normalized);
            if (!VfsPath.IsValidName(name, out string error))
                throw new VfsException(error);

            if (parent.Children.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new VfsException($"Папка уже существует: {normalized}");

            var node = new VfsNode(VfsKind.Directory, name);
            parent.Children.Add(node);
            parent.ModifiedUtc = DateTime.UtcNow;
            NotifyChanged();
            return node;
        }
    }

    public VfsNode CreateFile(string path, string content = "")
    {
        lock (_gate)
        {
            string normalized = VfsPath.Normalize(path);
            if (Resolve(normalized) is not null)
                throw new VfsException($"Файл уже существует: {normalized}");

            string parentPath = VfsPath.GetParent(normalized);
            VfsNode parent = Resolve(parentPath) ?? throw new VfsException($"Путь не найден: {parentPath}");
            if (!parent.IsDirectory)
                throw new VfsException($"Не папка: {parentPath}");

            string name = VfsPath.GetName(normalized);
            if (!VfsPath.IsValidName(name, out string error))
                throw new VfsException(error);

            var node = new VfsNode(VfsKind.File, name) { Content = content };
            parent.Children.Add(node);
            parent.ModifiedUtc = DateTime.UtcNow;
            NotifyChanged();
            return node;
        }
    }

    public string ReadAllText(string path)
    {
        lock (_gate)
        {
            VfsNode node = Resolve(path) ?? throw new VfsException($"Не удаётся найти путь: {VfsPath.Normalize(path)}");
            if (node.IsDirectory)
                throw new VfsException($"Это папка: {VfsPath.Normalize(path)}");

            return node.Content ?? string.Empty;
        }
    }

    public void WriteAllText(string path, string content)
    {
        lock (_gate)
        {
            string normalized = VfsPath.Normalize(path);
            VfsNode? node = Resolve(normalized);

            if (node is null)
            {
                CreateFile(normalized, content);
                return;
            }

            if (node.IsDirectory)
                throw new VfsException($"Это папка: {normalized}");

            node.Content = content;
            node.ModifiedUtc = DateTime.UtcNow;
            NotifyChanged();
        }
    }

    public void Delete(string path, bool recursive = false)
    {
        lock (_gate)
        {
            string normalized = VfsPath.Normalize(path);
            if (normalized == VfsPath.Root)
                throw new VfsException("Нельзя удалить корневой каталог.");

            VfsNode? node = Resolve(normalized) ?? throw new VfsException($"Не удаётся найти путь: {normalized}");

            if (node.IsDirectory && node.Children.Count > 0 && !recursive)
                throw new VfsException("Папка не пуста. Используйте /s для удаления с содержимым.");

            string parentPath = VfsPath.GetParent(normalized);
            VfsNode parent = Resolve(parentPath)!;
            parent.Children.Remove(node);
            parent.ModifiedUtc = DateTime.UtcNow;
            NotifyChanged();
        }
    }

    public void Move(string from, string to)
    {
        lock (_gate)
        {
            string sourcePath = VfsPath.Normalize(from);
            VfsNode node = Resolve(sourcePath) ?? throw new VfsException($"Не удаётся найти путь: {sourcePath}");

            string targetPath = VfsPath.Normalize(to);

            // Если цель — существующий каталог, переносим внутрь него с тем же именем.
            VfsNode? target = Resolve(targetPath);
            if (target is { IsDirectory: true })
                targetPath = VfsPath.Combine(targetPath, node.Name);

            if (Resolve(targetPath) is not null)
                throw new VfsException($"Файл уже существует: {targetPath}");

            string targetParentPath = VfsPath.GetParent(targetPath);
            VfsNode targetParent = Resolve(targetParentPath) ?? throw new VfsException($"Путь не найден: {targetParentPath}");
            if (!targetParent.IsDirectory)
                throw new VfsException($"Не папка: {targetParentPath}");

            if (node.IsDirectory && targetPath.StartsWith(sourcePath + "\\", StringComparison.OrdinalIgnoreCase))
                throw new VfsException("Нельзя переместить папку внутрь себя.");

            VfsNode sourceParent = Resolve(VfsPath.GetParent(sourcePath))!;
            sourceParent.Children.Remove(node);
            node.Name = VfsPath.GetName(targetPath);
            node.ModifiedUtc = DateTime.UtcNow;
            targetParent.Children.Add(node);
            targetParent.ModifiedUtc = DateTime.UtcNow;
            NotifyChanged();
        }
    }

    public void Copy(string from, string to)
    {
        lock (_gate)
        {
            string sourcePath = VfsPath.Normalize(from);
            VfsNode node = Resolve(sourcePath) ?? throw new VfsException($"Не удаётся найти путь: {sourcePath}");

            string targetPath = VfsPath.Normalize(to);
            VfsNode? target = Resolve(targetPath);
            if (target is { IsDirectory: true })
                targetPath = VfsPath.Combine(targetPath, node.Name);

            if (Resolve(targetPath) is not null)
                throw new VfsException($"Файл уже существует: {targetPath}");

            string targetParentPath = VfsPath.GetParent(targetPath);
            VfsNode targetParent = Resolve(targetParentPath) ?? throw new VfsException($"Путь не найден: {targetParentPath}");

            VfsNode copy = Clone(node, VfsPath.GetName(targetPath));
            targetParent.Children.Add(copy);
            targetParent.ModifiedUtc = DateTime.UtcNow;
            NotifyChanged();
        }
    }

    public IReadOnlyList<VfsNode> Enumerate(string path)
    {
        lock (_gate)
        {
            VfsNode node = Resolve(path) ?? throw new VfsException($"Не удаётся найти путь: {VfsPath.Normalize(path)}");
            return node.IsDirectory
                ? node.Children.OrderBy(c => c.IsDirectory ? 0 : 1).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
                : new[] { node };
        }
    }

    public string GetPath(VfsNode node)
    {
        lock (_gate)
        {
            var segments = new List<string>();
            if (FindPath(Root, node, segments))
            {
                segments.Reverse();
                return segments.Count == 0 ? VfsPath.Root : VfsPath.Drive + "\\" + string.Join('\\', segments);
            }

            return VfsPath.Root + node.Name;
        }
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_storagePath))
            return;

        try
        {
            string directory = Path.GetDirectoryName(_storagePath)!;
            Directory.CreateDirectory(directory);

            string temp = _storagePath + ".tmp";
            lock (_gate)
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(Root, SerializerOptions));
            }

            File.Move(temp, _storagePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Диск недоступен — виртуальная ФС продолжает работать в памяти.
        }
    }

    /// <summary>Полная очистка диска (используется кнопкой «Форматировать»/новой установкой).</summary>
    public void Format(string userName)
    {
        lock (_gate)
        {
            VfsNode fresh = VfsSeeder.CreateDefault(userName);
            Root.Children.Clear();
            Root.Children.AddRange(fresh.Children);
            Root.ModifiedUtc = DateTime.UtcNow;
        }

        Save();
        NotifyChanged();
    }

    private static VfsNode Clone(VfsNode node, string name)
    {
        var copy = new VfsNode(node.Kind, name)
        {
            Content = node.Content,
            CreatedUtc = node.CreatedUtc,
            ModifiedUtc = DateTime.UtcNow
        };

        foreach (VfsNode child in node.Children)
            copy.Children.Add(Clone(child, child.Name));

        return copy;
    }

    private static bool FindPath(VfsNode current, VfsNode target, List<string> segments)
    {
        if (ReferenceEquals(current, target))
            return true;

        foreach (VfsNode child in current.Children)
        {
            if (FindPath(child, target, segments))
            {
                segments.Add(child.Name);
                return true;
            }
        }

        return false;
    }

    private VfsNode? Resolve(string path)
    {
        string normalized = VfsPath.Normalize(path);
        if (normalized == VfsPath.Root)
            return Root;

        string[] segments = normalized[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        VfsNode current = Root;

        foreach (string segment in segments)
        {
            VfsNode? next = current.Children.FirstOrDefault(
                c => string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase));

            if (next is null)
                return null;

            current = next;
        }

        return current;
    }

    private void NotifyChanged()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static VfsNode? TryLoad(string? storagePath)
    {
        if (string.IsNullOrEmpty(storagePath) || !File.Exists(storagePath))
            return null;

        try
        {
            return JsonSerializer.Deserialize<VfsNode>(File.ReadAllText(storagePath), SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;    // битый файл — начинаем с чистой системы
        }
    }
}

/// <summary>Ошибка операции с виртуальной файловой системой (сообщение показывается в cmd).</summary>
public sealed class VfsException : Exception
{
    public VfsException(string message)
        : base(message)
    {
    }
}

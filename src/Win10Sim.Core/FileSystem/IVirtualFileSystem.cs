namespace Win10Sim.Core.FileSystem;

/// <summary>
/// Контракт виртуального диска. Реализация одна (<see cref="VirtualFileSystem"/>),
/// но интерфейс позволяет тестировать команды cmd и Проводник без реального диска.
/// </summary>
public interface IVirtualFileSystem
{
    VfsNode Root { get; }

    long FreeSpaceBytes { get; }

    event EventHandler? Changed;

    VfsNode? Get(string path);

    bool Exists(string path);

    VfsNode CreateDirectory(string path);

    VfsNode CreateFile(string path, string content = "");

    string ReadAllText(string path);

    void WriteAllText(string path, string content);

    void Delete(string path, bool recursive = false);

    void Move(string from, string to);

    void Copy(string from, string to);

    IReadOnlyList<VfsNode> Enumerate(string path);

    /// <summary>Полный путь узла (для отображения в Проводнике и cmd).</summary>
    string GetPath(VfsNode node);

    void Save();
}

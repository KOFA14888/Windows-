using System.Text.Json.Serialization;

namespace Win10Sim.Core.FileSystem;

public enum VfsKind
{
    Directory = 0,
    File = 1
}

/// <summary>
/// Узел виртуального диска. Одно дерево на всё приложение: Проводник, Блокнот
/// и cmd работают с одними и теми же объектами, поэтому файл, созданный
/// командой <c>echo > file.txt</c>, сразу виден в Проводнике.
/// </summary>
public sealed class VfsNode
{
    public VfsNode(VfsKind kind, string name)
    {
        Kind = kind;
        Name = name;
        CreatedUtc = DateTime.UtcNow;
        ModifiedUtc = CreatedUtc;
    }

    public VfsKind Kind { get; set; }

    public string Name { get; set; }

    /// <summary>Содержимое файла. Для каталогов всегда null.</summary>
    public string? Content { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    /// <summary>Дети каталога. Для файлов — пустой список, чтобы не проверять null.</summary>
    public List<VfsNode> Children { get; set; } = new();

    [JsonIgnore]
    public bool IsDirectory => Kind == VfsKind.Directory;

    [JsonIgnore]
    public long SizeBytes => IsDirectory ? 0 : (Content?.Length ?? 0) * 2L;   // UTF-16, как в NTFS для текста

    [JsonIgnore]
    public string Glyph => IsDirectory ? "\uE8B7" : FileGlyph(Name);

    /// <summary>Иконка файла по расширению (Segoe MDL2 Assets).</summary>
    private static string FileGlyph(string name)
    {
        string extension = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..].ToLowerInvariant() : string.Empty;

        return extension switch
        {
            "txt" or "log" or "ini" or "cfg" => "\uE8A5",   // Document
            "png" or "jpg" or "jpeg" or "bmp" or "gif" => "\uEB9F",   // Photo
            "exe" or "dll" or "msi" => "\uE7B8",            // Application
            "zip" or "rar" or "7z" => "\uF012",             // Zip folder
            "lnk" => "\uE71B",                              // Link
            _ => "\uE7C3"                                   // Page
        };
    }
}

using System.IO;
using Win10Sim.Core.FileSystem;
using Xunit;

namespace Win10Sim.Core.Tests;

/// <summary>
/// Виртуальный диск C:. Тесты работают без файла на диске (storagePath = null),
/// поэтому прогон ничего не пишет в %LocalAppData%.
/// </summary>
public class VirtualFileSystemTests
{
    private static VirtualFileSystem Create() => new(null, "tester");

    [Fact]
    public void Seeder_CreatesWindowsLikeLayout()
    {
        VirtualFileSystem vfs = Create();

        Assert.True(vfs.Exists(@"C:\Windows\System32"));
        Assert.True(vfs.Exists(@"C:\Program Files"));
        Assert.True(vfs.Exists(@"C:\Users\tester\Documents"));
        Assert.True(vfs.Exists(@"C:\Users\tester\Desktop"));
        Assert.True(vfs.Exists(@"C:\pagefile.sys"));
    }

    [Theory]
    [InlineData("C:\\")]
    [InlineData("c:\\")]
    [InlineData("C:")]
    [InlineData("/")]
    public void Normalize_AcceptsDifferentRoots(string path)
    {
        Assert.Equal(@"C:\", VfsPath.Normalize(path));
    }

    [Theory]
    [InlineData(@"C:\Users")]
    [InlineData(@"c:\users")]
    public void Exists_IsCaseInsensitive(string path)
    {
        VirtualFileSystem vfs = Create();

        Assert.True(vfs.Exists(path));
    }

    [Fact]
    public void CreateDirectory_ThenCreateFile_ThenRead()
    {
        VirtualFileSystem vfs = Create();

        vfs.CreateDirectory(@"C:\Temp");
        vfs.CreateFile(@"C:\Temp\a.txt", "привет");

        Assert.Equal("привет", vfs.ReadAllText(@"C:\Temp\a.txt"));
    }

    [Fact]
    public void CreateDirectory_Existing_Throws()
    {
        VirtualFileSystem vfs = Create();

        vfs.CreateDirectory(@"C:\Temp");

        Assert.Throws<VfsException>(() => vfs.CreateDirectory(@"C:\Temp"));
    }

    [Fact]
    public void CreateFile_InMissingFolder_Throws()
    {
        VirtualFileSystem vfs = Create();

        Assert.Throws<VfsException>(() => vfs.CreateFile(@"C:\Nowhere\a.txt"));
    }

    [Fact]
    public void Delete_NonEmptyFolder_RequiresRecursive()
    {
        VirtualFileSystem vfs = Create();

        vfs.CreateDirectory(@"C:\Temp");
        vfs.CreateFile(@"C:\Temp\a.txt", "x");

        Assert.Throws<VfsException>(() => vfs.Delete(@"C:\Temp"));

        vfs.Delete(@"C:\Temp", recursive: true);

        Assert.False(vfs.Exists(@"C:\Temp"));
    }

    [Fact]
    public void Move_RenamesAndKeepsContent()
    {
        VirtualFileSystem vfs = Create();

        vfs.CreateFile(@"C:\a.txt", "1");
        vfs.Move(@"C:\a.txt", @"C:\b.txt");

        Assert.False(vfs.Exists(@"C:\a.txt"));
        Assert.Equal("1", vfs.ReadAllText(@"C:\b.txt"));
    }

    [Fact]
    public void Copy_DuplicatesContent()
    {
        VirtualFileSystem vfs = Create();

        vfs.CreateFile(@"C:\a.txt", "1");
        vfs.Copy(@"C:\a.txt", @"C:\b.txt");

        Assert.Equal("1", vfs.ReadAllText(@"C:\a.txt"));
        Assert.Equal("1", vfs.ReadAllText(@"C:\b.txt"));
    }

    [Fact]
    public void Enumerate_ReturnsSingleLevel()
    {
        VirtualFileSystem vfs = Create();

        IReadOnlyList<VfsNode> nodes = vfs.Enumerate(@"C:\Users\tester");

        Assert.Contains(nodes, n => n.Name == "Documents" && n.IsDirectory);
        Assert.DoesNotContain(nodes, n => n.Name == "Windows");
    }

    [Fact]
    public void Changed_RaisedOnMutation()
    {
        VirtualFileSystem vfs = Create();
        int raised = 0;

        vfs.Changed += (_, _) => raised++;

        vfs.CreateDirectory(@"C:\Temp");
        vfs.CreateFile(@"C:\Temp\a.txt", "x");
        vfs.WriteAllText(@"C:\Temp\a.txt", "y");

        Assert.Equal(3, raised);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("a/b", false)]
    [InlineData("CON", false)]
    [InlineData("file.", false)]
    [InlineData("file ", false)]
    [InlineData("file", true)]
    [InlineData("отчёт 2026.txt", true)]
    [InlineData("a:b", false)]
    public void IsValidName_MatchesWindowsRules(string name, bool expected)
    {
        Assert.Equal(expected, VfsPath.IsValidName(name, out _));
    }

    [Theory]
    [InlineData("a<b")]
    [InlineData("a|b")]
    [InlineData("*")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    public void IsValidName_RejectsIllegalNames(string name)
    {
        Assert.False(VfsPath.IsValidName(name, out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void GetParent_OfRoot_IsRoot()
    {
        Assert.Equal(@"C:\", VfsPath.GetParent(@"C:\"));
        Assert.Equal(@"C:\Users", VfsPath.GetParent(@"C:\Users\tester"));
    }

    [Fact]
    public void TotalSize_CountsFileContent()
    {
        VirtualFileSystem vfs = Create();

        vfs.CreateFile(@"C:\a.txt", "1234");

        Assert.Equal(8, vfs.Get(@"C:\a.txt")!.SizeBytes);      // UTF-16: 4 символа × 2 байта
    }

    [Fact]
    public void SaveAndLoad_RestoreTree()
    {
        string file = Path.Combine(Path.GetTempPath(), "win10sim-vfs-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var first = new VirtualFileSystem(file, "tester");
            first.CreateFile(@"C:\Users\tester\Documents\заметка.txt", "текст");
            first.Save();

            var second = new VirtualFileSystem(file, "tester");

            Assert.Equal("текст", second.ReadAllText(@"C:\Users\tester\Documents\заметка.txt"));
        }
        finally
        {
            File.Delete(file);
        }
    }
}

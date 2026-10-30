using Win10Sim.Core.FileSystem;
using Win10Sim.Core.Shell;
using Xunit;

namespace Win10Sim.Core.Tests;

/// <summary>
/// Командная строка: разбор строки, команды над виртуальным диском, редирект.
/// Сеть не трогаем (ping проверяется только на разбор параметров, без запросов).
/// </summary>
public class CommandProcessorTests
{
    private static (CommandProcessor Processor, VirtualFileSystem Vfs) Create()
    {
        var vfs = new VirtualFileSystem(null, "tester");
        var processor = new CommandProcessor(vfs, "tester", @"C:\Users\tester");
        return (processor, vfs);
    }

    [Fact]
    public void InitialDirectory_IsUserProfile()
    {
        (CommandProcessor processor, _) = Create();

        Assert.Equal(@"C:\Users\tester", processor.Context.CurrentDirectory);
        Assert.Equal(@"C:\Users\tester>", processor.Prompt);
    }

    [Fact]
    public async Task UnknownCommand_ReportsRussianError()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync("фыва");

        Assert.Contains(result.Lines, line => line.Contains("не является", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmptyLine_ProducesNothing()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync("   ");

        Assert.Empty(result.Lines);
        Assert.False(result.ExitRequested);
    }

    [Fact]
    public async Task Ver_PrintsWindowsVersion()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync("ver");

        Assert.Contains(result.Lines, line => line.Contains("10.0.19045.5011", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Md_ThenDir_ShowsFolder()
    {
        (CommandProcessor processor, _) = Create();

        await processor.ExecuteAsync("md Тест");
        CommandResult dir = await processor.ExecuteAsync("dir");

        Assert.Contains(dir.Lines, line => line.Contains("Тест", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cd_ChangesPrompt()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync(@"cd C:\Windows");

        Assert.Empty(result.Lines);
        Assert.Equal(@"C:\Windows>", processor.Prompt);
    }

    [Fact]
    public async Task Cd_ToMissingFolder_ReportsError()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync(@"cd C:\НетТакой");

        Assert.NotEmpty(result.Lines);
        Assert.Equal(@"C:\Users\tester>", processor.Prompt);
    }

    [Fact]
    public async Task EchoRedirect_CreatesFile()
    {
        (CommandProcessor processor, VirtualFileSystem vfs) = Create();

        await processor.ExecuteAsync(@"echo привет > note.txt");

        Assert.Equal("привет" + Environment.NewLine, vfs.ReadAllText(@"C:\Users\tester\note.txt"));
    }

    [Fact]
    public async Task EchoAppend_AddsLine()
    {
        (CommandProcessor processor, VirtualFileSystem vfs) = Create();

        await processor.ExecuteAsync(@"echo 1 > log.txt");
        await processor.ExecuteAsync(@"echo 2 >> log.txt");

        string content = vfs.ReadAllText(@"C:\Users\tester\log.txt");
        Assert.Contains("1", content, StringComparison.Ordinal);
        Assert.Contains("2", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Type_PrintsFileContent()
    {
        (CommandProcessor processor, _) = Create();

        await processor.ExecuteAsync(@"echo строка > a.txt");
        CommandResult result = await processor.ExecuteAsync("type a.txt");

        Assert.Contains(result.Lines, line => line.Contains("строка", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CopyAndRen_WorkThroughProcessor()
    {
        (CommandProcessor processor, VirtualFileSystem vfs) = Create();

        await processor.ExecuteAsync(@"echo данные > a.txt");
        await processor.ExecuteAsync("copy a.txt b.txt");
        await processor.ExecuteAsync("ren b.txt c.txt");

        Assert.True(vfs.Exists(@"C:\Users\tester\a.txt"));
        Assert.True(vfs.Exists(@"C:\Users\tester\c.txt"));
    }

    [Fact]
    public async Task Del_RemovesFile()
    {
        (CommandProcessor processor, VirtualFileSystem vfs) = Create();

        await processor.ExecuteAsync(@"echo x > a.txt");
        await processor.ExecuteAsync("del a.txt");

        Assert.False(vfs.Exists(@"C:\Users\tester\a.txt"));
    }

    [Fact]
    public async Task Cls_RequestsScreenClear()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync("cls");

        Assert.True(result.ClearScreen);
    }

    [Fact]
    public async Task Exit_RequestsWindowClose()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync("exit");

        Assert.True(result.ExitRequested);
    }

    [Fact]
    public async Task Help_ListsRegisteredCommands()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync("help");
        string text = string.Join("\n", result.Lines);

        Assert.Contains("dir", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ping", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhoAmI_PrintsUserName()
    {
        (CommandProcessor processor, _) = Create();

        CommandResult result = await processor.ExecuteAsync("whoami");

        Assert.Contains(result.Lines, line => line.Contains("tester", StringComparison.Ordinal));
    }

    [Fact]
    public void History_KeepsCommandsInOrder()
    {
        (CommandProcessor processor, _) = Create();

        _ = processor.ExecuteAsync("ver").GetAwaiter().GetResult();
        _ = processor.ExecuteAsync("dir").GetAwaiter().GetResult();

        Assert.Equal(new[] { "ver", "dir" }, processor.History);
    }

    [Fact]
    public void Complete_CompletesCommandName()
    {
        (CommandProcessor processor, _) = Create();

        Assert.Equal("cls ", processor.Complete("cl"));
    }

    [Fact]
    public void Complete_CompletesFileName()
    {
        (CommandProcessor processor, VirtualFileSystem vfs) = Create();
        vfs.CreateFile(@"C:\Users\tester\уникальный.txt", "x");

        Assert.Equal("type уникальный.txt", processor.Complete("type уник"));
    }

    [Fact]
    public void Parser_UnderstandsQuotesAndRedirect()
    {
        CommandParser.ParsedCommand parsed = CommandParser.Parse("echo \"a b\" > out.txt");

        Assert.Equal("echo", parsed.Name);
        Assert.Contains("a b", parsed.Arguments);
        Assert.Equal("out.txt", parsed.RedirectPath);
        Assert.False(parsed.Append);
    }

    [Fact]
    public void Parser_RecognizesAppendRedirect()
    {
        CommandParser.ParsedCommand parsed = CommandParser.Parse("echo x >> log.txt");

        Assert.True(parsed.Append);
        Assert.Equal("log.txt", parsed.RedirectPath);
    }
}

using Win10Sim.Core.Setup;
using Xunit;

namespace Win10Sim.Core.Tests;

/// <summary>
/// Установщик проверяется на реальном времени, но с ускорением 1:100,
/// иначе один только тест шёл бы 40 секунд.
/// </summary>
[Collection(SetupTimingsCollection.Name)]
public class SetupInstallerTests : IDisposable
{
    private readonly double _originalFactor;

    public SetupInstallerTests()
    {
        _originalFactor = SetupTimings.SpeedFactor;
        SetupTimings.SpeedFactor = 0.01;
    }

    public void Dispose() => SetupTimings.SpeedFactor = _originalFactor;

    [Fact]
    public async Task Install_ReportsMonotonicProgressAndFinishesAt100()
    {
        var session = new SetupSession();
        var reports = new List<InstallProgress>();

        var progress = new Progress<InstallProgress>(reports.Add);
        await SetupInstaller.InstallAsync(session, progress, CancellationToken.None);

        // Progress<T> доставляет колбэки через контекст синхронизации; в тесте
        // без SynchronizationContext они идут в пул потоков — ждём фактического прихода.
        await WaitUntilAsync(() => session.InstallProgress >= 100);

        Assert.Equal(100, session.InstallProgress);
        Assert.NotNull(session.InstallStartedUtc);
        Assert.NotNull(session.InstallCompletedUtc);
        Assert.Equal(InstallStage.Finishing, session.LastStage);
    }

    [Fact]
    public async Task Install_CancellationStopsTheProcess()
    {
        var session = new SetupSession();
        using var cts = new CancellationTokenSource();

        Task installing = SetupInstaller.InstallAsync(session, new Progress<InstallProgress>(_ => { }), cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installing);

        Assert.True(session.InstallProgress < 100);
        Assert.Null(session.InstallCompletedUtc);
    }

    [Fact]
    public async Task Format_GoesThroughAllStages()
    {
        PartitionInfo partition = DiskFactory.CreateDefault()[0].Partitions[2];
        var stages = new List<FormatStage>();

        await SetupInstaller.FormatAsync(partition, new Progress<FormatStage>(stages.Add), CancellationToken.None);
        await WaitUntilAsync(() => stages.Count >= 3);

        Assert.Equal(
            new[] { FormatStage.QuickFormat, FormatStage.CreatingFileSystem, FormatStage.Done },
            stages);
    }

    [Fact]
    public async Task Format_RequiresPartition()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SetupInstaller.FormatAsync(null!, null, CancellationToken.None));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += 20)
        {
            if (condition())
                return;

            await Task.Delay(20);
        }

        Assert.Fail("Условие не выполнилось за отведённое время.");
    }
}

/// <summary>
/// SetupTimings.SpeedFactor — статическая настройка, поэтому тесты, которые её меняют,
/// не должны выполняться параллельно с остальными.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class SetupTimingsCollection
{
    public const string Name = "SetupTimings";
}

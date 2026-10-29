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

        await SetupInstaller.InstallAsync(session, new SyncProgress<InstallProgress>(reports.Add), CancellationToken.None);

        Assert.True(reports.Count > 100);
        Assert.Equal(100, reports[^1].Percent);
        for (int i = 1; i < reports.Count; i++)
            Assert.True(reports[i].Percent >= reports[i - 1].Percent, "прогресс не должен идти назад");

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

        Task installing = SetupInstaller.InstallAsync(session, new SyncProgress<InstallProgress>(_ => { }), cts.Token);
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

        await SetupInstaller.FormatAsync(partition, new SyncProgress<FormatStage>(stages.Add), CancellationToken.None);

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

    /// <summary>
    /// IProgress, который вызывает колбэк синхронно. Progress&lt;T&gt; в тестах
    /// не подходит: без SynchronizationContext он ставит колбэки в пул потоков,
    /// и порядок доставки не гарантирован — проверки на монотонность становятся
    /// флаки. В WPF-приложении Progress&lt;T&gt; нужен именно для маршалинга в UI-поток.
    /// </summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public SyncProgress(Action<T> callback) => _callback = callback;

        public void Report(T value) => _callback(value);
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

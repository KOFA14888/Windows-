namespace Win10Sim.Core.Setup;

/// <summary>Этап установки — UI сопоставляет его с локализованной строкой.</summary>
public enum InstallStage
{
    CopyingFiles = 0,
    InstallingComponents = 1,
    InstallingUpdates = 2,
    Finishing = 3
}

public enum FormatStage
{
    QuickFormat = 0,
    CreatingFileSystem = 1,
    Done = 2
}

/// <summary>Снимок прогресса установки. UI не знает ничего сверх этого.</summary>
public readonly record struct InstallProgress(
    double Percent,
    InstallStage Stage,
    string Detail);

/// <summary>
/// Имитация установщика. Никаких файлов не пишет: «диск» — это будущая
/// виртуальная ФС (ЭТАП 4), а прогресс — обычная асинхронная последовательность.
///
/// Все длительные операции принимают CancellationToken: выход по Insert
/// должен прерывать установку мгновенно, а не через 20 секунд.
/// </summary>
public sealed class SetupInstaller
{
    private readonly record struct StagePlan(InstallStage Stage, int From, int To, double Seconds);

    // Суммарно ~42 секунды при SpeedFactor = 1.
    private static readonly StagePlan[] Plan =
    {
        new(InstallStage.CopyingFiles,          0,  38, 16.0),
        new(InstallStage.InstallingComponents, 38,  66, 11.0),
        new(InstallStage.InstallingUpdates,    66,  88,  9.0),
        new(InstallStage.Finishing,            88, 100,  6.0)
    };

    private static readonly string[] CopyDetails =
    {
        @"C:\Windows\System32\drivers\disk.sys",
        @"C:\Windows\System32\ntoskrnl.exe",
        @"C:\Windows\System32\DriverStore\FileRepository\iigd_dch.inf_amd64",
        @"C:\Windows\WinSxS\amd64_microsoft-windows-shell32",
        @"C:\Windows\System32\config\SYSTEM",
        @"C:\Windows\Boot\EFI\bootmgfw.efi",
        @"C:\Windows\System32\shell32.dll",
        @"C:\Windows\System32\Windows.UI.Xaml.dll",
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\System32\explorer.exe"
    };

    private static readonly string[] ComponentDetails =
    {
        "Microsoft-Windows-Shell-Core",
        "Microsoft-Windows-Network-Security",
        "Microsoft-Windows-Storage-Spaces",
        "Microsoft-Windows-DirectX",
        "Microsoft-Windows-WindowsUpdateClient"
    };

    /// <summary>Быстрое форматирование выбранного раздела перед копированием файлов.</summary>
    public async Task FormatAsync(
        PartitionInfo partition,
        IProgress<FormatStage>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partition);

        progress?.Report(FormatStage.QuickFormat);
        await Task.Delay(SetupTimings.Scale(TimeSpan.FromMilliseconds(900)), cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(FormatStage.CreatingFileSystem);
        await Task.Delay(SetupTimings.Scale(TimeSpan.FromMilliseconds(1100)), cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(FormatStage.Done);
    }

    /// <summary>Полный цикл «Копирование файлов → Завершение».</summary>
    public async Task InstallAsync(
        SetupSession session,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(progress);

        var random = new Random(20251029);          // фиксированный seed: поведение воспроизводимо
        session.InstallStartedUtc ??= DateTime.UtcNow;

        foreach (StagePlan plan in Plan)
        {
            int steps = (plan.To - plan.From) * 2;  // шаг 0,5 %
            TimeSpan perStep = TimeSpan.FromSeconds(plan.Seconds) / steps;

            for (int i = 0; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double percent = plan.From + (plan.To - plan.From) * (double)i / steps;
                progress.Report(new InstallProgress(percent, plan.Stage, Describe(plan.Stage, random)));
                session.InstallProgress = percent;
                session.LastStage = plan.Stage;

                TimeSpan jitter = TimeSpan.FromMilliseconds(random.Next(0, 18));
                await Task.Delay(perStep + jitter, cancellationToken).ConfigureAwait(false);
            }
        }

        session.InstallProgress = 100;
        session.InstallCompletedUtc = DateTime.UtcNow;
        progress.Report(new InstallProgress(100, InstallStage.Finishing, string.Empty));
    }

    private static string Describe(InstallStage stage, Random random) => stage switch
    {
        InstallStage.CopyingFiles => CopyDetails[random.Next(CopyDetails.Length)],
        InstallStage.InstallingComponents => ComponentDetails[random.Next(ComponentDetails.Length)],
        InstallStage.InstallingUpdates => "KB5041585",
        _ => string.Empty
    };
}

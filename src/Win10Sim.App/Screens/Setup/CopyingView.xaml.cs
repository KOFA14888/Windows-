using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Прогресс установки: 0–100 % с реальными этапами и путями копируемых файлов.
///
/// Прогресс приходит из ядра через IProgress&lt;InstallProgress&gt;, то есть
/// колбэки выполняются на UI-потоке. Никаких Thread.Sleep и обращений к
/// контролам из фонового потока.
/// </summary>
public partial class CopyingView : SetupScreenBase
{
    private static readonly TimeSpan RebootDelay = TimeSpan.FromSeconds(3.5);

    public CopyingView(AppServices services)
        : base(services)
    {
        InitializeComponent();
    }

    // Назад с этого экрана нет: отмена установки — только выход по Insert.
    protected override OsPhase? BackTarget => null;

    public override async Task OnEnterAsync(CancellationToken cancellationToken)
    {
        var progress = new Progress<InstallProgress>(OnProgress);

        await SetupInstaller.InstallAsync(Session, progress, cancellationToken).ConfigureAwait(true);

        RebootHint.Visibility = System.Windows.Visibility.Visible;
        await Task.Delay(SetupTimings.Scale(RebootDelay), cancellationToken).ConfigureAwait(true);

        Advance(OsPhase.Reboot);
    }

    private void OnProgress(InstallProgress progress)
    {
        Progress.Value = progress.Percent;
        PercentText.Text = UiStrings.Format("L.Copy.Percent", (int)Math.Round(progress.Percent, MidpointRounding.AwayFromZero));
        StageText.Text = UiStrings.Get(StageResourceKey(progress.Stage));
        DetailText.Text = progress.Detail;
    }

    private static string StageResourceKey(InstallStage stage) => stage switch
    {
        InstallStage.CopyingFiles => "L.Copy.Stage.CopyingFiles",
        InstallStage.InstallingComponents => "L.Copy.Stage.InstallingComponents",
        InstallStage.InstallingUpdates => "L.Copy.Stage.InstallingUpdates",
        _ => "L.Copy.Stage.Finishing"
    };
}

using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// «Мы готовим всё для вас»: этапы идут один за другим с процентами.
/// После завершения учётная запись фиксируется, а фаза уходит в LockScreen —
/// там ЭТАП 3 подхватит управление.
/// </summary>
public partial class OobePreparingView : OobeScreenBase
{
    public OobePreparingView(AppServices services)
        : base(services)
    {
        InitializeComponent();
    }

    // Отсюда назад пути нет: учётная запись уже создана.
    protected override OsPhase? BackTarget => null;

    public override async Task OnEnterAsync(CancellationToken cancellationToken)
    {
        var progress = new Progress<PreparingProgress>(OnProgress);
        await OobePreparer.PrepareAsync(progress, cancellationToken).ConfigureAwait(true);

        Session.OobeCompletedUtc = DateTime.UtcNow;
        Services.SaveState();

        // Небольшая пауза на 100 %, затем — экран блокировки (ЭТАП 3).
        await Task.Delay(SetupTimings.Scale(TimeSpan.FromMilliseconds(900)), cancellationToken)
            .ConfigureAwait(true);

        Advance(OsPhase.LockScreen);
    }

    private void OnProgress(PreparingProgress progress)
    {
        Progress.Value = progress.Percent;
        StepText.Text = UiStrings.Get(StepKey(progress.Step));
    }

    private static string StepKey(PreparingStep step) => step switch
    {
        PreparingStep.ApplyingSettings => "L.Oobe.Preparing.Step.ApplyingSettings",
        PreparingStep.ConfiguringDisplay => "L.Oobe.Preparing.Step.ConfiguringDisplay",
        PreparingStep.InstallingApps => "L.Oobe.Preparing.Step.InstallingApps",
        PreparingStep.ConnectingAccount => "L.Oobe.Preparing.Step.ConnectingAccount",
        PreparingStep.CreatingProfile => "L.Oobe.Preparing.Step.CreatingProfile",
        _ => "L.Oobe.Preparing.Step.SettingUpCortana"
    };
}

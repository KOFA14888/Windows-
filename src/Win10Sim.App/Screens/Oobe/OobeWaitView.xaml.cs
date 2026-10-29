using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// «Подождите…» / «Just a moment…». Держится недолго: в настоящей установке
/// здесь идёт инициализация драйверов, у нас — фиксированная пауза.
/// </summary>
public partial class OobeWaitView : OobeScreenBase
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(3.2);

    public OobeWaitView(AppServices services)
        : base(services)
    {
        InitializeComponent();
    }

    public override async Task OnEnterAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(SetupTimings.Scale(Duration), cancellationToken).ConfigureAwait(true);
        Advance(OsPhase.OobeRegion);
    }
}

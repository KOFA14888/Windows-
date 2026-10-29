using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// «Привет». В настоящей Windows здесь показывается короткое приветствие,
/// пока поднимаются службы пользователя, — поэтому экран живёт ~2.5 с.
/// </summary>
public partial class OobeHelloView : OobeScreenBase
{
    private static readonly TimeSpan GreetingDuration = TimeSpan.FromSeconds(2.5);

    public OobeHelloView(AppServices services)
        : base(services)
    {
        InitializeComponent();
    }

    public override async Task OnEnterAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(SetupTimings.Scale(GreetingDuration), cancellationToken).ConfigureAwait(true);
        Advance(OsPhase.OobePreparing);
    }
}

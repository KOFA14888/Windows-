using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Boot;

/// <summary>
/// Экран загрузки/перезагрузки. Используется дважды за проход:
/// до установки (WindowsLogo → язык) и после копирования файлов
/// (Reboot → OOBE).
/// </summary>
public partial class BootLogoView : SetupScreenBase
{
    private readonly OsPhase _nextPhase;
    private readonly TimeSpan _delay;

    public BootLogoView(AppServices services, OsPhase nextPhase, TimeSpan delay)
        : base(services)
    {
        _nextPhase = nextPhase;
        _delay = delay;
        InitializeComponent();
    }

    public override async Task OnEnterAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(SetupTimings.Scale(_delay), cancellationToken).ConfigureAwait(true);
        Advance(_nextPhase);
    }
}

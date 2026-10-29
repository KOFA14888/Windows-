using System.Windows;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>Экран «Windows 10 → Установить сейчас». Аналог окна setup.exe.</summary>
public partial class InstallNowView : SetupScreenBase
{
    public InstallNowView(AppServices services)
        : base(services)
    {
        InitializeComponent();
    }

    protected override OsPhase? BackTarget => OsPhase.SetupLanguage;

    private void OnInstallNowClick(object sender, RoutedEventArgs e) => Advance(OsPhase.SetupKey);
}

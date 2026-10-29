using System.Windows;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Placeholder;

/// <summary>
/// Конец ЭТАПА 1 (и запасной экран для фаз, которых ещё нет).
/// Показывает итог установки и позволяет начать заново или выйти по Insert.
/// </summary>
public partial class SetupCompleteView : SetupScreenBase
{
    public SetupCompleteView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        KeyText.Text = Session.ProductKey is { Length: > 0 } key
            ? UiStrings.Format("L.Stage2.Key", key)
            : UiStrings.Get("L.Stage2.NoKey");

        EditionText.Text = UiStrings.Format("L.Stage2.Edition", Session.Edition.ProductName());

        DiskText.Text = UiStrings.Format(
            "L.Stage2.Disk",
            UiStrings.Format("L.Disk.Drive", Session.SelectedDiskIndex) + ", " +
            UiStrings.Format("L.Disk.Partition", Session.SelectedPartitionIndex));
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        // BootFlow по этой фазе выполнит ResetForNewInstall() и снова покажет логотип.
        Advance(OsPhase.Firmware);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Services.Host.RequestShutdown();
}

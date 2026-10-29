using System.Windows;
using System.Windows.Controls;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Тип установки. «Обновление» недоступно так же, как при загрузке с носителя:
/// обновлять нечего, существующей системы нет. Единственный рабочий вариант —
/// «Выборочная», поэтому он выбран сразу.
/// </summary>
public partial class InstallTypeView : SetupScreenBase
{
    public InstallTypeView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        UpgradeOption.IsEnabled = false;
        CustomOption.IsChecked = true;
        Session.InstallType = InstallType.Custom;
    }

    protected override OsPhase? BackTarget => OsPhase.SetupEula;

    private void OnTypeChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && Enum.TryParse(tag, out InstallType type))
        {
            Session.InstallType = type;
            Services.SaveState();
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e) => Advance(OsPhase.SetupDisk);

    private void OnBackClick(object sender, RoutedEventArgs e) => Advance(OsPhase.SetupEula);
}

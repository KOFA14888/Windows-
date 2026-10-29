using System.Windows;
using System.Windows.Controls;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Выбор редакции. Экран появляется только когда ключа нет —
/// при известном ключе редакция определена им и шаг пропускается.
/// </summary>
public partial class EditionView : SetupScreenBase
{
    public EditionView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        switch (Session.Edition)
        {
            case WindowsEdition.Home:
                HomeOption.IsChecked = true;
                break;
            case WindowsEdition.Enterprise:
                EnterpriseOption.IsChecked = true;
                break;
            default:
                ProOption.IsChecked = true;
                break;
        }
    }

    protected override OsPhase? BackTarget => OsPhase.SetupKey;

    private void OnEditionChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag } || !Enum.TryParse(tag, out WindowsEdition edition))
            return;

        Session.Edition = edition;
        Services.SaveState();
    }

    private void OnNextClick(object sender, RoutedEventArgs e) => Advance(OsPhase.SetupEula);

    private void OnBackClick(object sender, RoutedEventArgs e) => Advance(OsPhase.SetupKey);
}

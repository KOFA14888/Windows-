using System.Windows;
using System.Windows.Controls;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// «Ваш регион?» — как в настоящей Windows: сначала показывается регион,
/// определённый по IP/языку, с кнопками «Да, верно» / «Нет, выбрать другой».
/// Только после отказа появляется список.
/// </summary>
public partial class OobeRegionView : OobeScreenBase
{
    private bool _initializing = true;

    public OobeRegionView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        // Регион по умолчанию выводится из языка установки — как в реальной OOBE.
        RegionInfo detected = Session.RegionCode is { Length: > 0 } code
            ? RegionCatalog.Find(code)
            : RegionCatalog.DefaultFor(Session.InstallLanguage);

        DetectedText.Text = UiStrings.Format("L.Oobe.Region.Detected", detected.DisplayName);
        TimeZoneText.Text = UiStrings.Format("L.Oobe.Region.TimeZone", detected.TimeZone);

        foreach (RegionInfo region in RegionCatalog.All)
            RegionCombo.Items.Add(new ComboBoxItem { Content = $"{region.DisplayName}  ·  {region.Code}", Tag = region });

        int index = RegionCatalog.All.ToList().FindIndex(r => r.Code == detected.Code);
        RegionCombo.SelectedIndex = index >= 0 ? index : 0;

        _initializing = false;
    }

    protected override OsPhase? BackTarget => OsPhase.OobeWait;

    private void OnYesClick(object sender, RoutedEventArgs e)
    {
        SaveSelection();
        Advance(OsPhase.OobeKeyboard);
    }

    private void OnNoClick(object sender, RoutedEventArgs e)
    {
        DetectedPanel.Visibility = Visibility.Collapsed;
        ListPanel.Visibility = Visibility.Visible;
        RegionCombo.Focus();
        RegionCombo.IsDropDownOpen = true;
    }

    private void OnRegionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing)
            SaveSelection();
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        SaveSelection();
        Advance(OsPhase.OobeKeyboard);
    }

    private void SaveSelection()
    {
        if (RegionCombo.SelectedItem is not ComboBoxItem { Tag: RegionInfo region })
            return;

        Session.RegionCode = region.Code;
        Session.RegionName = region.DisplayName;
        Services.SaveState();
    }
}

using System.Windows;
using System.Windows.Controls;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// Проверка раскладки: основная и вторая (необязательная). Значения
/// сохраняются сразу — экран можно пройти, не трогая списки.
/// </summary>
public partial class OobeKeyboardView : OobeScreenBase
{
    private bool _initializing = true;

    public OobeKeyboardView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        foreach (KeyboardInfo keyboard in KeyboardCatalog.All)
            PrimaryCombo.Items.Add(new ComboBoxItem { Content = $"{keyboard.DisplayName}  ·  {keyboard.LayoutName}", Tag = keyboard });

        SecondaryCombo.Items.Add(new ComboBoxItem { Content = UiStrings.Get("L.Oobe.Keyboard.None"), Tag = null });
        foreach (KeyboardInfo keyboard in KeyboardCatalog.All)
            SecondaryCombo.Items.Add(new ComboBoxItem { Content = $"{keyboard.DisplayName}  ·  {keyboard.LayoutName}", Tag = keyboard });

        KeyboardInfo primary = Session.PrimaryKeyboard is { Length: > 0 } savedPrimary
            ? KeyboardCatalog.Find(savedPrimary)
            : KeyboardCatalog.DefaultFor(Session.InstallLanguage);

        int primaryIndex = KeyboardCatalog.All.ToList().FindIndex(k => k.Code == primary.Code);
        PrimaryCombo.SelectedIndex = primaryIndex >= 0 ? primaryIndex : 0;

        int secondaryIndex = 0;
        if (Session.SecondaryKeyboard is { Length: > 0 } savedSecondary)
        {
            int found = KeyboardCatalog.All.ToList().FindIndex(k => k.Code == savedSecondary);
            if (found >= 0)
                secondaryIndex = found + 1;      // +1: первый пункт — «Не добавлять»
        }

        SecondaryCombo.SelectedIndex = secondaryIndex;

        _initializing = false;
        SaveSelection();
    }

    protected override OsPhase? BackTarget => OsPhase.OobeRegion;

    private void OnPrimaryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing)
            SaveSelection();
    }

    private void OnSecondaryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing)
            SaveSelection();
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        SaveSelection();
        Advance(OsPhase.OobeAccount);
    }

    private void SaveSelection()
    {
        if (PrimaryCombo.SelectedItem is ComboBoxItem { Tag: KeyboardInfo primary })
            Session.PrimaryKeyboard = primary.Code;

        Session.SecondaryKeyboard =
            SecondaryCombo.SelectedItem is ComboBoxItem { Tag: KeyboardInfo secondary }
                ? secondary.Code
                : null;

        Services.SaveState();
    }
}

using System.Windows.Controls;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Первый экран установки: язык, формат времени, раскладка.
///
/// Ключевая деталь достоверности: выбор языка немедленно меняет язык ВСЕЙ
/// программы установки (через LocalizationService и DynamicResource),
/// а не только этого экрана, как и в настоящем setup.
/// </summary>
public partial class LanguageView : SetupScreenBase
{
    private sealed record LanguageOption(string Culture, string NativeName, string TimeFormat, string Keyboard);

    // Названия языков — на самом языке: они не должны меняться при смене UI-языка.
    private static readonly LanguageOption[] Languages =
    {
        new("ru-RU", "Русский (Россия)", "Русский (Россия)", "Русская"),
        new("en-US", "English (United States)", "English (United States)", "US"),
        new("de-DE", "Deutsch (Deutschland)", "Deutsch (Deutschland)", "Deutsch")
    };

    private bool _initializing = true;

    public LanguageView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        foreach (LanguageOption language in Languages)
            InstallLanguageCombo.Items.Add(new ComboBoxItem { Content = language.NativeName, Tag = language });

        // Восстанавливаем выбор, если установка уже начиналась.
        int index = Array.FindIndex(Languages, l => l.Culture == Session.InstallLanguage);
        InstallLanguageCombo.SelectedIndex = index >= 0 ? index : 0;

        RebuildDependentLists(Languages[Math.Max(0, InstallLanguageCombo.SelectedIndex)]);
        _initializing = false;
    }

    /// <summary>Список форматов времени и раскладок зависит от языка установки.</summary>
    private void RebuildDependentLists(LanguageOption language)
    {
        TimeFormatCombo.Items.Clear();
        KeyboardCombo.Items.Clear();

        // Свой язык — первым, английский (США) — как «международный» вариант.
        var formats = new List<string> { language.TimeFormat };
        if (language.Culture != "en-US")
            formats.Add("English (United States)");

        var keyboards = new List<string> { language.Keyboard };
        if (language.Culture != "en-US")
            keyboards.Add("US");

        foreach (string format in formats)
            TimeFormatCombo.Items.Add(new ComboBoxItem { Content = format });

        foreach (string keyboard in keyboards)
            KeyboardCombo.Items.Add(new ComboBoxItem { Content = keyboard });

        TimeFormatCombo.SelectedIndex = 0;
        KeyboardCombo.SelectedIndex = 0;
    }

    private void OnInstallLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || InstallLanguageCombo.SelectedItem is not ComboBoxItem { Tag: LanguageOption language })
            return;

        _initializing = true;
        RebuildDependentLists(language);
        _initializing = false;

        Session.InstallLanguage = language.Culture;
        Session.TimeAndCurrencyFormat = language.TimeFormat;
        Session.KeyboardLayout = language.Keyboard;

        Services.Localization.Apply(language.Culture);
        Services.SaveState();
    }

    private void OnTimeFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && TimeFormatCombo.SelectedItem is ComboBoxItem item)
            Session.TimeAndCurrencyFormat = item.Content?.ToString() ?? Session.TimeAndCurrencyFormat;
    }

    private void OnKeyboardChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && KeyboardCombo.SelectedItem is ComboBoxItem item)
            Session.KeyboardLayout = item.Content?.ToString() ?? Session.KeyboardLayout;
    }

    private void OnNextClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (InstallLanguageCombo.SelectedItem is not ComboBoxItem { Tag: LanguageOption language })
            return;

        Session.InstallLanguage = language.Culture;
        Session.TimeAndCurrencyFormat =
            TimeFormatCombo.SelectedItem is ComboBoxItem { Content: string timeFormat }
                ? timeFormat
                : language.TimeFormat;
        Session.KeyboardLayout =
            KeyboardCombo.SelectedItem is ComboBoxItem { Content: string keyboard }
                ? keyboard
                : language.Keyboard;

        Services.SaveState();
        Advance(OsPhase.SetupInstall);
    }
}

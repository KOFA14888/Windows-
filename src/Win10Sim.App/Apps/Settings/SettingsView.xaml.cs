using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;
using Win10Sim.Core.FileSystem;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Apps.Settings;

/// <summary>
/// «Параметры» Windows 10. Разделы повторяют настоящие: система, персонализация,
/// время и язык, учётные записи, о системе.
///
/// Настройки реально применяются: тема и формат часов пишутся в desktop.json,
/// язык переключает словари локализации, а сведения о компьютере берутся из
/// выбранного выпуска Windows и виртуального диска.
/// </summary>
public partial class SettingsView : UserControl, ISimApp
{
    private readonly AppServices _services;
    private bool _loading = true;

    public SettingsView(AppServices services)
    {
        _services = services;

        InitializeComponent();

        FillValues();
        _loading = false;
    }

    public void HandleArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return;

        // «Параметры» понимают имя раздела: settings about и т. п.
        string key = arguments.Trim().ToLowerInvariant();

        ShowPage(key switch
        {
            "personalization" or "theme" or "персонализация" => "personalization",
            "time" or "language" or "время" => "time",
            "accounts" or "user" or "учётные" or "учетные" => "accounts",
            "about" or "о системе" => "about",
            _ => "system"
        });
    }

    private void FillValues()
    {
        SetupSession session = _services.Session;

        DeviceNameText.Text = Environment.MachineName;
        DiskText.Text = UiStrings.FormatSize(_services.FileSystem.FreeSpaceBytes)
                        + " / "
                        + UiStrings.FormatSize(_services.FileSystem.FreeSpaceBytes + UsedSpace())
                        + " (" + VfsPath.Drive + ")";

        UserText.Text = _services.DisplayName + " (" + _services.UserName + ")";
        PasswordText.Text = session.Account is { HasPassword: true }
            ? UiStrings.Get("L.Settings.PasswordSet")
            : UiStrings.Get("L.Settings.PasswordNone");

        EditionText.Text = "Windows 10 " + session.Edition;

        VersionText.Text = "10.0.19045 (Windows 10 22H2)";
        ProcessorText.Text = "Виртуальный процессор симулятора (x64)";
        MemoryText.Text = "8,00 " + UiStrings.Get("L.Unit.GB");

        SelectTag(ThemeBox, _services.Settings.Theme);
        SelectTag(ClockBox, _services.Settings.ClockFormat);
        SelectTag(LanguageBox, _services.Localization.Culture);
    }

    private long UsedSpace()
    {
        long total = 0;
        Walk(_services.FileSystem.Root);
        return total;

        void Walk(VfsNode node)
        {
            if (!node.IsDirectory)
            {
                total += node.SizeBytes;
                return;
            }

            foreach (VfsNode child in node.Children)
                Walk(child);
        }
    }

    private static void SelectTag(ComboBox box, string tag)
    {
        foreach (object item in box.Items)
        {
            if (item is ComboBoxItem { Tag: string value }
                && string.Equals(value, tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }

        if (box.Items.Count > 0)
            box.SelectedIndex = 0;
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked)
            return;

        string page = clicked.Name switch
        {
            "NavPersonalization" => "personalization",
            "NavTimeLanguage" => "time",
            "NavAccounts" => "accounts",
            "NavAbout" => "about",
            _ => "system"
        };

        ShowPage(page);
    }

    private void ShowPage(string page)
    {
        PageSystem.Visibility = page == "system" ? Visibility.Visible : Visibility.Collapsed;
        PagePersonalization.Visibility = page == "personalization" ? Visibility.Visible : Visibility.Collapsed;
        PageTimeLanguage.Visibility = page == "time" ? Visibility.Visible : Visibility.Collapsed;
        PageAccounts.Visibility = page == "accounts" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = page switch
        {
            "personalization" => UiStrings.Get("L.Settings.Personalization"),
            "time" => UiStrings.Get("L.Settings.TimeLanguage"),
            "accounts" => UiStrings.Get("L.Settings.Accounts"),
            "about" => UiStrings.Get("L.Settings.About"),
            _ => UiStrings.Get("L.Settings.System")
        };

        // Подсветка выбранного раздела — через Tag, который читает стиль.
        NavSystem.Tag = page == "system" ? "selected" : null;
        NavPersonalization.Tag = page == "personalization" ? "selected" : null;
        NavTimeLanguage.Tag = page == "time" ? "selected" : null;
        NavAccounts.Tag = page == "accounts" ? "selected" : null;
        NavAbout.Tag = page == "about" ? "selected" : null;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || (ThemeBox.SelectedItem as ComboBoxItem)?.Tag is not string theme)
            return;

        _services.Settings.Theme = theme;
        _services.Settings.Save();
        NotifyApplied();
    }

    private void OnClockChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || (ClockBox.SelectedItem as ComboBoxItem)?.Tag is not string format)
            return;

        _services.Settings.ClockFormat = format;
        _services.Settings.Save();
        NotifyApplied();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || (LanguageBox.SelectedItem as ComboBoxItem)?.Tag is not string culture)
            return;

        _services.Localization.Apply(culture);
        NotifyApplied();
    }

    private void NotifyApplied()
    {
        AppliedText.Visibility = Visibility.Visible;

        // «Изменения применены» гаснет само — как всплывающая подсказка.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            AppliedText.Visibility = Visibility.Collapsed;
        };
        timer.Start();
    }
}

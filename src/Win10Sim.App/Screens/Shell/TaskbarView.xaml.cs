using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;

namespace Win10Sim.App.Screens.Shell;

/// <summary>
/// Панель задач. Отображает запущенные окна и часы; все действия передаёт
/// наружу событиями, чтобы не знать про «Пуск» и рабочий стол.
/// </summary>
public partial class TaskbarView : UserControl
{
    private readonly Dictionary<SimWindow, Button> _buttons = new();

    public TaskbarView()
    {
        InitializeComponent();
    }

    public event EventHandler? StartClicked;

    public event EventHandler? SearchClicked;

    public event EventHandler? NotificationsClicked;

    public event EventHandler? ClockClicked;

    public event EventHandler? ShowDesktopClicked;

    public event EventHandler? NetworkClicked;

    public event EventHandler? SoundClicked;

    public event EventHandler? LanguageClicked;

    public event EventHandler<SimWindow>? WindowClicked;

    /// <summary>Кнопка «Пуск» подсвечивается, когда меню открыто.</summary>
    public void SetStartMenuOpen(bool open)
    {
        StartButton.Tag = open ? "active" : null;
        SearchBox.Background = open
            ? new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF))
            : new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    }

    public void UpdateClock(DateTime now, string timeFormat)
    {
        ClockText.Text = now.ToString(timeFormat, CultureInfo.CurrentCulture);
        DateText.Text = now.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
    }

    public void SetLanguageBadge(string text) => LanguageButton.Content = text;

    public void SetSearchHint(string text) => SearchHint.Text = text;

    /// <summary>Перестраивает кнопки по списку открытых окон (порядок — порядок открытия).</summary>
    public void SyncWindows(IReadOnlyList<SimWindow> windows, SimWindow? active)
    {
        // Удаляем кнопки закрытых окон.
        foreach (SimWindow closed in _buttons.Keys.Where(w => !windows.Contains(w)).ToList())
        {
            AppButtons.Children.Remove(_buttons[closed]);
            _buttons.Remove(closed);
        }

        foreach (SimWindow window in windows)
        {
            if (!_buttons.TryGetValue(window, out Button? button))
            {
                button = CreateAppButton(window);
                _buttons[window] = button;
                AppButtons.Children.Add(button);
            }

            button.Tag = ReferenceEquals(window, active)
                ? "active"
                : window.IsMinimized ? null : "running";

            button.Opacity = window.IsMinimized ? 0.75 : 1.0;
        }
    }

    private Button CreateAppButton(SimWindow window)
    {
        var button = new Button
        {
            Style = (Style)FindResource("Button.TaskbarApp"),
            Content = new TextBlock
            {
                Text = AppGlyph(window.AppId),
                FontFamily = (FontFamily)FindResource("Font.Icons"),
                FontSize = 17,
                Foreground = Brushes.White
            },
            ToolTip = window.Title
        };

        button.Click += (_, _) => WindowClicked?.Invoke(this, window);
        return button;
    }

    private static string AppGlyph(string appId)
        => AppCatalog.Find(appId)?.Glyph ?? "\uE7C4";

    private void OnStartClick(object sender, RoutedEventArgs e) => StartClicked?.Invoke(this, EventArgs.Empty);

    private void OnSearchClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => SearchClicked?.Invoke(this, EventArgs.Empty);

    private void OnNotificationsClick(object sender, RoutedEventArgs e) => NotificationsClicked?.Invoke(this, EventArgs.Empty);

    private void OnClockClick(object sender, RoutedEventArgs e) => ClockClicked?.Invoke(this, EventArgs.Empty);

    private void OnShowDesktopClick(object sender, RoutedEventArgs e) => ShowDesktopClicked?.Invoke(this, EventArgs.Empty);

    private void OnNetworkClick(object sender, RoutedEventArgs e) => NetworkClicked?.Invoke(this, EventArgs.Empty);

    private void OnSoundClick(object sender, RoutedEventArgs e) => SoundClicked?.Invoke(this, EventArgs.Empty);

    private void OnLanguageClick(object sender, RoutedEventArgs e) => LanguageClicked?.Invoke(this, EventArgs.Empty);
}

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;
using Win10Sim.Core.FileSystem;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Shell;

/// <summary>
/// Рабочий стол Windows 10 — хост оболочки: обои, иконки, окна приложений,
/// панель задач, меню «Пуск», центр уведомлений и системный оверлей
/// (перезагрузка, сон).
///
/// Все «окна» программ — контролы на Canvas, поэтому панель задач и «Пуск»
/// всегда остаются сверху без трюков с topmost.
/// </summary>
public partial class DesktopView : SetupScreenBase
{
    private const double TaskbarHeight = 48;

    private readonly ObservableCollection<Toast> _toasts = new();
    private readonly DispatcherTimer _clock;
    private readonly Dictionary<string, bool> _quickActions = new()
    {
        ["wifi"] = true,
        ["bluetooth"] = false,
        ["airplane"] = false,
        ["nightlight"] = false
    };

    private bool _startMenuOpen;
    private bool _actionCenterOpen;
    private DateTime _lastIconClickUtc;
    private string? _lastIconTag;

    public DesktopView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        Toasts.ItemsSource = _toasts;

        Services.Windows.Attach(WindowCanvas);
        Services.Windows.Changed += OnWindowsChanged;
        Services.Windows.WindowClosed += OnWindowClosed;
        Services.FileSystem.Changed += OnFileSystemChanged;

        WireTaskbar();
        WireStartMenu();
        UpdateLocalizedState();

        _clock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => TickClock();
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            _clock.Stop();
            Services.Windows.Changed -= OnWindowsChanged;
            Services.Windows.WindowClosed -= OnWindowClosed;
            Services.FileSystem.Changed -= OnFileSystemChanged;
        };

        SizeChanged += (_, e) => UpdateWorkArea(e.NewSize);
    }

    protected override OsPhase? BackTarget => null;      // с рабочего стола «назад» нет

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWorkArea(new Size(ActualWidth, ActualHeight));
        TickClock();
        _clock.Start();

        Services.Windows.UpdateWorkArea(new Size(ActualWidth, ActualHeight - TaskbarHeight));
        SyncTaskbar();

        // Первый запуск: приветственное уведомление и подсказка про вход.
        if (!Services.Settings.FirstRunTipsShown)
        {
            Services.Settings.FirstRunTipsShown = true;
            Services.Settings.Save();

            ShowToast("Windows 10 Simulator",
                "Симулятор готов к работе. Выход на реальный рабочий стол — клавиша Insert.");
        }

        Keyboard.Focus(this);
    }

    // ───────────────────────────── панель задач ─────────────────────────────

    private void WireTaskbar()
    {
        Taskbar.StartClicked += (_, _) => ToggleStartMenu();
        Taskbar.SearchClicked += (_, _) => OpenStartMenuWithSearch();
        Taskbar.NotificationsClicked += (_, _) => ToggleActionCenter();
        Taskbar.ClockClicked += (_, _) => ToggleActionCenter();
        Taskbar.ShowDesktopClicked += (_, _) => MinimizeAll();
        Taskbar.NetworkClicked += (_, _) => ToggleActionCenter();
        Taskbar.SoundClicked += (_, _) => ShowToast("Звук", "Громкость: 100 %. Устройство: Динамики (Realtek Audio).");
        Taskbar.LanguageClicked += (_, _) => ShowToast(
            "Раскладка клавиатуры",
            (Services.Session.PrimaryKeyboard ?? "ru-RU") + " → " + (Services.Session.SecondaryKeyboard ?? "en-US"));
        Taskbar.WindowClicked += (_, window) => OnTaskbarWindowClicked(window);
    }

    private void TickClock()
    {
        Taskbar.UpdateClock(DateTime.Now, Services.Settings.ClockFormat);
        ClockLanguageBadge();
    }

    private void ClockLanguageBadge()
    {
        string code = Services.Session.PrimaryKeyboard ?? "ru-RU";
        string badge = code.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "РУС"
                     : code.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "ENG"
                     : code.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? "DEU"
                     : code[..2].ToUpperInvariant();

        Taskbar.SetLanguageBadge(badge);
    }

    private void SyncTaskbar()
    {
        Taskbar.SyncWindows(Services.Windows.Windows, Services.Windows.Active);
        Taskbar.SetStartMenuOpen(_startMenuOpen);
    }

    private void OnWindowsChanged(object? sender, EventArgs e) => SyncTaskbar();

    private void OnWindowClosed(object? sender, SimWindow window)
    {
        // Приложение могло держать файл открытым — сохраняем диск на всякий случай.
        Services.FileSystem.Save();
        SyncTaskbar();
    }

    private void OnTaskbarWindowClicked(SimWindow window)
    {
        if (ReferenceEquals(Services.Windows.Active, window) && !window.IsMinimized)
            Services.Windows.Minimize(window);
        else
            Services.Windows.Activate(window);

        CloseOverlays();
    }

    // ───────────────────────────── меню «Пуск» ─────────────────────────────

    private void WireStartMenu()
    {
        StartMenu.AppRequested += (_, appId) => LaunchApp(appId);
        StartMenu.SettingsRequested += (_, _) => LaunchApp(AppCatalog.Settings);
        StartMenu.PowerActionRequested += (_, action) => HandlePowerAction(action);
    }

    private void UpdateLocalizedState()
    {
        StartMenu.SetUser(Services.DisplayName);
        ThisPcLabel.Text = UiStrings.Get("L.Shell.ThisPc");
        Taskbar.SetSearchHint(UiStrings.Get("L.Shell.Search"));
        NotificationLine.Text = UiStrings.Get("L.Shell.SimNote");
    }

    private void ToggleStartMenu()
    {
        if (_startMenuOpen)
        {
            CloseStartMenu();
            return;
        }

        _actionCenterOpen = false;
        ActionCenter.Visibility = Visibility.Collapsed;

        _startMenuOpen = true;
        StartMenu.Visibility = Visibility.Visible;
        StartMenu.Opacity = 0;
        StartMenu.RenderTransform = new TranslateTransform(0, 24);

        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
        var slide = new System.Windows.Media.Animation.DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        StartMenu.BeginAnimation(OpacityProperty, fade);
        ((TranslateTransform)StartMenu.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slide);

        Taskbar.SetStartMenuOpen(true);
    }

    private void OpenStartMenuWithSearch()
    {
        if (!_startMenuOpen)
            ToggleStartMenu();

        StartMenu.FocusSearch();
    }

    private void CloseStartMenu()
    {
        if (!_startMenuOpen)
            return;

        _startMenuOpen = false;
        StartMenu.Visibility = Visibility.Collapsed;
        StartMenu.ClearSearch();
        Taskbar.SetStartMenuOpen(false);
    }

    private void ToggleActionCenter()
    {
        CloseStartMenu();

        _actionCenterOpen = !_actionCenterOpen;
        ActionCenter.Visibility = _actionCenterOpen ? Visibility.Visible : Visibility.Collapsed;

        if (_actionCenterOpen)
            RefreshQuickActions();
    }

    private void CloseOverlays()
    {
        CloseStartMenu();
        _actionCenterOpen = false;
        ActionCenter.Visibility = Visibility.Collapsed;
    }

    private void RefreshQuickActions()
    {
        foreach ((string key, bool on) in _quickActions)
        {
            Button? button = key switch
            {
                "wifi" => QuickWifi,
                "bluetooth" => QuickBluetooth,
                "airplane" => QuickAirplane,
                "nightlight" => QuickNightLight,
                _ => null
            };

            if (button is not null)
                button.Tag = on ? "on" : "off";
        }
    }

    private void OnQuickActionClick(object sender, RoutedEventArgs e)
    {
        // Состояние хранится по ключу, а ключ берём из имени кнопки:
        // Tag занят состоянием для триггера стиля.
        string key = sender switch
        {
            Button { Name: "QuickWifi" } => "wifi",
            Button { Name: "QuickBluetooth" } => "bluetooth",
            Button { Name: "QuickAirplane" } => "airplane",
            Button { Name: "QuickNightLight" } => "nightlight",
            _ => string.Empty
        };

        if (key.Length == 0)
            return;

        _quickActions[key] = !_quickActions[key];
        RefreshQuickActions();

        if (key == "airplane" && _quickActions[key])
            _quickActions["wifi"] = false;

        RefreshQuickActions();
    }

    private void OnAllSettingsClick(object sender, RoutedEventArgs e) => LaunchApp(AppCatalog.Settings);

    private void LaunchApp(string appId, string? arguments = null)
    {
        try
        {
            AppCatalog.Launch(Services, appId, arguments);
            CloseOverlays();
        }
        catch (Exception ex)
        {
            ShowToast("Ошибка", ex.Message);
        }
    }

    // ───────────────────────────── рабочий стол ─────────────────────────────

    private void OnDesktopIconClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string tag })
            return;

        // Двойной клик определяется по времени и цели: у WPF нет ClickCount у Border.
        DateTime now = DateTime.UtcNow;
        bool isDoubleClick = tag == _lastIconTag && (now - _lastIconClickUtc) < TimeSpan.FromMilliseconds(600);

        _lastIconTag = tag;
        _lastIconClickUtc = now;

        if (!isDoubleClick)
            return;

        _lastIconTag = null;

        switch (tag)
        {
            case "thispc":
                LaunchApp(AppCatalog.Explorer, "C:\\");
                break;
            case "recycle":
                LaunchApp(AppCatalog.Explorer, "$Recycle.Bin");
                break;
        }
    }

    private void OnDesktopLeftClick(object sender, MouseButtonEventArgs e)
    {
        CloseOverlays();
        e.Handled = false;
    }

    private void OnDesktopRightClick(object sender, MouseButtonEventArgs e)
    {
        // Меню рабочего стола открывается только по клику на пустое место,
        // а не по клику внутри окна приложения.
        if (!ReferenceEquals(e.OriginalSource, WindowCanvas))
            return;

        // Контекстное меню рабочего стола — как в Windows: создать, обновить, персонализация.
        var menu = new ContextMenu
        {
            Style = (Style)FindResource("ContextMenu.Sim"),
            PlacementTarget = WindowCanvas,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
        };

        var create = new MenuItem
        {
            Header = UiStrings.Get("L.Shell.Create"),
            Icon = "\uE710",
            Style = (Style)FindResource("MenuItem.Sim")
        };

        create.Items.Add(CreateMenuItem("\uE8B7", UiStrings.Get("L.Shell.Folder"), () => CreateOnDesktop(isFolder: true)));
        create.Items.Add(CreateMenuItem("\uE8A5", UiStrings.Get("L.Shell.TextFile"), () => CreateOnDesktop(isFolder: false)));

        menu.Items.Add(create);
        menu.Items.Add(CreateMenuItem("\uE72C", UiStrings.Get("L.Shell.Refresh"), RefreshDesktop));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("\uE771", UiStrings.Get("L.Shell.Personalize"), () => LaunchApp(AppCatalog.Settings)));
        menu.Items.Add(CreateMenuItem("\uE7F4", UiStrings.Get("L.Shell.DisplaySettings"), () => LaunchApp(AppCatalog.Settings)));

        menu.IsOpen = true;
    }

    private MenuItem CreateMenuItem(string glyph, string header, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = glyph,
            Style = (Style)FindResource("MenuItem.Sim")
        };

        item.Click += (_, _) => action();
        return item;
    }

    private void CreateOnDesktop(bool isFolder)
    {
        string desktop = $@"C:\Users\{Services.UserName}\Desktop";

        try
        {
            if (isFolder)
            {
                Services.FileSystem.CreateDirectory(VfsPath.Combine(desktop, NextFreeName(desktop, "Новая папка")));
            }
            else
            {
                string name = NextFreeName(desktop, "Новый текстовый документ", ".txt");
                Services.FileSystem.CreateFile(VfsPath.Combine(desktop, name), string.Empty);
            }

            LaunchApp(AppCatalog.Explorer, desktop);
        }
        catch (VfsException ex)
        {
            ShowToast("Проводник", ex.Message);
        }
    }

    private string NextFreeName(string directory, string baseName, string extension = "")
    {
        string candidate = baseName + extension;
        int index = 2;

        while (Services.FileSystem.Exists(VfsPath.Combine(directory, candidate)))
        {
            candidate = $"{baseName} ({index}){extension}";
            index++;
        }

        return candidate;
    }

    private void RefreshDesktop() => ShowToast("Рабочий стол", "Рабочий стол обновлён.");

    private void MinimizeAll()
    {
        foreach (SimWindow window in Services.Windows.Windows.Where(w => !w.IsMinimized).ToList())
            Services.Windows.Minimize(window);

        CloseOverlays();
    }

    // ───────────────────────────── питание ─────────────────────────────

    private async void HandlePowerAction(PowerAction action)
    {
        CloseOverlays();

        switch (action)
        {
            case PowerAction.Shutdown:
                // Настоящее выключение компьютера симулятору недоступно — честно говорим об этом.
                ShowToast(UiStrings.Get("L.Shell.Shutdown"), UiStrings.Get("L.Shell.SimNote"));
                break;

            case PowerAction.Restart:
                await ShowSystemOverlayAsync("Перезагрузка…", TimeSpan.FromSeconds(2.5));
                Advance(OsPhase.LockScreen);
                break;

            case PowerAction.Sleep:
                await ShowSleepAsync();
                Advance(OsPhase.LockScreen);
                break;

            case PowerAction.SignOut:
            case PowerAction.Lock:
                Advance(OsPhase.LockScreen);
                break;
        }
    }

    private async Task ShowSystemOverlayAsync(string text, TimeSpan duration)
    {
        OverlayText.Text = text;
        SystemOverlay.Visibility = Visibility.Visible;
        SystemOverlay.Opacity = 0;

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
        SystemOverlay.BeginAnimation(OpacityProperty, fadeIn);

        await Task.Delay(duration);
    }

    private async Task ShowSleepAsync()
    {
        // Сон = чёрный экран; любой клик «будит» компьютер и ведёт на экран блокировки.
        SystemOverlay.Visibility = Visibility.Visible;
        SystemOverlay.Background = Brushes.Black;
        OverlayText.Text = string.Empty;

        var completion = new TaskCompletionSource();
        MouseButtonEventHandler click = (_, _) => completion.TrySetResult();

        SystemOverlay.MouseLeftButtonUp += click;
        await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(12)));
        SystemOverlay.MouseLeftButtonUp -= click;

        SystemOverlay.Background = Brushes.Black;
    }

    // ───────────────────────────── уведомления ─────────────────────────────

    private void ShowToast(string title, string message)
    {
        var toast = new Toast(title, message);
        _toasts.Add(toast);

        // Уведомление живёт 6 секунд, затем плавно исчезает.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _toasts.Remove(toast);
        };
        timer.Start();
    }

    private void OnFileSystemChanged(object? sender, EventArgs e) => Services.FileSystem.Save();

    private void UpdateWorkArea(Size size)
    {
        Services.Windows.UpdateWorkArea(new Size(size.Width, Math.Max(200, size.Height - TaskbarHeight)));
    }

    /// <summary>Клавиатура: Esc закрывает «Пуск» и центр уведомлений, Win открывает «Пуск».</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            CloseOverlays();
            return;
        }

        if (e.Key == Key.LWin || e.Key == Key.RWin)
        {
            ToggleStartMenu();
            e.Handled = true;
            return;
        }

        if (_startMenuOpen && e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            LaunchApp(AppCatalog.Explorer, "C:\\");
            CloseOverlays();
            e.Handled = true;
        }
    }

    /// <summary>Элемент списка всплывающих уведомлений.</summary>
    public sealed record Toast(string Title, string Message);
}

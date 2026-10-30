using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Win10Sim.Core.FileSystem;

namespace Win10Sim.App.Shell;

/// <summary>
/// Внутреннее окно рабочего стола. Это НЕ настоящее окно Windows: обычный
/// контрол на Canvas. Поэтому у него наши заголовок, кнопки и z-order, а
/// «Пуск» и панель задач всегда остаются сверху.
///
/// Умеет: перетаскивание за заголовок, изменение размера за правый нижний
/// угол, сворачивание, разворачивание на весь рабочий стол, закрытие,
/// активацию (z-order + подсветка заголовка).
/// </summary>
public class SimWindow : ContentControl
{
    public const double TitleBarHeight = 32;
    public const double MinWindowWidth = 380;
    public const double MinWindowHeight = 240;

    private readonly Grid _root;
    private readonly Border _frame;
    private readonly TextBlock _title;
    private readonly TextBlock _icon;
    private readonly Grid _titleBar;
    private readonly Rectangle _resizeGrip;

    private Point _dragStart;
    private Point _dragOrigin;
    private bool _dragging;
    private bool _resizing;
    private Rect _restoreBounds;
    private bool _isMaximized;

    public SimWindow(string title, string iconGlyph, object content, string appId, bool canResize = true)
    {
        AppId = appId;
        Title = title;
        CanResize = canResize;

        Width = 900;
        Height = 560;
        MinWidth = MinWindowWidth;
        MinHeight = MinWindowHeight;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        SnapsToDevicePixels = true;

        _frame = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            BorderThickness = new Thickness(1)
        };

        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleBarHeight) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ---------- Заголовок ----------
        _titleBar = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B))
        };
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _icon = new TextBlock
        {
            Text = iconGlyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 0)
        };
        Grid.SetColumn(_icon, 0);
        _titleBar.Children.Add(_icon);

        _title = new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(_title, 1);
        _titleBar.Children.Add(_title);

        // ---------- Кнопки окна ----------
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var minimize = CreateCaptionButton("\uE921", "Свернуть");
        minimize.Click += (_, _) => RequestMinimize();

        var maximize = CreateCaptionButton("\uE922", "Развернуть");
        maximize.Click += (_, _) => ToggleMaximize();

        var close = CreateCaptionButton("\uE8BB", "Закрыть");
        close.HoverBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
        close.Click += (_, _) => RequestClose();

        buttons.Children.Add(minimize);
        buttons.Children.Add(maximize);
        buttons.Children.Add(close);

        Grid.SetColumn(buttons, 3);
        _titleBar.Children.Add(buttons);

        // ---------- Содержимое ----------
        var contentHost = new ContentControl
        {
            Content = content,
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F))
        };

        Grid.SetRow(contentHost, 1);
        _root.Children.Add(contentHost);

        Grid.SetRow(_titleBar, 0);
        _root.Children.Add(_titleBar);

        // ---------- Уголок для изменения размера ----------
        _resizeGrip = new Rectangle
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = Brushes.Transparent,
            Cursor = Cursors.SizeNWSE,
            Visibility = canResize ? Visibility.Visible : Visibility.Collapsed,
            ToolTip = "Изменить размер"
        };
        Grid.SetRowSpan(_resizeGrip, 2);
        _root.Children.Add(_resizeGrip);

        _frame.Child = _root;
        Content = _frame;

        // ---------- Перетаскивание и размер ----------
        _titleBar.MouseLeftButtonDown += OnTitleBarMouseDown;
        _titleBar.MouseMove += OnDragMove;
        _titleBar.MouseLeftButtonUp += OnDragEnd;
        _resizeGrip.MouseLeftButtonDown += OnResizeStart;
        _resizeGrip.MouseMove += OnResizeMove;
        _resizeGrip.MouseLeftButtonUp += OnResizeEnd;

        // Двойной клик по заголовку — как в Windows: развернуть/восстановить.
        _titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                e.Handled = true;
            }
        };

        PreviewMouseLeftButtonDown += (_, _) => RequestActivate();
    }

    public string AppId { get; }

    public string Title { get; }

    public bool CanResize { get; }

    /// <summary>Приложения меняют заголовок (например, Блокнот — имя открытого файла).</summary>
    public void SetTitle(string title)
    {
        _title.Text = title;
    }

    /// <summary>Программный «крестик» — для пункта меню «Выход».</summary>
    public void RaiseCloseRequest() => RequestClose();

    /// <summary>
    /// Окно, в котором лежит элемент. Нужно приложениям, созданным до окна
    /// (например, Блокноту — чтобы менять заголовок). Если содержимое ещё не
    /// вставлено в визуальное дерево, вернётся null: тогда используйте
    /// <see cref="ISimWindowAware"/>.
    /// </summary>
    public static SimWindow? FromVisual(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is SimWindow window)
                return window;

            element = element is Visual or Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return null;
    }

    public bool IsMinimized { get; private set; }

    public bool IsMaximized => _isMaximized;

    public bool IsActive { get; private set; }

    /// <summary>Свёрнуто в панель задач — окно скрыто, но живо.</summary>
    public event EventHandler? MinimizeRequested;

    public event EventHandler? CloseRequested;

    public event EventHandler? ActivateRequested;

    /// <summary>Размер рабочей области (передаётся менеджером окон при изменении размера экрана).</summary>
    public Size WorkArea { get; set; } = new(1600, 900);

    private CaptionButton CreateCaptionButton(string glyph, string tooltip) => new()
    {
        Glyph = glyph,
        ToolTipText = tooltip,
        HoverBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x3F))
    };

    public void SetActive(bool active)
    {
        IsActive = active;
        _titleBar.Background = active
            ? new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B))
            : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        _title.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
        _frame.BorderBrush = active
            ? new SolidColorBrush(Color.FromRgb(0x0078D7))
            : new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
    }

    public void Restore()
    {
        IsMinimized = false;
        Visibility = Visibility.Visible;
    }

    public void Minimize()
    {
        IsMinimized = true;
        Visibility = Visibility.Collapsed;
    }

    public void ToggleMaximize()
    {
        if (!CanResize)
            return;

        if (_isMaximized)
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
            _isMaximized = false;
            return;
        }

        _restoreBounds = new Rect(Left, Top, Width, Height);
        Left = 0;
        Top = 0;
        Width = WorkArea.Width;
        Height = WorkArea.Height;
        _isMaximized = true;
    }

    /// <summary>Держит окно в пределах рабочей области (после изменения размера экрана).</summary>
    public void ClampToWorkArea(Size workArea)
    {
        WorkArea = workArea;

        if (_isMaximized)
        {
            Width = workArea.Width;
            Height = workArea.Height;
            Left = 0;
            Top = 0;
            return;
        }

        Width = Math.Min(Width, Math.Max(MinWindowWidth, workArea.Width));
        Height = Math.Min(Height, Math.Max(MinWindowHeight, workArea.Height));
        Left = Math.Clamp(Left, 0, Math.Max(0, workArea.Width - Width));
        Top = Math.Clamp(Top, 0, Math.Max(0, workArea.Height - Height));
    }

    /// <summary>Каскадное размещение новых окон, как в Windows.</summary>
    public void PlaceAt(int index)
    {
        const double offset = 28;
        double maxLeft = Math.Max(0, WorkArea.Width - 760);
        double maxTop = Math.Max(0, WorkArea.Height - 480);

        Left = Math.Min(120 + index * offset, maxLeft);
        Top = Math.Min(90 + index * offset, maxTop);
        Width = Math.Min(900, Math.Max(MinWindowWidth, WorkArea.Width - 200));
        Height = Math.Min(580, Math.Max(MinWindowHeight, WorkArea.Height - 140));
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isMaximized)
            return;

        _dragging = true;
        _dragStart = e.GetPosition(Application.Current.MainWindow);
        _dragOrigin = new Point(Left, Top);
        _titleBar.CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        Point current = e.GetPosition(Application.Current.MainWindow);
        Left = Math.Clamp(_dragOrigin.X + (current.X - _dragStart.X), -Width + 120, WorkArea.Width - 120);
        Top = Math.Clamp(_dragOrigin.Y + (current.Y - _dragStart.Y), 0, WorkArea.Height - TitleBarHeight);
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        _titleBar.ReleaseMouseCapture();
    }

    private void OnResizeStart(object sender, MouseButtonEventArgs e)
    {
        _resizing = true;
        _dragStart = e.GetPosition(Application.Current.MainWindow);
        _dragOrigin = new Point(Width, Height);
        _resizeGrip.CaptureMouse();
        e.Handled = true;
    }

    private void OnResizeMove(object sender, MouseEventArgs e)
    {
        if (!_resizing)
            return;

        Point current = e.GetPosition(Application.Current.MainWindow);
        Width = Math.Clamp(_dragOrigin.X + (current.X - _dragStart.X), MinWindowWidth, WorkArea.Width - Left);
        Height = Math.Clamp(_dragOrigin.Y + (current.Y - _dragStart.Y), MinHeight, WorkArea.Height - Top);
    }

    private void OnResizeEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing)
            return;

        _resizing = false;
        _resizeGrip.ReleaseMouseCapture();
    }

    private void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void RequestMinimize() => MinimizeRequested?.Invoke(this, EventArgs.Empty);

    private void RequestActivate() => ActivateRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Кнопка заголовка с подсветкой при наведении (стиль Win10).</summary>
    private sealed class CaptionButton : Button
    {
        public string Glyph { get; init; } = string.Empty;

        public string ToolTipText { get; init; } = string.Empty;

        public Brush HoverBrush { get; set; } = Brushes.Gray;

        public CaptionButton()
        {
            Width = 46;
            Height = TitleBarHeight;
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Cursor = Cursors.Arrow;
            Focusable = false;
            Template = CreateTemplate(this);
        }

        private static ControlTemplate CreateTemplate(CaptionButton owner)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.Name = "Bg";

            var glyph = new FrameworkElementFactory(typeof(TextBlock));
            glyph.SetValue(TextBlock.TextProperty, owner.Glyph);
            glyph.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
            glyph.SetValue(TextBlock.FontSizeProperty, 10.0);
            glyph.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            glyph.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            glyph.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(glyph);

            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            template.Triggers.Add(new Trigger
            {
                Property = IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Border.BackgroundProperty, owner.HoverBrush, "Bg") }
            });

            return template;
        }
    }
}

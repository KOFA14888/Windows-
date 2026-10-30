using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;

namespace Win10Sim.App.Screens.Shell;

public enum PowerAction
{
    Shutdown,
    Restart,
    Sleep,
    SignOut,
    Lock
}

/// <summary>
/// Меню «Пуск»: поиск по приложениям, список программ, плитки и меню питания.
/// Само ничего не открывает — сообщает наружу выбранное приложение.
/// </summary>
public partial class StartMenuView : UserControl
{
    private readonly IReadOnlyList<AppDescriptor> _apps = AppCatalog.All;

    public StartMenuView()
    {
        InitializeComponent();

        foreach (AppDescriptor app in _apps)
            AppList.Children.Add(CreateAppRow(app, showDescription: false));

        foreach (AppDescriptor app in _apps.Where(a => a.PinnedTile))
            Tiles.Children.Add(CreateTile(app));

        SearchInput.TextChanged += (_, _) =>
            SearchPlaceholder.Visibility = SearchInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public event EventHandler<string>? AppRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? DismissRequested;

    public event EventHandler<PowerAction>? PowerActionRequested;

    public void SetUser(string displayName)
    {
        UserNameText.Text = displayName;
        UserInitial.Text = displayName.Length > 0 ? displayName[..1].ToUpperInvariant() : "?";
    }

    /// <summary>Открыть меню в режиме поиска (клик по полю поиска на панели задач).</summary>
    public void FocusSearch()
    {
        SearchInput.Focus();
        Keyboard.Focus(SearchInput);
    }

    public void ClearSearch() => SearchInput.Text = string.Empty;

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchInput.Text.Trim();

        if (query.Length == 0)
        {
            SearchResultsPanel.Visibility = Visibility.Collapsed;
            TilesPanel.Visibility = Visibility.Visible;
            return;
        }

        SearchResultsPanel.Visibility = Visibility.Visible;
        TilesPanel.Visibility = Visibility.Collapsed;

        SearchResults.Children.Clear();

        List<AppDescriptor> matches = _apps
            .Where(a => a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            SearchResults.Children.Add(new TextBlock
            {
                Text = UiStrings.Get("L.Shell.NoResults"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                Margin = new Thickness(8, 12, 0, 0),
                FontSize = 13
            });

            return;
        }

        SearchResults.Children.Add(new TextBlock
        {
            Text = UiStrings.Get("L.Shell.SearchResults"),
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
            FontSize = 12,
            Margin = new Thickness(8, 4, 0, 8)
        });

        foreach (AppDescriptor app in matches)
            SearchResults.Children.Add(CreateAppRow(app, showDescription: true));
    }

    private Button CreateAppRow(AppDescriptor app, bool showDescription)
    {
        var row = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 8, 10, 8),
            Cursor = Cursors.Hand,
            Template = CreateHoverTemplate()
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock
        {
            Text = app.Glyph,
            FontFamily = (FontFamily)FindResource("Font.Icons"),
            FontSize = 16,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontSize = 13,
            Foreground = Brushes.White
        });

        if (showDescription)
        {
            text.Children.Add(new TextBlock
            {
                Text = app.Id == AppCatalog.Settings ? "Приложение" : "Приложение Windows",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0))
            });
        }

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        row.Content = grid;
        row.Click += (_, _) => Launch(app.Id);
        return row;
    }

    private Button CreateTile(AppDescriptor app)
    {
        var tile = new Button
        {
            Width = 112,
            Height = 112,
            Margin = new Thickness(4),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(app.TileColor)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        var stack = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        stack.Children.Add(new TextBlock
        {
            Text = app.Glyph,
            FontFamily = (FontFamily)FindResource("Font.Icons"),
            FontSize = 26,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        stack.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontSize = 12,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        tile.Content = stack;
        tile.Click += (_, _) => Launch(app.Id);
        return tile;
    }

    private static ControlTemplate CreateHoverTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Root";
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.PaddingProperty, new Thickness(0));
        border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        template.Triggers.Add(new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true,
            Setters = { new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)), "Root") }
        });

        return template;
    }

    private void Launch(string appId) => AppRequested?.Invoke(this, appId);

    private void OnUserClick(object sender, RoutedEventArgs e) => PowerActionRequested?.Invoke(this, PowerAction.SignOut);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Меню питания: небольшое контекстное меню над кнопкой.</summary>
    private void OnPowerClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            Style = (Style)FindResource("ContextMenu.Sim"),
            PlacementTarget = PowerButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Top
        };

        AddItem(menu, "\uE7E8", UiStrings.Get("L.Shell.Shutdown"), PowerAction.Shutdown);
        AddItem(menu, "\uE777", UiStrings.Get("L.Shell.Restart"), PowerAction.Restart);
        AddItem(menu, "\uE708", UiStrings.Get("L.Shell.Sleep"), PowerAction.Sleep);
        AddItem(menu, "\uE77B", UiStrings.Get("L.Shell.SignOut"), PowerAction.SignOut);
        AddItem(menu, "\uE72E", UiStrings.Get("L.Shell.Lock"), PowerAction.Lock);

        menu.IsOpen = true;
    }

    private void AddItem(ContextMenu menu, string glyph, string header, PowerAction action)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = glyph,
            Style = (Style)FindResource("MenuItem.Sim")
        };

        item.Click += (_, _) => PowerActionRequested?.Invoke(this, action);
        menu.Items.Add(item);
    }
}

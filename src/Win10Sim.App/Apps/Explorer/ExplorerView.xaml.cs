using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;
using Win10Sim.Core.FileSystem;

namespace Win10Sim.App.Apps.Explorer;

/// <summary>
/// Проводник Windows 10 поверх виртуального диска C:.
///
/// Работает с тем же <see cref="IVirtualFileSystem"/>, что и командная строка,
/// поэтому файл, созданный в cmd, сразу появляется здесь (и наоборот).
/// Подписка на <see cref="IVirtualFileSystem.Changed"/> держит список актуальным.
/// </summary>
public partial class ExplorerView : UserControl, ISimApp
{
    private readonly AppServices _services;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly ObservableCollection<ExplorerItem> _items = new();
    private readonly List<string> _history = new();
    private readonly Stack<string> _forward = new();
    private readonly List<(Button Button, string Path)> _navigation = new();

    private string _currentPath = VfsPath.Root;
    private bool _navigatingHistory;
    private NameDialogMode _dialogMode;
    private ExplorerItem? _dialogTarget;

    public ExplorerView(AppServices services, IVirtualFileSystem fileSystem)
    {
        _services = services;
        _fileSystem = fileSystem;

        InitializeComponent();

        FileList.ItemsSource = _items;
        _fileSystem.Changed += OnFileSystemChanged;

        BuildNavigation();
        Navigate(VfsPath.Root, addHistory: false);
    }

    private enum NameDialogMode
    {
        NewFolder,
        NewFile,
        Rename
    }

    public void HandleArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return;

        // Проводник понимает и папку, и файл: файл открывает родительскую папку
        // и выделяет его — как двойной клик в настоящей Windows.
        string path = VfsPath.Normalize(arguments, VfsPath.Root);
        VfsNode? node = _fileSystem.Get(path);

        if (node is null)
            return;

        if (node.IsDirectory)
        {
            Navigate(path);
            return;
        }

        string parent = VfsPath.GetParent(path);
        Navigate(parent);
        SelectByName(node.Name);
    }

    /// <summary>Список быстрых ссылок слева: «Этот компьютер», папки пользователя, диск.</summary>
    private void BuildNavigation()
    {
        NavigationPanel.Children.Clear();
        _navigation.Clear();

        string user = _services.UserName;

        AddNavigationItem(UiStrings.Get("L.Explorer.ThisPc"), "\uE977", VfsPath.Root);
        NavigationPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            Margin = new Thickness(6, 6, 6, 6)
        });

        AddNavigationItem(UiStrings.Get("L.Explorer.Desktop"), "\uE8FC", $@"C:\Users\{user}\Desktop");
        AddNavigationItem(UiStrings.Get("L.Explorer.Documents"), "\uE8A5", $@"C:\Users\{user}\Documents");
        AddNavigationItem(UiStrings.Get("L.Explorer.Downloads"), "\uE896", $@"C:\Users\{user}\Downloads");
        AddNavigationItem(UiStrings.Get("L.Explorer.Pictures"), "\uEB9F", $@"C:\Users\{user}\Pictures");
        AddNavigationItem(UiStrings.Get("L.Explorer.Music"), "\uE8D6", $@"C:\Users\{user}\Music");
        AddNavigationItem(UiStrings.Get("L.Explorer.Videos"), "\uE714", $@"C:\Users\{user}\Videos");
        AddNavigationItem(UiStrings.Get("L.Explorer.RecycleBin"), "\uE74D", @"C:\$Recycle.Bin");

        NavigationPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            Margin = new Thickness(6, 6, 6, 6)
        });

        AddNavigationItem(UiStrings.Get("L.Explorer.Disk"), "\uEDA2", VfsPath.Root);
    }

    private void AddNavigationItem(string title, string glyph, string path)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = (FontFamily)FindResource("Font.Icons"),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xB1, 0x5C)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });
        content.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Style = (Style)FindResource("Button.Sidebar"),
            Content = content
        };

        button.Click += (_, _) =>
        {
            if (_fileSystem.Exists(path))
                Navigate(path);
        };

        _navigation.Add((button, path));
        NavigationPanel.Children.Add(button);
    }

    private void OnFileSystemChanged(object? sender, EventArgs e)
    {
        // Файл могли удалить в cmd — если текущая папка исчезла, поднимаемся к корню.
        if (!_fileSystem.Exists(_currentPath))
            _currentPath = VfsPath.Root;

        Refresh();
    }

    private void Navigate(string path, bool addHistory = true)
    {
        string normalized = VfsPath.Normalize(path, _currentPath);

        if (!_fileSystem.Exists(normalized))
        {
            StatusText.Text = UiStrings.Get("L.Explorer.NotFound");
            return;
        }

        if (addHistory && !_navigatingHistory && normalized != _currentPath)
        {
            _history.Add(_currentPath);
            _forward.Clear();
        }

        _currentPath = normalized;
        PathBox.Text = normalized;
        Refresh();
    }

    private void Refresh()
    {
        string? selectedName = (FileList.SelectedItem as ExplorerItem)?.Name;

        _items.Clear();

        foreach (VfsNode node in _fileSystem.Enumerate(_currentPath))
        {
            _items.Add(new ExplorerItem
            {
                Name = node.Name,
                Path = _fileSystem.GetPath(node),
                Glyph = node.Glyph,
                IsDirectory = node.IsDirectory,
                IconBrush = node.IsDirectory
                    ? new SolidColorBrush(Color.FromRgb(0xF2, 0xB1, 0x5C))
                    : new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                ModifiedText = node.ModifiedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture),
                SizeText = node.IsDirectory ? string.Empty : UiStrings.FormatSize(node.SizeBytes)
            });
        }

        EmptyLabel.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        StatusText.Text = UiStrings.Format("L.Explorer.Items", _items.Count);

        long used = UsedSpace();
        long total = _fileSystem.FreeSpaceBytes + used;
        DiskText.Text = UiStrings.Format("L.Explorer.Free", UiStrings.FormatSize(_fileSystem.FreeSpaceBytes), UiStrings.FormatSize(total));

        BackButton.IsEnabled = _history.Count > 0;
        ForwardButton.IsEnabled = _forward.Count > 0;

        // Подсветка активного раздела слева (стиль реагирует на Tag="selected").
        foreach ((Button button, string path) in _navigation)
            button.Tag = string.Equals(path, _currentPath, StringComparison.OrdinalIgnoreCase) ? "selected" : null;

        if (selectedName is not null)
            SelectByName(selectedName);
        else
            FileList.SelectedItem = null;
    }

    private long UsedSpace()
    {
        long total = 0;

        void Walk(VfsNode node)
        {
            if (node.IsDirectory)
            {
                foreach (VfsNode child in node.Children)
                    Walk(child);
            }
            else
            {
                total += node.SizeBytes;
            }
        }

        Walk(_fileSystem.Root);
        return total;
    }

    private void SelectByName(string name)
    {
        ExplorerItem? item = _items.FirstOrDefault(
            i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return;

        FileList.SelectedItem = item;
        FileList.ScrollIntoView(item);
    }

    // ==================== Навигация ====================

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_history.Count == 0)
            return;

        _forward.Push(_currentPath);
        _navigatingHistory = true;
        Navigate(_history[^1], addHistory: false);
        _history.RemoveAt(_history.Count - 1);
        _navigatingHistory = false;
    }

    private void OnForwardClick(object sender, RoutedEventArgs e)
    {
        if (_forward.Count == 0)
            return;

        _history.Add(_currentPath);
        _navigatingHistory = true;
        Navigate(_forward.Pop(), addHistory: false);
        _navigatingHistory = false;
    }

    private void OnUpClick(object sender, RoutedEventArgs e)
    {
        string parent = VfsPath.GetParent(_currentPath);

        if (!string.Equals(parent, _currentPath, StringComparison.OrdinalIgnoreCase))
            Navigate(parent);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();

    private void OnPathKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        Navigate(PathBox.Text);
        e.Handled = true;
    }

    // ==================== Файлы ====================

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            ShowRenameDialog();
            e.Handled = true;
        }
    }

    private void OnOpenClick(object sender, RoutedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (FileList.SelectedItem is not ExplorerItem item)
            return;

        if (item.IsDirectory)
        {
            Navigate(item.Path);
            return;
        }

        // Текстовый файл открываем в Блокноте, остальные — просто выделяем.
        if (item.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            item.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
            item.Name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
        {
            AppCatalog.Launch(_services, AppCatalog.Notepad, item.Path);
        }
    }

    private void OnNewFolderClick(object sender, RoutedEventArgs e)
    {
        _dialogMode = NameDialogMode.NewFolder;
        _dialogTarget = null;
        ShowNameDialog(UiStrings.Get("L.Explorer.NameDialog.CreateFolder"));
    }

    private void OnNewFileClick(object sender, RoutedEventArgs e)
    {
        _dialogMode = NameDialogMode.NewFile;
        _dialogTarget = null;
        ShowNameDialog(UiStrings.Get("L.Explorer.NameDialog.CreateFile"));
    }

    private void OnRenameClick(object sender, RoutedEventArgs e) => ShowRenameDialog();

    private void ShowRenameDialog()
    {
        if (FileList.SelectedItem is not ExplorerItem item)
            return;

        _dialogMode = NameDialogMode.Rename;
        _dialogTarget = item;
        ShowNameDialog(item.Name);
    }

    private void ShowNameDialog(string initial)
    {
        NameDialogTitle.Text = _dialogMode == NameDialogMode.Rename
            ? UiStrings.Get("L.Explorer.NameDialog.Rename")
            : UiStrings.Get("L.Explorer.NameDialog.Title");

        NameDialogInput.Text = initial;
        NameDialogError.Visibility = Visibility.Collapsed;
        NameDialog.Visibility = Visibility.Visible;

        NameDialogInput.Focus();
        NameDialogInput.SelectAll();
    }

    private void OnNameDialogCancel(object sender, RoutedEventArgs e) => NameDialog.Visibility = Visibility.Collapsed;

    private void OnNameDialogOk(object sender, RoutedEventArgs e)
    {
        string name = NameDialogInput.Text.Trim();

        if (!VfsPath.IsValidName(name, out string error))
        {
            NameDialogError.Text = error;
            NameDialogError.Visibility = Visibility.Visible;
            return;
        }

        string target = VfsPath.Combine(_currentPath, name);

        try
        {
            switch (_dialogMode)
            {
                case NameDialogMode.NewFolder:
                    if (_fileSystem.Exists(target))
                    {
                        ShowNameError(UiStrings.Get("L.Explorer.Taken"));
                        return;
                    }

                    _fileSystem.CreateDirectory(target);
                    break;

                case NameDialogMode.NewFile:
                    if (_fileSystem.Exists(target))
                    {
                        ShowNameError(UiStrings.Get("L.Explorer.Taken"));
                        return;
                    }

                    _fileSystem.CreateFile(target);
                    break;

                case NameDialogMode.Rename:
                    if (_dialogTarget is null)
                        return;

                    if (!string.Equals(_dialogTarget.Name, name, StringComparison.Ordinal))
                    {
                        if (_fileSystem.Exists(target))
                        {
                            ShowNameError(UiStrings.Get("L.Explorer.Taken"));
                            return;
                        }

                        _fileSystem.Move(_dialogTarget.Path, target);
                    }

                    break;
            }
        }
        catch (VfsException ex)
        {
            ShowNameError(ex.Message);
            return;
        }

        NameDialog.Visibility = Visibility.Collapsed;
        _fileSystem.Save();
        Refresh();
        SelectByName(name);
    }

    private void ShowNameError(string message)
    {
        NameDialogError.Text = message;
        NameDialogError.Visibility = Visibility.Visible;
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        if (FileList.SelectedItem is not ExplorerItem item)
            return;

        // Удаление без подтверждения — как в Windows при нажатии Delete без Shift
        // элемент уходит в корзину; корзина виртуальная, поэтому просто удаляем
        // содержимое и обновляем список.
        try
        {
            _fileSystem.Delete(item.Path, recursive: true);
            _fileSystem.Save();
        }
        catch (VfsException ex)
        {
            StatusText.Text = ex.Message;
        }

        Refresh();
    }
}

/// <summary>Строка списка Проводника.</summary>
public sealed class ExplorerItem
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string Glyph { get; init; }

    public required bool IsDirectory { get; init; }

    public required Brush IconBrush { get; init; }

    public required string ModifiedText { get; init; }

    public required string SizeText { get; init; }
}

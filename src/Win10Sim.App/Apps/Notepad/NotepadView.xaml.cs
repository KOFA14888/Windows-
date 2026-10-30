using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Win10Sim.App.Apps.Explorer;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;
using Win10Sim.Core.FileSystem;

namespace Win10Sim.App.Apps.Notepad;

/// <summary>
/// Блокнот. Файлы лежат на виртуальном диске C:, поэтому диалоги открытия и
/// сохранения собственные (системные WPF-диалоги работают с реальным диском,
/// а он у нас не должен трогаться).
///
/// Автосохранение: если у документа есть путь, изменения пишутся на диск через
/// паузу в 1,5 с после последнего нажатия, как в «современном» блокноте Windows 11.
/// </summary>
public partial class NotepadView : UserControl, ISimApp, ISimWindowAware
{
    private readonly AppServices _services;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly DispatcherTimer _autoSave;

    private SimWindow? _window;
    private string? _path;
    private string _dialogFolder = VfsPath.Root;
    private bool _saveDialog;
    private bool _suppressDirty;

    public NotepadView(AppServices services)
    {
        _services = services;
        _fileSystem = services.FileSystem;

        InitializeComponent();

        _autoSave = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1500) };
        _autoSave.Tick += (_, _) => AutoSave();

        Editor.TextChanged += OnTextChanged;
        Loaded += (_, _) => Editor.Focus();

        UpdateTitle();
        UpdateStatus();
    }

    /// <summary>Путь к открытому файлу — его показывает заголовок окна.</summary>
    public string? FilePath => _path;

    public void AttachWindow(SimWindow window)
    {
        _window = window;
        UpdateTitle();
    }

    public void HandleArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return;

        string path = VfsPath.Normalize(arguments, VfsPath.Root);

        if (!_fileSystem.Exists(path))
        {
            // Аргумент может быть просто именем нового файла.
            string parent = VfsPath.GetParent(path);
            if (_fileSystem.Exists(parent))
            {
                _path = path;
                ShowPath();
                return;
            }

            return;
        }

        LoadFile(path);
    }

    private void LoadFile(string path)
    {
        try
        {
            string text = _fileSystem.ReadAllText(path);

            _suppressDirty = true;
            Editor.Text = text;
            _suppressDirty = false;

            _path = path;
            Editor.CaretIndex = 0;

            UpdateTitle();
            ShowPath();
            UpdateStatus();
        }
        catch (VfsException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateStatus();

        if (_suppressDirty)
            return;

        _autoSave.Stop();
        _autoSave.Start();
    }

    private void AutoSave()
    {
        _autoSave.Stop();

        if (_path is null)
            return;

        try
        {
            _fileSystem.WriteAllText(_path, Editor.Text);
            StatusText.Text = UiStrings.Format("L.Notepad.Saved", VfsPath.GetName(_path));
        }
        catch (VfsException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void UpdateTitle()
    {
        string name = _path is null ? UiStrings.Get("L.Notepad.Untitled") : VfsPath.GetName(_path);

        // Заголовок окна живёт в SimWindow; Блокнот подписывается на изменения
        // через родительское окно на момент загрузки.
        (_window ??= SimWindow.FromVisual(this))?.SetTitle(name + " — " + UiStrings.Get("L.Notepad.Title"));
    }

    private void ShowPath()
    {
        PathText.Text = _path ?? string.Empty;
    }

    private void UpdateStatus()
    {
        int caret = Math.Min(Editor.CaretIndex, Editor.Text.Length);
        string beforeCaret = Editor.Text[..caret];
        int line = beforeCaret.Count(c => c == '\n') + 1;
        int column = caret - beforeCaret.LastIndexOf('\n');

        StatusText.Text = UiStrings.Format("L.Notepad.StatusBar", line, column);
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e) => UpdateStatus();

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    Save();
                    e.Handled = true;
                    break;
                case Key.O:
                    ShowDialog(save: false);
                    e.Handled = true;
                    break;
                case Key.N:
                    NewDocument();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.F5)
        {
            InsertTimeDate();
            e.Handled = true;
        }
    }

    // ==================== Меню «Файл» ====================

    private void OnNewClick(object sender, RoutedEventArgs e) => NewDocument();

    private void NewDocument()
    {
        AutoSave();

        _suppressDirty = true;
        Editor.Clear();
        _suppressDirty = false;

        _path = null;
        ShowPath();
        UpdateTitle();
        UpdateStatus();
    }

    private void OnOpenClick(object sender, RoutedEventArgs e) => ShowDialog(save: false);

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    private void OnSaveAsClick(object sender, RoutedEventArgs e) => ShowDialog(save: true);

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        AutoSave();

        (_window ?? SimWindow.FromVisual(this))?.RaiseCloseRequest();
    }

    private void Save()
    {
        if (_path is null)
        {
            ShowDialog(save: true);
            return;
        }

        try
        {
            _fileSystem.WriteAllText(_path, Editor.Text);
            StatusText.Text = UiStrings.Format("L.Notepad.Saved", VfsPath.GetName(_path));
        }
        catch (VfsException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    // ==================== Меню «Правка» ====================

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (Editor.CanUndo)
            Editor.Undo();
    }

    private void OnCutClick(object sender, RoutedEventArgs e) => Editor.Cut();

    private void OnCopyClick(object sender, RoutedEventArgs e) => Editor.Copy();

    private void OnPasteClick(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
            Editor.SelectedText = Clipboard.GetText();
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e) => Editor.SelectAll();

    private void OnTimeDateClick(object sender, RoutedEventArgs e) => InsertTimeDate();

    private void InsertTimeDate()
    {
        string stamp = DateTime.Now.ToString("HH:mm dd.MM.yyyy", CultureInfo.CurrentCulture);
        int caret = Editor.CaretIndex;

        Editor.Text = Editor.Text.Insert(caret, stamp);
        Editor.CaretIndex = caret + stamp.Length;
    }

    private void OnWordWrapClick(object sender, RoutedEventArgs e)
    {
        Editor.TextWrapping = WordWrapItem.IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;
        Editor.HorizontalScrollBarVisibility = WordWrapItem.IsChecked
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }

    // ==================== Диалог открытия/сохранения ====================

    private void ShowDialog(bool save)
    {
        _saveDialog = save;
        _dialogFolder = _path is null ? DefaultFolder() : VfsPath.GetParent(_path);

        DialogTitle.Text = UiStrings.Get(save ? "L.Notepad.Dialog.SaveTitle" : "L.Notepad.Dialog.Title");
        FileNameBox.Text = _path is null
            ? UiStrings.Get("L.Notepad.Untitled") + ".txt"
            : VfsPath.GetName(_path);

        DialogError.Visibility = Visibility.Collapsed;
        FileDialog.Visibility = Visibility.Visible;

        LoadDialogFiles();

        FileNameBox.Focus();
        FileNameBox.SelectAll();
    }

    private string DefaultFolder()
    {
        string documents = $@"C:\Users\{_services.UserName}\Documents";
        return _fileSystem.Exists(documents) ? documents : VfsPath.Root;
    }

    private void LoadDialogFiles()
    {
        var files = new List<ExplorerItem>();

        if (_fileSystem.Exists(_dialogFolder))
        {
            foreach (VfsNode node in _fileSystem.Enumerate(_dialogFolder))
            {
                if (node.IsDirectory)
                    continue;

                files.Add(new ExplorerItem
                {
                    Name = node.Name,
                    Path = _fileSystem.GetPath(node),
                    Glyph = node.Glyph,
                    IsDirectory = false,
                    IconBrush = System.Windows.Media.Brushes.Gray,
                    ModifiedText = string.Empty,
                    SizeText = string.Empty
                });
            }
        }

        FileList.ItemsSource = files;
        EmptyLabel.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFileListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is ExplorerItem item)
        {
            FileNameBox.Text = item.Name;
            OnDialogOk(this, new RoutedEventArgs());
        }
    }

    private void OnDialogCancel(object sender, RoutedEventArgs e) => FileDialog.Visibility = Visibility.Collapsed;

    private void OnDialogOk(object sender, RoutedEventArgs e)
    {
        string name = FileNameBox.Text.Trim();

        if (!VfsPath.IsValidName(name, out string error))
        {
            ShowDialogError(error);
            return;
        }

        string path = VfsPath.Combine(_dialogFolder, name);

        try
        {
            if (_saveDialog)
            {
                _fileSystem.WriteAllText(path, Editor.Text);
                _path = path;
                StatusText.Text = UiStrings.Format("L.Notepad.Saved", name);
            }
            else
            {
                if (!_fileSystem.Exists(path))
                {
                    ShowDialogError(UiStrings.Get("L.Explorer.NotFound"));
                    return;
                }

                FileDialog.Visibility = Visibility.Collapsed;
                LoadFile(path);
                return;
            }
        }
        catch (VfsException ex)
        {
            ShowDialogError(ex.Message);
            return;
        }

        FileDialog.Visibility = Visibility.Collapsed;
        ShowPath();
        UpdateTitle();
    }

    private void ShowDialogError(string message)
    {
        DialogError.Text = message;
        DialogError.Visibility = Visibility.Visible;
    }
}

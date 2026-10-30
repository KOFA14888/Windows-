using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;
using Win10Sim.Core.Shell;

namespace Win10Sim.App.Apps.Cmd;

/// <summary>
/// Командная строка. Вся логика команд живёт в <see cref="CommandProcessor"/>
/// (ядро, покрытое тестами); здесь только ввод, вывод, история и Ctrl+C.
/// </summary>
public partial class CmdView : UserControl, ISimApp, ISimWindowAware
{
    private readonly CommandProcessor _processor;
    private readonly StringBuilder _output = new();

    private SimWindow? _window;
    private CancellationTokenSource? _running;
    private int _historyIndex = -1;
    private string _draft = string.Empty;

    public CmdView(AppServices services)
    {
        _processor = services.CommandProcessor;

        InitializeComponent();

        foreach (string line in _processor.Banner)
            WriteLine(line);

        WriteLine(string.Empty);
        WriteLine(UiStrings.Get("L.Cmd.Hint"));
        WriteLine(string.Empty);

        UpdatePrompt();

        Loaded += (_, _) => InputBox.Focus();
    }

    public void AttachWindow(SimWindow window) => _window = window;

    public void HandleArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return;

        // «cmd /c dir» — сразу выполнить команду, как в настоящей Windows.
        string command = arguments.Trim();

        if (command.StartsWith("/c ", StringComparison.OrdinalIgnoreCase))
            command = command[3..];

        InputBox.Text = command;
        ExecuteAsync(command);
    }

    private void UpdatePrompt()
    {
        // Приглашение после cd меняется — оно часть контекста процессора.
        PromptText.Text = _processor.Context.Prompt;
    }

    private void WriteLine(string line)
    {
        _output.Append(line).Append(Environment.NewLine);
        OutputText.Text = _output.ToString();
        OutputScroll.ScrollToEnd();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                string line = InputBox.Text;

                InputBox.Clear();

                // Проверка на пустую строку — в cmd это просто новое приглашение.
                WriteLine(_processor.Context.Prompt + line);

                if (line.Trim().Length > 0)
                {
                    _historyIndex = -1;
                    _draft = string.Empty;
                    ExecuteAsync(line);
                }

                e.Handled = true;
                break;

            case Key.Up:
                RecallHistory(-1);
                e.Handled = true;
                break;

            case Key.Down:
                RecallHistory(+1);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Tab — дополнение, Ctrl+C — прервать текущую команду (например, ping).</summary>
    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            string? completed = _processor.Complete(InputBox.Text);

            if (completed is not null)
            {
                InputBox.Text = completed;
                InputBox.CaretIndex = completed.Length;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.C && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_running is { IsCancellationRequested: false })
            {
                _running.Cancel();
                WriteLine("^C");
            }
            else
            {
                InputBox.Clear();
            }

            e.Handled = true;
        }
    }

    private void RecallHistory(int direction)
    {
        IReadOnlyList<string> history = _processor.History;

        if (history.Count == 0)
            return;

        if (_historyIndex == -1)
        {
            _draft = InputBox.Text;
            _historyIndex = history.Count;
        }

        int next = Math.Clamp(_historyIndex + direction, 0, history.Count);

        if (next == history.Count)
        {
            _historyIndex = -1;
            InputBox.Text = _draft;
        }
        else
        {
            _historyIndex = next;
            InputBox.Text = history[next];
        }

        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private async void ExecuteAsync(string line)
    {
        using var cancellation = new CancellationTokenSource();
        _running = cancellation;

        InputBox.IsEnabled = false;

        try
        {
            CommandResult result = await _processor
                .ExecuteAsync(line, cancellation.Token)
                .ConfigureAwait(true);

            if (result.ClearScreen)
            {
                _output.Clear();
                OutputText.Text = string.Empty;
            }

            foreach (string outputLine in result.Lines)
                WriteLine(outputLine);

            UpdatePrompt();

            if (result.ExitRequested)
            {
                // Как настоящий «exit»: закрываем окно, а не приложение.
                (_window ?? SimWindow.FromVisual(this))?.RaiseCloseRequest();

                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Команда не должна ронять симулятор: показываем текст ошибки.
            WriteLine(ex.Message);
        }
        finally
        {
            _running = null;
            InputBox.IsEnabled = true;
            InputBox.Focus();
        }
    }
}

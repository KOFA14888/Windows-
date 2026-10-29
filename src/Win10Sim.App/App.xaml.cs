using System.IO;
using System.Windows;
using System.Windows.Threading;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Platform;
using Win10Sim.Core.Persistence;

namespace Win10Sim.App;

/// <summary>
/// Точка входа. Инварианты, которые нельзя нарушать ни на одном этапе:
///   1. Выход — ТОЛЬКО Insert (ни Alt+F4, ни закрытие окна, ни завершение потока).
///   2. Хук Insert ставится ДО показа окна: если UI упадёт при инициализации,
///      пользователь всё равно попадёт на реальный рабочий стол.
///   3. Состояние установки сохраняется после каждого экрана, а не только при выходе.
/// </summary>
public partial class App : Application, IAppHost
{
    private InsertKeyHook? _insertHook;
    private CancellationTokenSource? _lifetime;
    private bool _exiting;

    public AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // --- 1. Аварийный выход. Раньше всего остального. ---
        _insertHook = new InsertKeyHook();
        _insertHook.InsertPressed += OnInsertPressed;
        try
        {
            _insertHook.Install();
        }
        catch (Exception ex)
        {
            // Политика безопасности/античит может запретить хук: не падаем,
            // останется резервный путь MainWindow.PreviewKeyDown.
            Log("Не удалось установить глобальный хук Insert: " + ex.Message);
        }

        _lifetime = new CancellationTokenSource();

        // --- 2. Состояние и сервисы. ---
        Services = new AppServices { Host = this };
        Services.Localization.Apply(Services.Session.UiCulture, saveSelection: false);

        // --- 3. Единственное окно. ---
        var window = new MainWindow(Services);
        MainWindow = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Единственный легальный путь завершения симулятора.</summary>
    public void RequestShutdown()
    {
        if (_exiting)
            return;

        _exiting = true;
        Log("Выход по Insert.");

        try
        {
            _lifetime?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (_insertHook is not null)
        {
            _insertHook.InsertPressed -= OnInsertPressed;
            _insertHook.Dispose();
            _insertHook = null;
        }

        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _lifetime?.Cancel();
            // Последний шанс сохранить фазу: InstallProgress мог обновиться
            // между экранами, а файл должен описывать актуальное состояние.
            Services?.Store.Save(Services.Session);
        }
        catch (Exception ex)
        {
            Log("Не удалось сохранить состояние при выходе: " + ex.Message);
        }
        finally
        {
            _insertHook?.Dispose();
            _lifetime?.Dispose();
            TaskbarVisibility.Show();   // вернуть реальную панель задач, что бы ни случилось
        }

        base.OnExit(e);
    }

    private void OnInsertPressed(object? sender, EventArgs e)
    {
        // Колбэк хука приходит на UI-потоке, но BeginInvoke защищает от изменений
        // в будущем (например, если хук переедет на отдельный поток).
        Dispatcher.BeginInvoke(new Action(RequestShutdown), DispatcherPriority.Send);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("UI-исключение: " + e.Exception);
        e.Handled = true;               // не оставляем пользователя в мёртвом полноэкранном окне
        RequestShutdown();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log("Фатальное исключение: " + e.ExceptionObject);
    }

    private static void Log(string message)
    {
        try
        {
            AppPaths.EnsureCreated();
            File.AppendAllText(
                AppPaths.LogFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Логирование не должно само убивать приложение.
        }
    }
}

using System.Windows;
using System.Windows.Input;
using Win10Sim.App.Boot;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Platform;

namespace Win10Sim.App;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly CancellationTokenSource _lifetime;

    public MainWindow(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _lifetime = new CancellationTokenSource();

        InitializeComponent();

        Screens = new ScreenManager(ScreenHost);
        Flow = new BootFlow(_services, Screens);

        Loaded += OnLoaded;
        Closed += (_, _) => _lifetime.Cancel();

        // Резервный путь на случай, если низкоуровневый хук не установился
        // (групповые политики, античит, запуск под отладчиком).
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public ScreenManager Screens { get; }

    public BootFlow Flow { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Fullscreen.Apply(this);

        // Для полного киоска: панель задач реальной Windows будет скрыта.
        // По умолчанию выключено — см. Platform/TaskbarVisibility.cs.
        // TaskbarVisibility.Hide();

        Activate();
        Focus();

        try
        {
            await Flow.RunAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            // Штатный выход по Insert.
        }
        catch (Exception)
        {
            // Любая ошибка машины состояний не должна оставлять пользователя
            // в полноэкранном окне без выхода. Детали уже записаны в app.log
            // обработчиком DispatcherUnhandledException в App.
            (Application.Current as App)?.RequestShutdown();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Insert || (e.Key == Key.System && e.SystemKey == Key.Insert))
        {
            e.Handled = true;
            (Application.Current as App)?.RequestShutdown();
        }
    }
}

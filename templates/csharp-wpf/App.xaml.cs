using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Win10Sim.Template.Platform;

namespace Win10Sim.Template
{
    /// <summary>
    /// Точка входа симулятора.
    /// Правила, зафиксированные здесь навсегда (см. docs/ARCHITECTURE.md §1):
    ///   1. Выход — ТОЛЬКО клавиша Insert. ShutdownMode = OnExplicitShutdown,
    ///      поэтому ни одно окно не может закрыть приложение «само».
    ///   2. Хук Insert ставится ДО показа первого окна: даже если UI упадёт
    ///      на старте, пользователь сможет выйти на реальный рабочий стол.
    /// </summary>
    public partial class App : Application
    {
        private InsertKeyHook? _insertHook;
        private bool _exiting;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            // 1. Сначала — аварийный выход, потом всё остальное.
            _insertHook = new InsertKeyHook();
            _insertHook.InsertPressed += OnInsertPressed;
            try
            {
                _insertHook.Install();
            }
            catch (Exception ex)
            {
                // Хук не поставился (экзотика/политика) — окно всё равно получит
                // Insert через PreviewKeyDown, поэтому не падаем.
                Log("Не удалось установить глобальный хук клавиатуры: " + ex.Message);
            }

            // 2. Единственное окно приложения — на весь экран, без рамки.
            var shell = new MainWindow();
            MainWindow = shell;
            shell.Show();
            shell.Activate();
        }

        private void OnInsertPressed(object? sender, EventArgs e)
        {
            // Колбэк низкоуровневого хука приходит на UI-потоке (хук ставился здесь),
            // но BeginInvoke защищает от «выхода из чужого потока» в будущем.
            Dispatcher.BeginInvoke(new Action(RequestExit), DispatcherPriority.Send);
        }

        /// <summary>Единственный легальный путь завершения симулятора.</summary>
        public void RequestExit()
        {
            if (_exiting)
                return;

            _exiting = true;

            if (_insertHook != null)
            {
                _insertHook.InsertPressed -= OnInsertPressed;
                _insertHook.Dispose();
                _insertHook = null;
            }

            Shutdown(0);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _insertHook?.Dispose();
            TaskbarVisibility.Show();   // вернуть реальную панель задач, чем бы ни закончилось
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("UI-исключение: " + e.Exception);
            e.Handled = true;           // не оставляем пользователя в мёртвом полноэкранном окне
            RequestExit();
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log("Фатальное исключение: " + e.ExceptionObject);
        }

        private static void Log(string message)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Win10Sim");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "app.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch
            {
                // логирование не должно само убивать приложение
            }
        }
    }
}

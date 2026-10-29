using System;
using System.Runtime.InteropServices;

namespace Win10Sim.App.Platform
{
    /// <summary>
    /// Скрытие РЕАЛЬНОЙ панели задач Windows.
    ///
    /// По умолчанию НЕ используется: окно симулятора — topmost и растянуто на
    /// monitor bounds, поэтому панель задач и так оказывается позади него.
    /// Это безопасный путь: если процесс убьют, панель задач никуда не денется.
    ///
    /// Метод Hide() нужен для режима «полного киоска». Он опасен: при жёстком
    /// завершении процесса (Task Manager → End task) панель задач останется
    /// скрытой до перезапуска explorer.exe. Поэтому здесь же стоит страховка
    /// ProcessExit, а в App.OnExit вызывается Show().
    /// </summary>
    internal static class TaskbarVisibility
    {
        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;

        private static bool _hidden;
        private static bool _safetyNetInstalled;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void Hide()
        {
            InstallSafetyNet();
            _hidden = true;

            ShowWindow(FindWindow("Shell_TrayWnd", null), SW_HIDE);          // основная панель
            ShowWindow(FindWindow("Shell_SecondaryTrayWnd", null), SW_HIDE); // панели на других мониторах
        }

        public static void Show()
        {
            ShowWindow(FindWindow("Shell_TrayWnd", null), SW_SHOWNORMAL);
            ShowWindow(FindWindow("Shell_SecondaryTrayWnd", null), SW_SHOWNORMAL);
            _hidden = false;
        }

        private static void InstallSafetyNet()
        {
            if (_safetyNetInstalled)
                return;

            _safetyNetInstalled = true;
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            {
                if (_hidden)
                    Show();
            };
        }
    }
}

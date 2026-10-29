using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Win10Sim.Template.Platform
{
    /// <summary>
    /// Borderless fullscreen («полноэкранный без рамки»).
    ///
    /// WPF не умеет exclusive fullscreen, и это не нужно: окно без рамки,
    /// растянутое на rcMonitor (а не на рабочую область rcWork), визуально
    /// неотличимо от полноэкранного режима и не мигает при переключении.
    ///
    /// Шаги:
    ///   1) снять WindowStyle/ResizeMode на уровне WPF;
    ///   2) снять у HWND биты WS_CAPTION | WS_THICKFRAME | WS_SYSMENU |
    ///      WS_MINIMIZEBOX | WS_MAXIMIZEBOX  → нет заголовка, нет Alt+Space,
    ///      нет кнопок «свернуть/развернуть/закрыть»;
    ///   3) SetWindowPos на прямоугольник монитора в ФИЗИЧЕСКИХ пикселях
    ///      (поэтому шаблон помечен PerMonitorV2 в app.manifest).
    /// </summary>
    internal static class Fullscreen
    {
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;   // весь монитор, включая область панели задач
            public RECT rcWork;      // рабочая область
            public uint dwFlags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        /// <summary>Применяет полноэкранный режим. Вызывать после Show() — нужен HWND.</summary>
        public static void Apply(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.ShowInTaskbar = true;

            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                // Окно ещё не создано — применим, как только появится HWND.
                window.SourceInitialized += (_, __) => ApplyNative(window);
                return;
            }

            ApplyNative(window);

            // Смена разрешения / подключение второго монитора — пересчитываем границы.
            SystemEvents.DisplaySettingsChanged += (_, __) =>
                window.Dispatcher.BeginInvoke(new Action(() => Reapply(window)));
        }

        /// <summary>Перечитать прямоугольник монитора и растянуть окно заново.</summary>
        public static void Reapply(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
                StretchToMonitor(hwnd);
        }

        private static void ApplyNative(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;

            // 1. Убираем всё, что рисует «настоящая» Windows.
            long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));

            // 2. Не показываем окно в Alt+Tab-превью как «инструментальное» —
            //    наоборот, оставляем обычным. Закомментировано намеренно:
            //    раскомментируйте, если нужно полностью спрятать окно из переключателя.
            // long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            // SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TOOLWINDOW));

            // 3. Растягиваем на весь монитор и поднимаем наверх.
            StretchToMonitor(hwnd);
        }

        private static void StretchToMonitor(IntPtr hwnd)
        {
            RECT bounds;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var info = new MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(monitor, ref info))
                {
                    bounds = info.rcMonitor;
                }
                else
                {
                    bounds = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
                }
            }
            else
            {
                bounds = new RECT
                {
                    Left = 0,
                    Top = 0,
                    Right = (int)SystemParameters.PrimaryScreenWidth,
                    Bottom = (int)SystemParameters.PrimaryScreenHeight
                };
            }

            SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                bounds.Left,
                bounds.Top,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top,
                SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
        }
    }
}

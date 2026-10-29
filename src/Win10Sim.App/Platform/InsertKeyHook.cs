using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Win10Sim.App.Platform
{
    /// <summary>
    /// Глобальный перехват клавиши Insert (WH_KEYBOARD_LL).
    ///
    /// Почему низкоуровневый хук, а не Window.KeyDown:
    ///   * событие приходит, даже если фокус увели в другое окно (Alt+Tab, UAC-запрос);
    ///   * Insert можно «съесть» (вернуть 1 из колбэка), чтобы клавиша не
    ///     доходила до чужого приложения;
    ///   * работает до появления первого окна — ставится в App.OnStartup.
    ///
    /// Требования: установка и снятие хука — на потоке с циклом сообщений
    /// (у нас это главный UI-поток), делегат хранится в поле, иначе GC его
    /// соберёт и приложение упадёт при первом нажатии.
    /// </summary>
    internal sealed class InsertKeyHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_INSERT = 0x2D;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private readonly LowLevelKeyboardProc _callback;  // держим ссылку: GC-safety
        private IntPtr _hook = IntPtr.Zero;
        private bool _disposed;

        public event EventHandler? InsertPressed;

        public InsertKeyHook()
        {
            _callback = HookCallback;
        }

        public bool IsInstalled => _hook != IntPtr.Zero;

        public void Install()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InsertKeyHook));

            if (_hook != IntPtr.Zero)
                return;

            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "SetWindowsHookEx(WH_KEYBOARD_LL) не удался");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (data.vkCode == VK_INSERT)
                {
                    InsertPressed?.Invoke(this, EventArgs.Empty);

                    // Клавиша до чужих приложений не доходит.
                    return new IntPtr(1);
                }
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}

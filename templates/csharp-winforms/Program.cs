using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Win10Sim.WinFormsTemplate
{
    /// <summary>
    /// Тот же контракт, что и в WPF-шаблоне, но на WinForms — для сравнения.
    /// Полноэкранность: FormBorderStyle.None + Bounds = Screen.Bounds (НЕ WorkingArea).
    /// Выход: Form.KeyPreview + Insert, либо глобальный хук (см. ниже).
    /// </summary>
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var form = new ShellForm())
            {
                // Безрамочное окно на весь экран монитора, под которым оно находится.
                form.FormBorderStyle = FormBorderStyle.None;
                form.WindowState = FormWindowState.Normal;
                form.StartPosition = FormStartPosition.Manual;
                form.Bounds = Screen.FromHandle(form.Handle).Bounds;
                form.TopMost = true;
                form.KeyPreview = true;                 // форма получает клавиши ПЕРВОЙ
                form.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Insert)
                        form.Close();                   // единственный путь выхода
                };

                // Резервный вариант: спрятать панель задач (см. предупреждение в WPF-шаблоне).
                ShowWindow(FindWindow("Shell_TrayWnd", null), 0);

                Application.Run(form);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }
}

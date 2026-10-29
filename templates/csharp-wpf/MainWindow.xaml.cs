using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Win10Sim.Template.Platform;

namespace Win10Sim.Template
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;

        public MainWindow()
        {
            InitializeComponent();

            _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (_, __) => UpdateClock();

            Loaded += OnLoaded;
            Closed += (_, __) => _clockTimer.Stop();

            // Резервный путь: если низкоуровневый хук не установился,
            // Insert всё равно закроет приложение, когда окно в фокусе.
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Полноэкранный режим применяем, когда HWND уже существует.
            Fullscreen.Apply(this);

            // Для режима полного киоска раскомментируйте:
            // TaskbarVisibility.Hide();

            UpdateClock();
            _clockTimer.Start();

            Activate();
            Focus();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Insert || e.Key == Key.System && e.SystemKey == Key.Insert)
            {
                e.Handled = true;
                (Application.Current as App)?.RequestExit();
            }
        }

        private void UpdateClock()
        {
            DateTime now = DateTime.Now;
            ClockText.Text = now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)
                             + "   ·   "
                             + now.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Win10Sim.App.Boot;

/// <summary>
/// Подмена содержимого единственного окна. Никаких новых HWND: смена экрана —
/// это кросс-фейд внутри одного ContentControl (как переходы в программе
/// установки Windows 10, где фон не мигает между шагами).
/// </summary>
public sealed class ScreenManager
{
    private const int FadeOutMs = 140;
    private const int FadeInMs = 220;

    private readonly ContentControl _host;

    public ScreenManager(ContentControl host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public FrameworkElement? Current => _host.Content as FrameworkElement;

    public async Task ShowAsync(FrameworkElement view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (Current is not null)
            await FadeAsync(1.0, 0.0, FadeOutMs, cancellationToken).ConfigureAwait(true);

        _host.Content = view;
        _host.Opacity = 0;
        await FadeAsync(0.0, 1.0, FadeInMs, cancellationToken).ConfigureAwait(true);
    }

    private async Task FadeAsync(double from, double to, int milliseconds, CancellationToken cancellationToken)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        };

        _host.BeginAnimation(UIElement.OpacityProperty, animation);
        await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(true);
        _host.BeginAnimation(UIElement.OpacityProperty, null);
        _host.Opacity = to;
    }
}

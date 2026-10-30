using System.Windows;
using System.Windows.Controls;

namespace Win10Sim.App.Shell;

/// <summary>
/// Менеджер внутренних окон рабочего стола: z-order, единственный активный
/// экземпляр приложения, связка с панелью задач.
///
/// Все окна лежат на одном Canvas. z-order задаётся через
/// <see cref="Panel.ZIndexProperty"/>: активированное окно получает максимальный,
/// панель задач и меню «Пуск» живут на отдельных слоях с заведомо большим индексом.
/// </summary>
public sealed class SimWindowManager
{
    private readonly List<SimWindow> _windows = new();
    private Canvas? _canvas;
    private int _zCounter = 10;

    /// <summary>Привязка к слою окон конкретной оболочки (после «перезагрузки» создаётся новый).</summary>
    public void Attach(Canvas canvas)
    {
        _canvas = canvas;

        foreach (SimWindow window in _windows)
        {
            if (!canvas.Children.Contains(window))
                canvas.Children.Add(window);
        }
    }

    /// <summary>Оболочка закрывается: окна остаются в памяти (они и есть состояния приложений).</summary>
    public void Detach()
    {
        _windows.ForEach(w => _canvas?.Children.Remove(w));
        _canvas = null;
        Active = null;
    }

    public IReadOnlyList<SimWindow> Windows => _windows;

    public SimWindow? Active { get; private set; }

    /// <summary>Экран поменялся/закрыт — панель задач перерисовывает кнопки.</summary>
    public event EventHandler? Changed;

    /// <summary>Окно закрыто пользователем (нужно, чтобы приложение удалило ссылки).</summary>
    public event EventHandler<SimWindow>? WindowClosed;

    public SimWindow Open(string appId, Func<SimWindow> factory, bool singleInstance = true)
    {
        if (singleInstance)
        {
            SimWindow? existing = _windows.FirstOrDefault(w => w.AppId == appId);
            if (existing is not null)
            {
                Activate(existing);
                return existing;
            }
        }

        SimWindow window = factory();
        window.WorkArea = WorkArea;
        window.PlaceAt(_windows.Count);
        window.MinimizeRequested += (_, _) => Minimize(window);
        window.CloseRequested += (_, _) => Close(window);
        window.ActivateRequested += (_, _) => Activate(window);

        _windows.Add(window);
        _canvas?.Children.Add(window);

        Activate(window);
        return window;
    }

    public void Activate(SimWindow window)
    {
        if (window.IsMinimized)
            window.Restore();

        Panel.SetZIndex(window, ++_zCounter);

        foreach (SimWindow other in _windows)
            other.SetActive(ReferenceEquals(other, window));

        Active = window;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Minimize(SimWindow window)
    {
        window.Minimize();

        if (ReferenceEquals(Active, window))
        {
            Active = null;
            SimWindow? next = _windows.LastOrDefault(w => !w.IsMinimized);
            if (next is not null)
                Activate(next);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Close(SimWindow window)
    {
        _canvas?.Children.Remove(window);
        _windows.Remove(window);

        if (ReferenceEquals(Active, window))
        {
            Active = null;
            SimWindow? next = _windows.LastOrDefault(w => !w.IsMinimized);
            if (next is not null)
                Activate(next);
        }

        WindowClosed?.Invoke(this, window);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CloseAll()
    {
        foreach (SimWindow window in _windows.ToList())
            Close(window);
    }

    /// <summary>Рабочая область под панелью задач — сюда менеджер вписывает окна.</summary>
    public Size WorkArea { get; set; } = new(1600, 900);

    public void UpdateWorkArea(Size workArea)
    {
        WorkArea = workArea;

        foreach (SimWindow window in _windows)
            window.ClampToWorkArea(workArea);
    }
}

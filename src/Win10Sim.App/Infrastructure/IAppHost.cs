namespace Win10Sim.App.Infrastructure;

/// <summary>
/// То немногое, что экранам нужно от приложения. Позволяет не тащить
/// <c>Application.Current</c> в каждый UserControl и держать экраны тестируемыми.
/// </summary>
public interface IAppHost
{
    /// <summary>Завершить симулятор. Вызывается ТОЛЬКО из обработчика Insert и кнопки выхода.</summary>
    void RequestShutdown();
}

internal sealed class NullAppHost : IAppHost
{
    public static readonly NullAppHost Instance = new();

    public void RequestShutdown()
    {
    }
}

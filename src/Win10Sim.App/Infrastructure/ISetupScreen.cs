using Win10Sim.Core.Setup;

namespace Win10Sim.App.Infrastructure;

/// <summary>
/// Контракт экрана. Экран не навигирует сам — он сообщает, какая фаза должна
/// наступить следующей, а решение и сохранение состояния делает <see cref="Boot.BootFlow"/>.
/// Это единственное место, где экраны «знают» про порядок этапов.
/// </summary>
public interface ISetupScreen
{
    /// <summary>Запросить переход к фазе <paramref name="nextPhase"/>.</summary>
    event EventHandler<OsPhase>? AdvanceRequested;

    /// <summary>Вызывается после показа экрана: здесь стартуют таймеры/анимации/установка.</summary>
    Task OnEnterAsync(CancellationToken cancellationToken);
}

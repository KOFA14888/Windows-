using System.Windows.Controls;
using System.Windows.Input;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Infrastructure;

/// <summary>
/// База для экранов установки: доступ к сервисам, запрос перехода,
/// обработка Esc («Назад»).
/// </summary>
public abstract class SetupScreenBase : UserControl, ISetupScreen
{
    protected SetupScreenBase(AppServices services)
    {
        Services = services;

        // Клавиатура: экран получает фокус при появлении, чтобы Esc работал
        // даже если пользователь ещё никуда не кликнул.
        Focusable = true;
        Loaded += (_, _) => Focus();

        // Insert уже перехвачен глобальным хуком, сюда он не доходит.
        PreviewKeyDown += OnScreenPreviewKeyDown;
    }

    protected AppServices Services { get; }

    protected SetupSession Session => Services.Session;

    /// <summary>Фаза, на которую ведёт Esc. null — «назад» с этого экрана некуда.</summary>
    protected virtual OsPhase? BackTarget => null;

    /// <summary>
    /// false — экран занят (идёт форматирование/установка) и уходить с него нельзя.
    /// </summary>
    protected virtual bool CanNavigateBack => true;

    public event EventHandler<OsPhase>? AdvanceRequested;

    public virtual Task OnEnterAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected void Advance(OsPhase nextPhase) => AdvanceRequested?.Invoke(this, nextPhase);

    private void OnScreenPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && CanNavigateBack && BackTarget is { } target)
        {
            e.Handled = true;
            Advance(target);
        }
    }
}

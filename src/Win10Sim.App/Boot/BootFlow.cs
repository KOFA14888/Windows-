using System.Windows;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Screens.Boot;
using Win10Sim.App.Screens.Oobe;
using Win10Sim.App.Screens.Placeholder;
using Win10Sim.App.Screens.Setup;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Boot;

/// <summary>
/// Машина состояний «жизни» симулятора.
///
/// Единственное место, где записано, какой экран соответствует какой фазе и
/// куда эта фаза ведёт дальше. Экраны о порядке ничего не знают — они лишь
/// просят перейти к следующей фазе.
///
/// Цикл (а не один проход) нужен для «перезагрузки»: экран завершения
/// запрашивает фазу Firmware, BootFlow сбрасывает сессию и снова показывает
/// логотип — уже с другим продолжением.
/// </summary>
public sealed class BootFlow
{
    private readonly AppServices _services;
    private readonly ScreenManager _screens;

    public BootFlow(AppServices services, ScreenManager screens)
    {
        _services = services;
        _screens = screens;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            OsPhase phase = _services.Session.Phase;

            FrameworkElement? view = CreateScreen(phase);
            if (view is null)
                return;                      // фаза следующего этапа — этот код её не обслуживает

            OsPhase next = await ShowScreenAsync(view, cancellationToken).ConfigureAwait(true);

            if (next == OsPhase.Firmware)
            {
                // Полный цикл заново: пользователь начал установку с нуля.
                _services.Session.ResetForNewInstall();
                _services.Session.Phase = OsPhase.WindowsLogo;
                _services.SaveState();
                continue;
            }

            _services.Session.Phase = next;
            _services.SaveState();           // фаза на диске — до показа следующего экрана
        }
    }

    private FrameworkElement? CreateScreen(OsPhase phase) => phase switch
    {
        // ---------- ЭТАП 1: установка ----------
        OsPhase.Firmware or OsPhase.WindowsLogo =>
            new BootLogoView(_services, OsPhase.SetupLanguage, SetupTimings.WindowsLogo),

        OsPhase.SetupLanguage => new LanguageView(_services),
        OsPhase.SetupInstall => new InstallNowView(_services),
        OsPhase.SetupKey => new ProductKeyView(_services),
        OsPhase.SetupEdition => new EditionView(_services),
        OsPhase.SetupEula => new EulaView(_services),
        OsPhase.SetupInstallType => new InstallTypeView(_services),
        OsPhase.SetupDisk => new DiskView(_services),
        OsPhase.SetupCopying => new CopyingView(_services),

        // Логотип после перезагрузки — уже «первый запуск установленной системы».
        OsPhase.Reboot =>
            new BootLogoView(_services, OsPhase.OobeWait, SetupTimings.RebootLogo),

        // ---------- ЭТАП 2: первая настройка (OOBE) ----------
        OsPhase.OobeWait => new OobeWaitView(_services),
        OsPhase.OobeRegion => new OobeRegionView(_services),
        OsPhase.OobeKeyboard => new OobeKeyboardView(_services),
        OsPhase.OobeAccount => new OobeAccountView(_services),
        OsPhase.OobePassword => new OobePasswordView(_services),
        OsPhase.OobeHello => new OobeHelloView(_services),
        OsPhase.OobePreparing => new OobePreparingView(_services),

        // ---------- ЭТАП 3 ещё не реализован ----------
        // LockScreen/Desktop обслуживает экран-итог OOBE, пока ЭТАП 3 не добавит
        // настоящий экран блокировки и рабочий стол.
        OsPhase.LockScreen or OsPhase.Desktop => new OobeCompleteView(_services),

        // Неизвестная фаза (например, из более новой версии state.json):
        // лучше показать итог установки, чем пустое чёрное окно без выхода.
        _ => new SetupCompleteView(_services)
    };

    private async Task<OsPhase> ShowScreenAsync(FrameworkElement view, CancellationToken cancellationToken)
    {
        if (view is not ISetupScreen screen)
            throw new InvalidOperationException($"Экран {view.GetType().Name} не реализует ISetupScreen.");

        var completion = new TaskCompletionSource<OsPhase>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnAdvance(object? sender, OsPhase nextPhase) => completion.TrySetResult(nextPhase);

        screen.AdvanceRequested += OnAdvance;
        try
        {
            await _screens.ShowAsync(view, cancellationToken).ConfigureAwait(true);
            await screen.OnEnterAsync(cancellationToken).ConfigureAwait(true);

            using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
            {
                return await completion.Task.ConfigureAwait(true);
            }
        }
        finally
        {
            screen.AdvanceRequested -= OnAdvance;
        }
    }
}

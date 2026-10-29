using Win10Sim.App.Localization;
using Win10Sim.Core.Persistence;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Infrastructure;

/// <summary>
/// Ручной DI-контейнер: сервисы создаются один раз на процесс и живут
/// столько же, сколько приложение. Никаких статических синглтонов, кроме
/// <c>Application.Current</c>, который нужен самому WPF.
/// </summary>
public sealed class AppServices
{
    public AppServices(JsonStateStore? stateStore = null)
    {
        AppPaths.EnsureCreated();

        Store = stateStore ?? new JsonStateStore();
        Session = Store.Load();
        Disks = DiskFactory.CreateDefault();
        Installer = new SetupInstaller();
        Localization = new LocalizationService();
    }

    public IAppHost Host { get; set; } = NullAppHost.Instance;

    /// <summary>Состояние установки. Живёт весь процесс и сохраняется после каждого экрана.</summary>
    public SetupSession Session { get; }

    public JsonStateStore Store { get; }

    /// <summary>«Железо» — список дисков для экрана выбора раздела.</summary>
    public IReadOnlyList<DiskInfo> Disks { get; }

    public SetupInstaller Installer { get; }

    public LocalizationService Localization { get; }

    /// <summary>Сохраняет фазу на диск. Вызывается BootFlow после каждого перехода.</summary>
    public void SaveState()
    {
        try
        {
            Store.Save(Session);
        }
        catch (IOException)
        {
            // Диск занят/недоступен — симулятор продолжает работать в памяти.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

using System.Windows;
using Win10Sim.App.Apps.Cmd;
using Win10Sim.App.Apps.Edge;
using Win10Sim.App.Apps.Explorer;
using Win10Sim.App.Apps.Notepad;
using Win10Sim.App.Apps.Settings;
using Win10Sim.App.Infrastructure;

namespace Win10Sim.App.Shell;

/// <summary>Описание приложения: как оно выглядит в «Пуске» и что открывает.</summary>
public sealed class AppDescriptor
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Глиф Segoe MDL2 Assets.</summary>
    public required string Glyph { get; init; }

    public required Func<AppServices, FrameworkElement> CreateContent { get; init; }

    /// <summary>Плитка в меню «Пуск» (как «Почта», «Погода» и т. п.).</summary>
    public bool PinnedTile { get; init; }

    public string TileColor { get; init; } = "#0078D7";

    /// <summary>Параметры запуска, специфичные для приложения (например, путь в Проводнике).</summary>
    public string? Arguments { get; set; }
}

/// <summary>
/// Каталог приложений. Единственное место, где перечислены программы системы:
/// «Пуск», панель задач, поиск и Проводник («Открыть с помощью») берут список отсюда.
/// </summary>
public static class AppCatalog
{
    public const string Explorer = "explorer";
    public const string Notepad = "notepad";
    public const string Edge = "edge";
    public const string Cmd = "cmd";
    public const string Settings = "settings";

    public static IReadOnlyList<AppDescriptor> All { get; } = new[]
    {
        new AppDescriptor
        {
            Id = Explorer,
            Name = "Проводник",
            Glyph = "\uE8B7",
            CreateContent = services => new ExplorerView(services, services.FileSystem),
            PinnedTile = true,
            TileColor = "#0078D7"
        },
        new AppDescriptor
        {
            Id = Edge,
            Name = "Microsoft Edge",
            Glyph = "\uE774",
            CreateContent = services => new EdgeView(services),
            PinnedTile = true,
            TileColor = "#0F6CBD"
        },
        new AppDescriptor
        {
            Id = Notepad,
            Name = "Блокнот",
            Glyph = "\uE70F",
            CreateContent = services => new NotepadView(services),
            PinnedTile = true,
            TileColor = "#2B579A"
        },
        new AppDescriptor
        {
            Id = Cmd,
            Name = "Командная строка",
            Glyph = "\uE756",
            CreateContent = services => new CmdView(services),
            PinnedTile = true,
            TileColor = "#1F1F1F"
        },
        new AppDescriptor
        {
            Id = Settings,
            Name = "Параметры",
            Glyph = "\uE713",
            CreateContent = services => new SettingsView(services),
            PinnedTile = true,
            TileColor = "#5C2D91"
        }
    };

    public static AppDescriptor? Find(string appId)
        => All.FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Открывает приложение во внутреннем окне. Повторный запуск активирует
    /// уже открытое окно — как в настоящей Windows для одноэкземплярных программ.
    /// </summary>
    public static SimWindow Launch(AppServices services, string appId, string? arguments = null)
    {
        AppDescriptor? app = Find(appId)
            ?? throw new InvalidOperationException($"Приложение не найдено: {appId}");

        return services.Windows.Open(app.Id, () =>
        {
            FrameworkElement content = app.CreateContent(services);

            // Аргументы передаются содержимому через общий контракт: Проводник
            // открывает указанную папку, Блокнот — указанный файл.
            if (arguments is { Length: > 0 } && content is ISimApp appContent)
                appContent.HandleArguments(arguments);

            var window = new SimWindow(app.Name, app.Glyph, content, app.Id);

            // Содержимое создаётся раньше окна, поэтому окно сообщает о себе
            // через отдельный интерфейс (нужно Блокноту и cmd).
            if (content is ISimWindowAware aware)
                aware.AttachWindow(window);

            return window;
        });
    }
}

/// <summary>Приложение, которому можно передать аргументы запуска.</summary>
public interface ISimApp
{
    void HandleArguments(string arguments);
}

/// <summary>Приложению нужно знать своё окно (заголовок, закрытие по команде «exit»).</summary>
public interface ISimWindowAware
{
    void AttachWindow(SimWindow window);
}

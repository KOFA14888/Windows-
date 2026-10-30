using System.IO;
using System.Text.Json;

namespace Win10Sim.App.Infrastructure;

/// <summary>
/// Персонализация рабочего стола. Отдельно от state.json: установка закончена,
/// и «сброс установки» не должен сбрасывать обои или формат часов.
/// </summary>
public sealed class DesktopSettings
{
    public string Theme { get; set; } = "dark";

    /// <summary>Формат часов в трее. Выбор формата времени на ЭТАПЕ 1 задаёт значение по умолчанию.</summary>
    public string ClockFormat { get; set; } = "HH:mm";

    public bool ShowSeconds { get; set; }

    public string AccentColor { get; set; } = "#0078D7";

    /// <summary>Имя файла обоев из Assets.</summary>
    public string Wallpaper { get; set; } = "Wallpaper.jpg";

    public bool FirstRunTipsShown { get; set; }

    public static string StoragePath => Path.Combine(Core.Persistence.AppPaths.Root, "desktop.json");

    public static DesktopSettings Load()
    {
        try
        {
            if (File.Exists(StoragePath))
            {
                return JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(StoragePath))
                       ?? new DesktopSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        return new DesktopSettings();
    }

    public void Save()
    {
        try
        {
            Core.Persistence.AppPaths.EnsureCreated();
            File.WriteAllText(StoragePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

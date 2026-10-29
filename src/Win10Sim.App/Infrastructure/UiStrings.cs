using System.Globalization;
using System.Windows;

namespace Win10Sim.App.Infrastructure;

/// <summary>
/// Доступ к строкам интерфейса из кода (там, где {DynamicResource} неприменим:
/// строки с подстановкой, тексты в сгенерированных строках списков).
/// </summary>
public static class UiStrings
{
    public static string Get(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Размер в человекочитаемом виде: "475,0 ГБ", "500 МБ".</summary>
    public static string FormatSize(long bytes)
    {
        const long gb = 1_000_000_000;
        const long mb = 1_000_000;

        return bytes >= gb
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.0} {1}", bytes / (double)gb, Get("L.Unit.GB"))
            : string.Format(CultureInfo.CurrentCulture, "{0:0} {1}", bytes / (double)mb, Get("L.Unit.MB"));
    }
}

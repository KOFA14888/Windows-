namespace Win10Sim.Core.Setup;

/// <summary>Раскладка клавиатуры в том виде, как её показывает OOBE.</summary>
public sealed record KeyboardInfo(string Code, string DisplayName, string LayoutName);

public static class KeyboardCatalog
{
    public static IReadOnlyList<KeyboardInfo> All { get; } = new[]
    {
        new KeyboardInfo("ru-RU", "Русская", "RU"),
        new KeyboardInfo("en-US", "Английская (США)", "US"),
        new KeyboardInfo("en-GB", "Английская (Великобритания)", "UK"),
        new KeyboardInfo("de-DE", "Немецкая", "DE"),
        new KeyboardInfo("fr-FR", "Французская", "FR"),
        new KeyboardInfo("pl-PL", "Польская", "PL"),
        new KeyboardInfo("uk-UA", "Украинская", "UA"),
        new KeyboardInfo("by-BY", "Белорусская", "BY")
    };

    /// <summary>Основная раскладка по языку установки.</summary>
    public static KeyboardInfo DefaultFor(string installLanguage)
        => All.FirstOrDefault(k => k.Code.Equals(installLanguage, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static KeyboardInfo Find(string code)
        => All.FirstOrDefault(k => k.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}

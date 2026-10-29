namespace Win10Sim.Core.Setup;

/// <summary>Регион, который OOBE предлагает выбрать («Ваш регион?»).</summary>
public sealed record RegionInfo(string Code, string DisplayName, string TimeZone);

/// <summary>
/// Небольшой каталог регионов. Полный список Windows содержит ~250 стран;
/// здесь — самые ходовые, при этом названия даны так, как их показывает сама
/// Windows на выбранном языке интерфейса.
/// </summary>
public static class RegionCatalog
{
    public static IReadOnlyList<RegionInfo> All { get; } = new[]
    {
        new RegionInfo("BY", "Беларусь", "Минск"),
        new RegionInfo("RU", "Россия", "Москва"),
        new RegionInfo("KZ", "Казахстан", "Астана"),
        new RegionInfo("UA", "Украина", "Киев"),
        new RegionInfo("US", "США", "UTC-05:00 Восточное время (США и Канада)"),
        new RegionInfo("GB", "Соединенное Королевство", "UTC+00:00 Лондон"),
        new RegionInfo("DE", "Германия", "UTC+01:00 Берлин"),
        new RegionInfo("FR", "Франция", "UTC+01:00 Париж"),
        new RegionInfo("PL", "Польша", "UTC+01:00 Варшава"),
        new RegionInfo("TR", "Турция", "UTC+03:00 Стамбул"),
        new RegionInfo("CN", "Китай", "UTC+08:00 Пекин"),
        new RegionInfo("JP", "Япония", "UTC+09:00 Токио"),
        new RegionInfo("IN", "Индия", "UTC+05:30 Ченнаи"),
        new RegionInfo("BR", "Бразилия", "UTC-03:00 Бразилиа"),
        new RegionInfo("CA", "Канада", "UTC-05:00 Восточное время (США и Канада)"),
        new RegionInfo("AU", "Австралия", "UTC+10:00 Канберра")
    };

    /// <summary>Регион по умолчанию — по языку установки.</summary>
    public static RegionInfo DefaultFor(string installLanguage)
    {
        string culture = installLanguage ?? string.Empty;

        return culture.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? Find("BY")
             : culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? Find("US")
             : culture.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? Find("DE")
             : All[0];
    }

    public static RegionInfo Find(string code)
        => All.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}

using System.Globalization;
using System.Windows;

namespace Win10Sim.App.Localization;

/// <summary>
/// Смена языка интерфейса без перезапуска.
///
/// Устроено как подмена одного ResourceDictionary в Application.Resources:
/// все строки в XAML взяты через {DynamicResource L.*}, поэтому WPF сам
/// перерисует зависящие от них элементы, как только словарь будет заменён.
/// Это принципиально для экрана языка установки: там выбор языка должен
/// немедленно менять всю программу установки.
/// </summary>
public sealed class LocalizationService
{
    private const string DictionaryMarker = "Lang.";
    private ResourceDictionary? _current;

    public string Culture { get; private set; } = "ru";

    public void Apply(string culture, bool saveSelection = true)
    {
        Culture = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";

        // Абсолютный pack-URI: код не наследует BaseUri от XAML, и относительный
        // путь в этом месте зависел бы от того, откуда запущено приложение.
        string assemblyName = typeof(LocalizationService).Assembly.GetName().Name!;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/{assemblyName};component/Themes/Lang.{Culture}.xaml",
                UriKind.Absolute)
        };

        IList<ResourceDictionary> merged = Application.Current.Resources.MergedDictionaries;

        if (_current is not null)
        {
            int index = merged.IndexOf(_current);
            if (index >= 0)
                merged[index] = dictionary;      // та же позиция — приоритет не меняется
            else
                merged.Add(dictionary);
        }
        else
        {
            int existing = FindExistingIndex(merged);
            if (existing >= 0)
            {
                _current = merged[existing];
                merged[existing] = dictionary;
            }
            else
            {
                merged.Add(dictionary);
            }
        }

        _current = dictionary;

        // Форматирование даты/чисел тоже должно следовать языку установки.
        var cultureInfo = Culture == "ru"
            ? new CultureInfo("ru-RU")
            : new CultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        Thread.CurrentThread.CurrentCulture = cultureInfo;
        Thread.CurrentThread.CurrentUICulture = cultureInfo;

        if (saveSelection)
        {
            var app = Application.Current as App;
            if (app?.Services is { } services)
            {
                services.Session.InstallLanguage = Culture == "ru" ? "ru-RU" : "en-US";
                services.SaveState();
            }
        }
    }

    private static int FindExistingIndex(IList<ResourceDictionary> merged)
    {
        for (int i = 0; i < merged.Count; i++)
        {
            Uri? source = merged[i].Source;
            if (source is not null &&
                source.OriginalString.Contains(DictionaryMarker, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}

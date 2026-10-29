using System.Xml.Linq;
using Win10Sim.Core.Setup;
using Xunit;

namespace Win10Sim.Core.Tests;

public class LicenseAgreementTests
{
    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    public void Text_IsLongEnoughToRequireScrolling(string culture)
    {
        string text = LicenseAgreement.GetText(culture);

        Assert.True(text.Length > 1500, $"Текст лицензии слишком короткий для {culture}");
        Assert.Contains("Windows 10", text);
    }

    [Fact]
    public void UnknownCulture_FallsBackToEnglish()
    {
        Assert.Equal(
            LicenseAgreement.GetText("en-US"),
            LicenseAgreement.GetText("fr-FR"));
    }
}

/// <summary>
/// Проверки ресурсов локализации. Файлы читаются из исходников, поэтому тест
/// находит корень репозитория подъёмом по каталогам (тест может запускаться
/// из bin/Debug/net8.0 как в CI, так и локально из IDE).
/// </summary>
public class LocalizationResourceTests
{
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void RussianAndEnglishDictionaries_HaveIdenticalKeys()
    {
        Dictionary<string, string> ru = ReadStrings("Lang.ru.xaml");
        Dictionary<string, string> en = ReadStrings("Lang.en.xaml");

        Assert.NotEmpty(ru);
        Assert.Equal(ru.Keys.OrderBy(k => k, StringComparer.Ordinal),
                     en.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void NoEmptyStrings()
    {
        foreach (string file in new[] { "Lang.ru.xaml", "Lang.en.xaml" })
        {
            foreach ((string key, string value) in ReadStrings(file))
                Assert.False(string.IsNullOrWhiteSpace(value), $"{file}: пустая строка '{key}'");
        }
    }

    [Fact]
    public void EveryDynamicResourceInXaml_ExistsInDictionaries()
    {
        string appRoot = FindAppRoot();
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(appRoot, "*.xaml", SearchOption.AllDirectories))
        {
            XDocument document = XDocument.Load(file);
            foreach (XElement element in document.Descendants())
            {
                string? key = element.Attribute(XamlNs + "Key")?.Value;
                if (key is not null)
                    declared.Add(key);
            }
        }

        var missing = new List<string>();
        foreach (string file in Directory.EnumerateFiles(appRoot, "*.xaml", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (string key in ExtractKeys(text))
            {
                if (!declared.Contains(key))
                    missing.Add($"{Path.GetFileName(file)}: {key}");
            }
        }

        Assert.Empty(missing);
    }

    private static IEnumerable<string> ExtractKeys(string xaml)
    {
        const string marker = "Resource ";
        int index = 0;

        while ((index = xaml.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            int start = index + marker.Length;
            int end = start;
            while (end < xaml.Length && (char.IsLetterOrDigit(xaml[end]) || xaml[end] is '.' or '_'))
                end++;

            if (end > start && xaml[start] is 'L' or 'B' or 'T' or 'F' or 'C' or 'G' or 'S' or 'P')
                yield return xaml[start..end];

            index = end;
        }
    }

    private static Dictionary<string, string> ReadStrings(string fileName)
    {
        string path = Path.Combine(FindAppRoot(), "Themes", fileName);
        XDocument document = XDocument.Load(path);

        return document.Root!.Elements()
            .Where(e => e.Attribute(XamlNs + "Key") is not null)
            .ToDictionary(
                e => e.Attribute(XamlNs + "Key")!.Value,
                e => e.Value,
                StringComparer.Ordinal);
    }

    private static string FindAppRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Win10Sim.App");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не найден каталог src/Win10Sim.App — тест нужно запускать из репозитория.");
    }
}

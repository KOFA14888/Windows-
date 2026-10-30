using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Win10Sim.App.Infrastructure;
using Win10Sim.App.Shell;
using Win10Sim.Core.Persistence;

namespace Win10Sim.App.Apps.Edge;

/// <summary>
/// Microsoft Edge внутри симулятора. Настоящий браузерный движок — WebView2
/// (Chromium). Если на машине не установлен WebView2 Runtime, показывается
/// понятное объяснение, а не исключение: симулятор должен работать и без него.
///
/// Профиль пользователя (cookies, кэш) хранится в отдельной папке
/// %LocalAppData%\Win10Sim\WebView2 — реестр и профиль настоящего Edge не трогаем.
/// </summary>
public partial class EdgeView : UserControl, ISimApp
{
    private const string HomeUrl = "https://www.bing.com";

    private WebView2? _browser;
    private bool _initialized;
    private bool _initializationFailed;

    public EdgeView(AppServices services)
    {
        InitializeComponent();

        AddressBox.Text = HomeUrl;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void HandleArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return;

        AddressBox.Text = arguments.Trim();

        if (_browser is not null)
            Navigate(arguments.Trim());
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            string profileFolder = AppPaths.WebViewDataFolder;
            Directory.CreateDirectory(profileFolder);

            CoreWebView2Environment environment = await CoreWebView2Environment
                .CreateAsync(browserExecutableFolder: null, userDataFolder: profileFolder)
                .ConfigureAwait(true);

            _browser = new WebView2();
            _browser.CoreWebView2InitializationCompleted += OnCoreInitialized;
            _browser.NavigationStarting += (_, args) => SetLoading(true, args.Uri);
            _browser.NavigationCompleted += (_, args) => SetLoading(false, _browser.Source?.ToString());
            _browser.SourceChanged += (_, _) => SyncAddress();

            BrowserHost.Child = _browser;

            await _browser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

            if (_browser.CoreWebView2 is { } core)
            {
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsStatusBarEnabled = true;
                core.Settings.AreDevToolsEnabled = false;

                // Красим полосу прокрутки под тёмную тему симулятора не нужно:
                // страницы рисует Chromium, они должны выглядеть как настоящие.
                core.Navigate(AddressBox.Text);
            }
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or DllNotFoundException or InvalidOperationException or IOException)
        {
            _initializationFailed = true;
            ShowFallback(ex);
        }
    }

    private void OnCoreInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess)
            return;

        _initializationFailed = true;
        ShowFallback(e.InitializationException);
    }

    private void ShowFallback(Exception? exception)
    {
        LoadingBar.Visibility = Visibility.Collapsed;
        BrowserHost.Child = null;
        FallbackPanel.Visibility = Visibility.Visible;

        if (exception is not null)
            FallbackDetail.Text = exception.GetType().Name + ": " + exception.Message;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Окно закрыли — освобождаем движок, иначе процесс msedgewebview2.exe
        // останется в памяти до выхода из симулятора.
        if (_browser is null)
            return;

        try
        {
            _browser.Dispose();
        }
        catch (InvalidOperationException)
        {
        }

        _browser = null;
        _initialized = false;
    }

    private void Navigate(string input)
    {
        string url = NormalizeUrl(input);

        AddressBox.Text = url;

        if (_browser?.CoreWebView2 is { } core)
            core.Navigate(url);
    }

    /// <summary>
    /// «виндовс 10» → поиск Bing, «example.com» → https://example.com,
    /// «https://…» → как есть. Так же ведёт себя адресная строка Edge.
    /// </summary>
    private static string NormalizeUrl(string input)
    {
        string text = input.Trim();

        if (text.Length == 0)
            return HomeUrl;

        if (text.Contains("://", StringComparison.Ordinal))
            return text;

        // Похоже на домен: есть точка и нет пробелов.
        bool looksLikeHost = !text.Contains(' ', StringComparison.Ordinal)
                             && text.Contains('.', StringComparison.Ordinal)
                             && !text.EndsWith(".", StringComparison.Ordinal);

        return looksLikeHost
            ? "https://" + text
            : "https://www.bing.com/search?q=" + Uri.EscapeDataString(text);
    }

    private void SyncAddress()
    {
        if (_browser?.Source is { } source)
            AddressBox.Text = source.ToString();
    }

    private void SetLoading(bool loading, string? url)
    {
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

        if (!string.IsNullOrEmpty(url))
            AddressBox.Text = url;

        if (_browser is not null)
        {
            BackButton.IsEnabled = _browser.CoreWebView2?.CanGoBack ?? false;
            ForwardButton.IsEnabled = _browser.CoreWebView2?.CanGoForward ?? false;
        }
    }

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        Go();
        e.Handled = true;
    }

    private void Go()
    {
        if (_browser is null)
        {
            // Движок ещё не готов или недоступен — запоминаем адрес,
            // его откроет OnLoaded, когда WebView2 создастся.
            AddressBox.Text = NormalizeUrl(AddressBox.Text);

            if (_initializationFailed)
                FallbackPanel.Visibility = Visibility.Visible;

            return;
        }

        Navigate(AddressBox.Text);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_browser?.CoreWebView2?.CanGoBack == true)
            _browser.CoreWebView2.GoBack();
    }

    private void OnForwardClick(object sender, RoutedEventArgs e)
    {
        if (_browser?.CoreWebView2?.CanGoForward == true)
            _browser.CoreWebView2.GoForward();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _browser?.CoreWebView2?.Reload();

    private void OnNewTabClick(object sender, RoutedEventArgs e) => Navigate(HomeUrl);
}

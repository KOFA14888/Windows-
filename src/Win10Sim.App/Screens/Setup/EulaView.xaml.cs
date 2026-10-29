using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Лицензионное соглашение. Флажок становится доступен только после того,
/// как текст прокручен до конца — это поведение настоящего установщика
/// и одновременно защита от «прощёлкал не читая».
/// </summary>
public partial class EulaView : SetupScreenBase
{
    private bool _scrolledToEnd;

    public EulaView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        EulaText.Text = LicenseAgreement.GetText(Services.Localization.Culture);
        ScrollHint.Text = UiStrings.Get("L.Eula.ScrollHint");

        // Текст может быть короче окна (большой монитор) — тогда прокручивать нечего.
        // Проверяем после того, как шаблон ScrollViewer применён и метрики посчитаны.
        Loaded += (_, _) => Dispatcher.BeginInvoke(
            new Action(EnsureScrolledToEnd),
            DispatcherPriority.Loaded);
    }

    /// <summary>Назад: к ключу, если редакция пришла из ключа, иначе — к выбору редакции.</summary>
    protected override OsPhase? BackTarget => Session.ProductKey is null ? OsPhase.SetupEdition : OsPhase.SetupKey;

    private void OnEulaScrollChanged(object sender, ScrollChangedEventArgs e) => EnsureScrolledToEnd();

    private void EnsureScrolledToEnd()
    {
        if (_scrolledToEnd)
            return;

        // Допуск в 1 px: дробные значения прокрутки на 125/150 % DPI.
        if (EulaScroll.ScrollableHeight <= 0 ||
            EulaScroll.VerticalOffset >= EulaScroll.ScrollableHeight - 1)
        {
            _scrolledToEnd = true;
            AcceptCheck.IsEnabled = true;
            ScrollHint.Text = UiStrings.Get("L.Eula.Ready");
        }
    }

    private void OnAcceptChanged(object sender, RoutedEventArgs e)
    {
        NextButton.IsEnabled = AcceptCheck.IsChecked == true;
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (AcceptCheck.IsChecked != true)
            return;

        Session.EulaAccepted = true;
        Services.SaveState();
        Advance(OsPhase.SetupInstallType);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        Session.EulaAccepted = false;
        Advance(BackTarget ?? OsPhase.SetupKey);
    }
}

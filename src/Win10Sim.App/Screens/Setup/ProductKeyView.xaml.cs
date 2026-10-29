using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Ввод ключа продукта.
///
/// Поведение как в настоящем setup:
///   * ввод автоматически разбивается на пять групп по пять символов;
///   * недопустимые символы не появляются в поле вообще;
///   * «У меня нет ключа продукта» ведёт на ручной выбор редакции;
///   * известный generic-ключ задаёт редакцию и пропускает выбор редакции.
/// </summary>
public partial class ProductKeyView : SetupScreenBase
{
    private bool _formatting;

    public ProductKeyView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        if (Session.ProductKey is { Length: > 0 } savedKey)
            ProductKeyBox.Text = ProductKeyValidator.Normalize(savedKey);

        ProductKeyBox.Focus();
        ProductKeyBox.CaretIndex = ProductKeyBox.Text.Length;
    }

    protected override OsPhase? BackTarget => OsPhase.SetupInstall;

    /// <summary>
    /// Фокус в поле ввода сразу после появления экрана: пользователь должен иметь
    /// возможность печатать ключ, не кликая предварительно мышью.
    /// </summary>
    public override Task OnEnterAsync(CancellationToken cancellationToken)
    {
        ProductKeyBox.Focus();
        Keyboard.Focus(ProductKeyBox);
        return Task.CompletedTask;
    }

    private void OnKeyTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_formatting)
            return;

        _formatting = true;
        try
        {
            string formatted = ProductKeyValidator.FormatAsYouType(ProductKeyBox.Text);
            if (!string.Equals(formatted, ProductKeyBox.Text, StringComparison.Ordinal))
            {
                ProductKeyBox.Text = formatted;
                ProductKeyBox.CaretIndex = formatted.Length;
            }

            ErrorText.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _formatting = false;
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        ProductKeyValidation result = ProductKeyValidator.Validate(ProductKeyBox.Text);
        if (!result.IsValid)
        {
            ShowError(result.Error);
            return;
        }

        Session.ProductKey = result.NormalizedKey;
        Session.Edition = result.Edition;
        Services.SaveState();

        // Редакция известна из ключа — экран выбора редакции пропускается,
        // ровно как в настоящей установке с корректным ключом.
        Advance(OsPhase.SetupEula);
    }

    private void OnNoKeyClick(object sender, RoutedEventArgs e)
    {
        Session.ProductKey = null;
        Services.SaveState();
        Advance(OsPhase.SetupEdition);
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => Advance(OsPhase.SetupInstall);

    private void ShowError(ProductKeyError error)
    {
        string key = error switch
        {
            ProductKeyError.Empty => "L.Key.Error.Empty",
            ProductKeyError.WrongLength => "L.Key.Error.WrongLength",
            ProductKeyError.InvalidCharacters => "L.Key.Error.InvalidCharacters",
            _ => "L.Key.Error.InvalidKey"
        };

        ErrorText.Text = UiStrings.Get(key);
        ErrorText.Visibility = Visibility.Visible;
        ProductKeyBox.Focus();
    }
}

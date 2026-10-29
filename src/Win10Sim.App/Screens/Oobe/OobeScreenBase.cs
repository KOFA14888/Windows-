using System.Windows;
using System.Windows.Controls;
using Win10Sim.App.Infrastructure;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// База экранов OOBE. Кроме общих сервисов даёт две вещи, одинаковые для всех
/// экранов первой настройки: показ ошибки под полем ввода и ссылку
/// «Специальные возможности» в левом нижнем углу (она есть на каждом экране
/// настоящей Windows, здесь — декоративная).
/// </summary>
public abstract class OobeScreenBase : SetupScreenBase
{
    protected OobeScreenBase(AppServices services)
        : base(services)
    {
    }

    /// <summary>Показать локализованную ошибку в переданном TextBlock.</summary>
    protected static void ShowError(TextBlock target, string resourceKey)
    {
        target.Text = UiStrings.Get(resourceKey);
        target.Visibility = Visibility.Visible;
    }

    protected static void HideError(TextBlock target)
    {
        target.Visibility = Visibility.Collapsed;
    }

    protected static string KeyFor(Core.Accounts.AccountValidationError error) => error switch
    {
        Core.Accounts.AccountValidationError.Empty => "L.Oobe.Account.Error.Empty",
        Core.Accounts.AccountValidationError.TooLong => "L.Oobe.Account.Error.TooLong",
        Core.Accounts.AccountValidationError.InvalidCharacters => "L.Oobe.Account.Error.InvalidCharacters",
        Core.Accounts.AccountValidationError.EndsWithDotOrSpace => "L.Oobe.Account.Error.EndsWithDotOrSpace",
        Core.Accounts.AccountValidationError.ReservedName => "L.Oobe.Account.Error.ReservedName",
        Core.Accounts.AccountValidationError.AllDots => "L.Oobe.Account.Error.AllDots",
        Core.Accounts.AccountValidationError.PasswordTooShort => "L.Oobe.Password.Error.Short",
        Core.Accounts.AccountValidationError.PasswordMismatch => "L.Oobe.Password.Error.Mismatch",
        Core.Accounts.AccountValidationError.PasswordHintTooLong => "L.Oobe.Password.Error.HintTooLong",
        _ => "L.Oobe.Account.Error.Empty"
    };
}

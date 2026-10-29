using System.Windows;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Accounts;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// «Создайте пароль». Пароль превращается в PBKDF2-хэш сразу и в открытом виде
/// не покидает этот метод; в state.json попадут только соль, итерации и хэш.
///
/// PBKDF2 с 600 000 итераций занимает ~0.3–0.5 с, поэтому хэширование выполняется
/// в фоне (Task.Run) — UI не должен подвисать на нажатии «Далее».
/// </summary>
public partial class OobePasswordView : OobeScreenBase
{
    private bool _busy;

    public OobePasswordView(AppServices services)
        : base(services)
    {
        InitializeComponent();
    }

    protected override OsPhase? BackTarget => OsPhase.OobeAccount;

    // Пока считаем хэш, уходить с экрана нельзя.
    protected override bool CanNavigateBack => !_busy;

    public override Task OnEnterAsync(CancellationToken cancellationToken)
    {
        PasswordInput.Focus();
        return Task.CompletedTask;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => HideError(ErrorText);

    private async void OnNextClick(object sender, RoutedEventArgs e)
    {
        await CompleteAsync(PasswordInput.Password, ConfirmInput.Password, HintBox.Text)
            .ConfigureAwait(true);
    }

    private async void OnSkipClick(object sender, RoutedEventArgs e)
    {
        HideError(ErrorText);
        await CompleteAsync(string.Empty, string.Empty, string.Empty).ConfigureAwait(true);
    }

    private async Task CompleteAsync(string password, string confirmation, string hint)
    {
        if (_busy)
            return;

        AccountValidation passwordValidation = AccountValidator.ValidatePassword(password, confirmation);
        if (!passwordValidation.IsValid)
        {
            ShowError(ErrorText, KeyFor(passwordValidation.Error));
            PasswordInput.Focus();
            return;
        }

        AccountValidation hintValidation = AccountValidator.ValidateHint(hint);
        if (!hintValidation.IsValid)
        {
            ShowError(ErrorText, KeyFor(hintValidation.Error));
            HintBox.Focus();
            return;
        }

        _busy = true;
        try
        {
            var account = new UserAccount
            {
                UserName = Session.UserName ?? "User",
                DisplayName = Session.UserDisplayName ?? Session.UserName ?? "User"
            };

            // Хэширование — на пуле потоков: интерфейс остаётся отзывчивым.
            await Task.Run(() =>
            {
                account.SetPassword(password);

                if (!string.IsNullOrWhiteSpace(hint))
                    account.SetSecurityAnswer("Password hint", hint);
            }).ConfigureAwait(true);

            Session.Account = account;
            Services.SaveState();
        }
        finally
        {
            _busy = false;
        }

        Advance(OsPhase.OobeHello);
    }
}

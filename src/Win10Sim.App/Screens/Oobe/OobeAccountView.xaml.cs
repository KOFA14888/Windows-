using System.Windows;
using System.Windows.Controls;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Accounts;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// «Кто будет использовать этот компьютер?» — имя учётной записи.
///
/// Проверка идёт по правилам локальных учётных записей Windows: запрещённые
/// символы, 20 символов, точка/пробел в конце, зарезервированные имена (CON, NUL,
/// COM1…). Ошибка показывается сразу при вводе, а не только по кнопке.
/// </summary>
public partial class OobeAccountView : OobeScreenBase
{
    public OobeAccountView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        if (Session.UserName is { Length: > 0 } savedName)
            NameBox.Text = savedName;
    }

    protected override OsPhase? BackTarget => OsPhase.OobeKeyboard;

    public override Task OnEnterAsync(CancellationToken cancellationToken)
    {
        NameBox.Focus();
        NameBox.CaretIndex = NameBox.Text.Length;
        return Task.CompletedTask;
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        AccountValidation validation = AccountValidator.ValidateUserName(NameBox.Text);

        if (validation.IsValid || string.IsNullOrEmpty(NameBox.Text))
            HideError(ErrorText);
        else
            ShowError(ErrorText, KeyFor(validation.Error));
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        AccountValidation validation = AccountValidator.ValidateUserName(NameBox.Text);
        if (!validation.IsValid)
        {
            ShowError(ErrorText, KeyFor(validation.Error));
            NameBox.Focus();
            return;
        }

        Session.UserName = validation.Preview;
        Session.UserDisplayName = validation.Preview;
        Services.SaveState();

        Advance(OsPhase.OobePassword);
    }
}

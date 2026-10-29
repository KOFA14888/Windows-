using System.Windows;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Accounts;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Oobe;

/// <summary>
/// Экран-итог ЭТАПА 2: показывает, что сохранено, и позволяет начать заново
/// или выйти. Здесь же видно, что пароль в состоянии — только хэш.
/// </summary>
public partial class OobeCompleteView : OobeScreenBase
{
    public OobeCompleteView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        UserAccount? account = Session.Account;
        string passwordState = account is { HasPassword: true }
            ? UiStrings.Format("L.Oobe.Done.PasswordSet", account.PasswordIterations)
            : UiStrings.Get("L.Oobe.Done.NoPassword");

        AccountText.Text = UiStrings.Format(
            "L.Oobe.Done.Account",
            account?.DisplayName ?? Session.UserName ?? "—",
            passwordState);

        RegionText.Text = UiStrings.Format(
            "L.Oobe.Done.Region",
            Session.RegionName ?? "—",
            Session.RegionCode ?? "—");

        string primary = KeyboardCatalog.Find(Session.PrimaryKeyboard ?? "en-US").DisplayName;
        string secondary = Session.SecondaryKeyboard is { Length: > 0 } second
            ? " + " + KeyboardCatalog.Find(second).DisplayName
            : string.Empty;

        KeyboardText.Text = UiStrings.Format("L.Oobe.Done.Keyboard", primary + secondary);
    }

    private void OnRestartClick(object sender, RoutedEventArgs e) => Advance(OsPhase.Firmware);

    private void OnExitClick(object sender, RoutedEventArgs e) => Services.Host.RequestShutdown();
}

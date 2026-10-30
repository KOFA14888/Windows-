using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Accounts;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Shell;

/// <summary>
/// Экран блокировки. Два состояния, как в Windows: «обложка» (часы, дата,
/// «нажмите любую клавишу») и панель входа с паролем.
///
/// Пароль проверяется через <see cref="UserAccount.CheckPassword"/> — тот же
/// PBKDF2-хэш, что сохранён в OOBE. Если пароля нет, показывается кнопка входа
/// без пароля.
/// </summary>
public partial class LockScreenView : SetupScreenBase
{
    private readonly DispatcherTimer _clock;
    private bool _signInVisible;
    private bool _unlocking;

    public LockScreenView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        _clock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();

        Loaded += OnLoaded;
        Unloaded += (_, _) => _clock.Stop();

        PreviewMouseLeftButtonUp += OnAnyInput;
        PreviewKeyDown += OnAnyKey;

        UpdateClock();
        UpdateUserInfo();
    }

    protected override OsPhase? BackTarget => null;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _clock.Start();
        UpdateClock();
        Focus();

        // Если пароля нет — сразу показываем панель входа: угадывать,
        // что нужно нажать любую клавишу, незачем.
        if (Services.Session.Account is { HasPassword: false })
            ShowSignIn();
    }

    private void UpdateClock()
    {
        DateTime now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);

        // «суббота, 30 октября» — как на настоящем экране блокировки: день недели и дата.
        string text = now.ToString("dddd, d MMMM", CultureInfo.CurrentCulture);
        DateText.Text = char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
    }

    private void UpdateUserInfo()
    {
        string displayName = Services.DisplayName;
        string initial = displayName.Length > 0 ? displayName[..1].ToUpperInvariant() : "?";

        UserNameText.Text = displayName;
        UserInitial.Text = initial;
        WelcomeInitial.Text = initial;

        UserAccount? account = Services.Session.Account;

        if (account is { HasPassword: false })
        {
            NoPasswordButton.Visibility = Visibility.Visible;
            PasswordInput.Visibility = Visibility.Collapsed;
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrWhiteSpace(account?.SecurityQuestion))
        {
            HintText.Text = UiStrings.Get("L.Lock.HintLabel") + ": " + account!.SecurityQuestion;
            HintText.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Любой клик по «обложке» открывает панель входа.</summary>
    private void OnAnyInput(object sender, MouseButtonEventArgs e)
    {
        if (_unlocking)
            return;

        if (!_signInVisible)
        {
            ShowSignIn();
            e.Handled = true;
        }
    }

    private void OnAnyKey(object sender, KeyEventArgs e)
    {
        if (_unlocking)
            return;

        // Insert обрабатывается глобальным хуком выше — сюда не доходит.
        if (!_signInVisible)
        {
            ShowSignIn();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            HideSignIn();
            e.Handled = true;
        }
    }

    private void ShowSignIn()
    {
        _signInVisible = true;
        UnlockHint.Visibility = Visibility.Collapsed;
        SignInPanel.Visibility = Visibility.Visible;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
        SignInPanel.BeginAnimation(OpacityProperty, fade);

        if (PasswordInput.IsVisible)
        {
            PasswordInput.Focus();
            Keyboard.Focus(PasswordInput);
        }
    }

    private void HideSignIn()
    {
        _signInVisible = false;
        UnlockHint.Visibility = Visibility.Visible;
        SignInPanel.Visibility = Visibility.Collapsed;
        ErrorText.Visibility = Visibility.Collapsed;
        PasswordInput.Clear();
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TrySignIn();
            e.Handled = true;
        }
    }

    private void OnSignInClick(object sender, RoutedEventArgs e) => TrySignIn();

    private void TrySignIn()
    {
        if (_unlocking)
            return;

        UserAccount? account = Services.Session.Account;

        // Учётной записи нет (установка не дошла до OOBE) — входить некуда, пропускаем.
        if (account is null)
        {
            Unlock();
            return;
        }

        if (!account.HasPassword)
        {
            Unlock();
            return;
        }

        if (!account.CheckPassword(PasswordInput.Password))
        {
            ShowWrongPassword();
            return;
        }

        Unlock();
    }

    private void ShowWrongPassword()
    {
        ErrorText.Text = UiStrings.Get("L.Lock.WrongPassword");
        ErrorText.Visibility = Visibility.Visible;

        // «Тряска» поля — как в Windows при неверном пароле.
        var shake = new DoubleAnimationUsingKeyFrames();
        for (int i = 0; i < 6; i++)
        {
            double offset = (i % 2 == 0 ? 8 : -8) * (1.0 - i / 6.0);
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(offset, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50 * (i + 1)))));
        }

        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360))));

        SignInPanel.RenderTransform = new System.Windows.Media.TranslateTransform();
        ((System.Windows.Media.TranslateTransform)SignInPanel.RenderTransform)
            .BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, shake);

        PasswordInput.Clear();
        PasswordInput.Focus();
    }

    private async void Unlock()
    {
        if (_unlocking)
            return;

        _unlocking = true;
        ErrorText.Visibility = Visibility.Collapsed;

        WelcomePanel.Visibility = Visibility.Visible;
        WelcomeText.Text = UiStrings.Get("L.Lock.Unlocking") + ", " + Services.DisplayName;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        WelcomePanel.BeginAnimation(OpacityProperty, fade);

        await Task.Delay(900);

        _clock.Stop();
        Advance(OsPhase.Desktop);
    }
}

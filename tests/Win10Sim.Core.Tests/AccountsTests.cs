using Win10Sim.Core.Accounts;
using Win10Sim.Core.Persistence;
using Win10Sim.Core.Setup;
using Xunit;

namespace Win10Sim.Core.Tests;

public class PasswordHasherTests
{
    // В тестах берём минимально допустимое число итераций: 600 000 (~0.3 с)
    // на каждый хэш превратили бы прогон в десятки секунд.
    private const int TestIterations = 1000;

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        PasswordHasher.HashResult result = PasswordHasher.Hash("Qwerty123", TestIterations);

        Assert.True(PasswordHasher.Verify("Qwerty123", result.HashBase64, result.SaltBase64, result.Iterations));
    }

    [Theory]
    [InlineData("qwerty123")]
    [InlineData("Qwerty123 ")]
    [InlineData("")]
    public void Verify_WrongPassword_Fails(string candidate)
    {
        PasswordHasher.HashResult result = PasswordHasher.Hash("Qwerty123", TestIterations);

        Assert.False(PasswordHasher.Verify(candidate, result.HashBase64, result.SaltBase64, result.Iterations));
    }

    [Fact]
    public void Hash_UsesRandomSalt()
    {
        PasswordHasher.HashResult first = PasswordHasher.Hash("same-password", TestIterations);
        PasswordHasher.HashResult second = PasswordHasher.Hash("same-password", TestIterations);

        Assert.NotEqual(first.SaltBase64, second.SaltBase64);
        Assert.NotEqual(first.HashBase64, second.HashBase64);
        Assert.True(PasswordHasher.Verify("same-password", first.HashBase64, first.SaltBase64, first.Iterations));
        Assert.True(PasswordHasher.Verify("same-password", second.HashBase64, second.SaltBase64, second.Iterations));
    }

    [Fact]
    public void Default_Iterations_AreStrong()
    {
        Assert.True(PasswordHasher.DefaultIterations >= 600_000);
    }

    [Fact]
    public void Verify_CorruptedStorage_ReturnsFalseInsteadOfThrowing()
    {
        Assert.False(PasswordHasher.Verify("x", "not-base64!", "eA==", 1000));
        Assert.False(PasswordHasher.Verify("x", string.Empty, string.Empty, 1000));
        Assert.False(PasswordHasher.Verify("x", "eA==", "eA==", 0));
    }

    [Fact]
    public void Hash_RejectsTinyIterationCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordHasher.Hash("x", 10));
    }
}

public class UserAccountTests
{
    [Fact]
    public void SetPassword_StoresOnlyHashAndSupportsCheck()
    {
        var account = new UserAccount { UserName = "user", DisplayName = "user" };
        account.SetPassword("Secret123");

        Assert.True(account.HasPassword);
        Assert.NotEqual("Secret123", account.PasswordHash);
        Assert.DoesNotContain("Secret123", account.PasswordHash);
        Assert.True(account.CheckPassword("Secret123"));
        Assert.False(account.CheckPassword("secret123"));
    }

    [Fact]
    public void EmptyPassword_MeansNoPasswordSignIn()
    {
        var account = new UserAccount();
        account.SetPassword(string.Empty);

        Assert.False(account.HasPassword);
        Assert.False(account.CheckPassword(string.Empty));
        Assert.Equal(0, account.PasswordIterations);
    }

    [Fact]
    public void SecurityAnswer_IgnoresCaseAndSpaces()
    {
        var account = new UserAccount();
        account.SetSecurityAnswer("Password hint", "  Мурка ");

        Assert.True(account.CheckSecurityAnswer("мурка"));
        Assert.True(account.CheckSecurityAnswer("МУРКА"));
        Assert.False(account.CheckSecurityAnswer("Шарик"));
        Assert.Equal("Password hint", account.SecurityQuestion);
        Assert.NotNull(account.SecurityAnswerHash);
        Assert.DoesNotContain("мурка", account.SecurityAnswerHash!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfilePath_FollowsUserName()
    {
        var account = new UserAccount { UserName = "ivan" };

        Assert.Equal(@"C:\Users\ivan", account.ProfilePath);
    }
}

public class AccountValidatorTests
{
    [Theory]
    [InlineData("ivan")]
    [InlineData("Ivan Petrov")]
    [InlineData("user-01")]
    [InlineData("админ")]
    [InlineData("a.b")]
    public void ValidNames_AreAccepted(string name)
    {
        AccountValidation result = AccountValidator.ValidateUserName(name);

        Assert.True(result.IsValid, $"{name}: {result.Error}");
    }

    [Theory]
    [InlineData("", AccountValidationError.Empty)]
    [InlineData("   ", AccountValidationError.Empty)]
    [InlineData("ThisNameIsWayTooLong", AccountValidationError.TooLong)]
    [InlineData("a/b", AccountValidationError.InvalidCharacters)]
    [InlineData("a:b", AccountValidationError.InvalidCharacters)]
    [InlineData("a@b", AccountValidationError.InvalidCharacters)]
    [InlineData("ivan.", AccountValidationError.EndsWithDotOrSpace)]
    [InlineData("ivan ", AccountValidationError.EndsWithDotOrSpace)]
    [InlineData("...", AccountValidationError.AllDots)]
    [InlineData("CON", AccountValidationError.ReservedName)]
    [InlineData("nul", AccountValidationError.ReservedName)]
    [InlineData("COM1", AccountValidationError.ReservedName)]
    [InlineData("LPT9", AccountValidationError.ReservedName)]
    [InlineData("CON.txt", AccountValidationError.ReservedName)]
    public void InvalidNames_AreRejectedWithSpecificError(string name, AccountValidationError expected)
    {
        AccountValidation result = AccountValidator.ValidateUserName(name);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void Name_IsTrimmedInPreview()
    {
        AccountValidation result = AccountValidator.ValidateUserName("  ivan  ");

        Assert.True(result.IsValid);
        Assert.Equal("ivan", result.Preview);
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("abcd", "abcd", true)]
    [InlineData("abc", "abc", false)]
    [InlineData("abcd", "abce", false)]
    public void PasswordRules(string password, string confirmation, bool expectedValid)
    {
        AccountValidation result = AccountValidator.ValidatePassword(password, confirmation);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void EmptyPassword_CanBeForbidden()
    {
        AccountValidation result = AccountValidator.ValidatePassword(string.Empty, string.Empty, allowEmpty: false);

        Assert.False(result.IsValid);
        Assert.Equal(AccountValidationError.PasswordTooShort, result.Error);
    }

    [Fact]
    public void Hint_HasLengthLimit()
    {
        Assert.True(AccountValidator.ValidateHint(new string('a', 60)).IsValid);
        Assert.False(AccountValidator.ValidateHint(new string('a', 61)).IsValid);
    }
}

public class OobeCatalogTests
{
    [Fact]
    public void RegionCatalog_IsNotEmptyAndHasUniqueCodes()
    {
        Assert.NotEmpty(RegionCatalog.All);
        Assert.Equal(
            RegionCatalog.All.Count,
            RegionCatalog.All.Select(r => r.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("ru-RU", "BY")]
    [InlineData("en-US", "US")]
    [InlineData("de-DE", "DE")]
    public void DefaultRegion_FollowsInstallLanguage(string language, string expectedCode)
    {
        Assert.Equal(expectedCode, RegionCatalog.DefaultFor(language).Code);
    }

    [Fact]
    public void UnknownRegion_FallsBackInsteadOfThrowing()
    {
        Assert.NotNull(RegionCatalog.Find("ZZ"));
        Assert.NotNull(KeyboardCatalog.Find("xx-XX"));
    }

    [Fact]
    public void KeyboardCatalog_ContainsLayoutForEverySupportedLanguage()
    {
        foreach (string language in new[] { "ru-RU", "en-US", "de-DE", "fr-FR" })
        {
            KeyboardInfo keyboard = KeyboardCatalog.DefaultFor(language);
            Assert.Equal(language, keyboard.Code);
        }
    }
}

[Collection(SetupTimingsCollection.Name)]
public class OobePreparerTests : IDisposable
{
    private readonly double _originalFactor;

    public OobePreparerTests()
    {
        _originalFactor = SetupTimings.SpeedFactor;
        SetupTimings.SpeedFactor = 0.01;
    }

    public void Dispose() => SetupTimings.SpeedFactor = _originalFactor;

    [Fact]
    public async Task Prepare_ReportsMonotonicProgressAndAllSteps()
    {
        var reports = new List<PreparingProgress>();

        await OobePreparer.PrepareAsync(new SyncProgress<PreparingProgress>(reports.Add), CancellationToken.None);

        Assert.True(reports.Count > 10);
        for (int i = 1; i < reports.Count; i++)
            Assert.True(reports[i].Percent >= reports[i - 1].Percent, "прогресс не должен идти назад");

        Assert.Equal(100, reports[^1].Percent);
        Assert.True(reports[^1].IsDone);
        Assert.Equal(Enum.GetValues<PreparingStep>().Length, reports.Select(r => r.Step).Distinct().Count());
    }

    [Fact]
    public async Task Prepare_IsCancellable()
    {
        using var cts = new CancellationTokenSource();
        Task preparing = OobePreparer.PrepareAsync(null, cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparing);
    }

    /// <summary>Синхронный IProgress: порядок доставки гарантирован (см. SetupInstallerTests).</summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public SyncProgress(Action<T> callback) => _callback = callback;

        public void Report(T value) => _callback(value);
    }
}

public class OobeSessionTests
{
    [Fact]
    public void Account_SurvivesSaveAndLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), $"win10sim-oobe-{Guid.NewGuid():N}.json");
        try
        {
            var account = new UserAccount { UserName = "ivan", DisplayName = "Ivan" };
            account.SetPassword("Secret123");

            var session = new SetupSession
            {
                Phase = OsPhase.LockScreen,
                RegionCode = "BY",
                RegionName = "Беларусь",
                PrimaryKeyboard = "ru-RU",
                SecondaryKeyboard = "en-US",
                UserName = "ivan",
                Account = account,
                OobeCompletedUtc = DateTime.UtcNow
            };

            var store = new JsonStateStore(path);
            store.Save(session);
            SetupSession loaded = new JsonStateStore(path).Load();

            Assert.NotNull(loaded.Account);
            Assert.Equal("ivan", loaded.Account!.UserName);
            Assert.Equal("BY", loaded.RegionCode);
            Assert.Equal("ru-RU", loaded.PrimaryKeyboard);
            Assert.NotNull(loaded.OobeCompletedUtc);

            // Главное: после перезапуска пароль всё ещё проверяется.
            Assert.True(loaded.Account.CheckPassword("Secret123"));
            Assert.False(loaded.Account.CheckPassword("wrong"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PlainPassword_IsNotWrittenToStateFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"win10sim-secret-{Guid.NewGuid():N}.json");
        try
        {
            var account = new UserAccount { UserName = "ivan" };
            account.SetPassword("SuperSecret42");

            var store = new JsonStateStore(path);
            store.Save(new SetupSession { Account = account });

            string json = File.ReadAllText(path);

            Assert.DoesNotContain("SuperSecret42", json);
            Assert.Contains("PasswordHash", json);
            Assert.Contains("PasswordSalt", json);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

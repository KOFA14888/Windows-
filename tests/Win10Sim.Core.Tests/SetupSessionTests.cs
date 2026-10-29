using Win10Sim.Core.Persistence;
using Win10Sim.Core.Setup;
using Xunit;

namespace Win10Sim.Core.Tests;

public class SetupSessionTests
{
    [Fact]
    public void StateStore_RoundTripsAllFields()
    {
        string path = Path.Combine(Path.GetTempPath(), $"win10sim-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonStateStore(path);
            var session = new SetupSession
            {
                Phase = OsPhase.SetupCopying,
                InstallLanguage = "en-US",
                TimeAndCurrencyFormat = "English (United States)",
                KeyboardLayout = "US",
                ProductKey = "VK7JG-NPHTM-C97JM-9MPGT-3V66T",
                Edition = WindowsEdition.Enterprise,
                EulaAccepted = true,
                InstallType = InstallType.Custom,
                SelectedDiskIndex = 0,
                SelectedPartitionIndex = 3,
                DiskFormatted = true,
                FormattedPartition = "0:3",
                InstallProgress = 42.5,
                LastStage = InstallStage.InstallingComponents
            };

            store.Save(session);
            SetupSession loaded = new JsonStateStore(path).Load();

            Assert.Equal(OsPhase.SetupCopying, loaded.Phase);
            Assert.Equal("en-US", loaded.InstallLanguage);
            Assert.Equal(WindowsEdition.Enterprise, loaded.Edition);
            Assert.True(loaded.EulaAccepted);
            Assert.Equal(InstallType.Custom, loaded.InstallType);
            Assert.Equal(3, loaded.SelectedPartitionIndex);
            Assert.True(loaded.DiskFormatted);
            Assert.Equal("0:3", loaded.FormattedPartition);
            Assert.Equal(42.5, loaded.InstallProgress);
            Assert.Equal(InstallStage.InstallingComponents, loaded.LastStage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StateFile_IsHumanReadable_WithEnumNames()
    {
        string path = Path.Combine(Path.GetTempPath(), $"win10sim-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonStateStore(path);
            store.Save(new SetupSession { Phase = OsPhase.SetupEula, Edition = WindowsEdition.Home });

            string json = File.ReadAllText(path);

            Assert.Contains("\"Phase\": \"SetupEula\"", json);
            Assert.Contains("\"Edition\": \"Home\"", json);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsFreshSession()
    {
        string path = Path.Combine(Path.GetTempPath(), $"win10sim-missing-{Guid.NewGuid():N}.json");

        SetupSession session = new JsonStateStore(path).Load();

        Assert.Equal(OsPhase.Firmware, session.Phase);
        Assert.Null(session.ProductKey);
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsFreshSessionAndKeepsBackup()
    {
        string path = Path.Combine(Path.GetTempPath(), $"win10sim-broken-{Guid.NewGuid():N}.json");
        string backup = path + ".corrupted";

        try
        {
            File.WriteAllText(path, "{ this is not json");

            SetupSession session = new JsonStateStore(path).Load();

            Assert.Equal(OsPhase.Firmware, session.Phase);
            Assert.True(File.Exists(backup));
        }
        finally
        {
            File.Delete(path);
            File.Delete(backup);
        }
    }

    [Fact]
    public void ResetForNewInstall_ClearsInstallDecisionsButKeepsLanguage()
    {
        var session = new SetupSession
        {
            Phase = OsPhase.OobeWait,
            InstallLanguage = "en-US",
            ProductKey = "VK7JG-NPHTM-C97JM-9MPGT-3V66T",
            EulaAccepted = true,
            DiskFormatted = true,
            InstallProgress = 100
        };

        session.ResetForNewInstall();

        Assert.Equal(OsPhase.Firmware, session.Phase);
        Assert.Null(session.ProductKey);
        Assert.False(session.EulaAccepted);
        Assert.False(session.DiskFormatted);
        Assert.Equal(0, session.InstallProgress);
        Assert.Equal("en-US", session.InstallLanguage);   // язык носителя остаётся
    }

    [Fact]
    public void UiCulture_FollowsInstallLanguage()
    {
        Assert.Equal("ru", new SetupSession { InstallLanguage = "ru-RU" }.UiCulture);
        Assert.Equal("en", new SetupSession { InstallLanguage = "en-US" }.UiCulture);
    }

    [Theory]
    [InlineData(OsPhase.SetupKey, true, false, false)]
    [InlineData(OsPhase.Reboot, true, false, false)]
    [InlineData(OsPhase.OobeAccount, false, true, false)]
    [InlineData(OsPhase.LockScreen, false, false, true)]
    [InlineData(OsPhase.Desktop, false, false, true)]
    public void PhaseRanges_AreClassifiedCorrectly(OsPhase phase, bool setup, bool oobe, bool shell)
    {
        Assert.Equal(setup, phase.IsSetupPhase());
        Assert.Equal(oobe, phase.IsOobePhase());
        Assert.Equal(shell, phase.IsShellPhase());
    }
}

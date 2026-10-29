using System.Text.Json.Serialization;
using Win10Sim.Core.Accounts;

namespace Win10Sim.Core.Setup;

/// <summary>
/// Всё состояние симулятора, которое должно пережить перезапуск процесса.
/// Именно этот объект сериализуется в state.json, поэтому:
///   * только простые типы и списки;
///   * никаких ссылок на сервисы и UI;
///   * enum-ы пишутся строками (см. JsonStateStore) — файл читаем глазами.
/// </summary>
public sealed class SetupSession
{
    /// <summary>Текущая фаза. Единственный источник истины для BootFlow.</summary>
    public OsPhase Phase { get; set; } = OsPhase.Firmware;

    // ---------- Экран языка ----------
    public string InstallLanguage { get; set; } = "ru-RU";
    public string TimeAndCurrencyFormat { get; set; } = "ru-RU";
    public string KeyboardLayout { get; set; } = "ru-RU";

    // ---------- Ключ и редакция ----------
    public string? ProductKey { get; set; }
    public WindowsEdition Edition { get; set; } = WindowsEdition.Pro;

    // ---------- EULA и тип установки ----------
    public bool EulaAccepted { get; set; }
    public InstallType InstallType { get; set; } = InstallType.Custom;

    // ---------- Диск ----------
    public int SelectedDiskIndex { get; set; }
    public int SelectedPartitionIndex { get; set; } = 2;
    public bool DiskFormatted { get; set; }

    /// <summary>Id раздела ("disk:partition"), который был отформатирован. Сброс при выборе другого.</summary>
    public string? FormattedPartition { get; set; }

    // ---------- Прогресс ----------
    public double InstallProgress { get; set; }
    public InstallStage? LastStage { get; set; }
    public DateTime? InstallStartedUtc { get; set; }
    public DateTime? InstallCompletedUtc { get; set; }

    // ---------- OOBE (ЭТАП 2) ----------
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? PrimaryKeyboard { get; set; }
    public string? SecondaryKeyboard { get; set; }
    public string? UserName { get; set; }
    public string? UserDisplayName { get; set; }

    /// <summary>Учётная запись, созданная в OOBE. Пароль хранится только как PBKDF2-хэш.</summary>
    public UserAccount? Account { get; set; }

    public DateTime? OobeCompletedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Язык интерфейса: "ru" или "en" (по коду InstallLanguage).</summary>
    [JsonIgnore]
    public string UiCulture =>
        InstallLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";

    /// <summary>Сброс к состоянию «только что включили компьютер».</summary>
    public void ResetForNewInstall()
    {
        Phase = OsPhase.Firmware;
        ProductKey = null;
        Edition = WindowsEdition.Pro;
        EulaAccepted = false;
        InstallType = InstallType.Custom;
        SelectedDiskIndex = 0;
        SelectedPartitionIndex = 2;
        DiskFormatted = false;
        FormattedPartition = null;
        InstallProgress = 0;
        LastStage = null;
        InstallStartedUtc = null;
        InstallCompletedUtc = null;
        UserName = null;
        UserDisplayName = null;
        RegionCode = null;
        RegionName = null;
        PrimaryKeyboard = null;
        SecondaryKeyboard = null;
        Account = null;
        OobeCompletedUtc = null;
        CreatedUtc = DateTime.UtcNow;
    }
}

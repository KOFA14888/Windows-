using System.Text.Json.Serialization;

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

    // ---------- Аватары будущих этапов (ЭТАП 2+) ----------
    public string? UserName { get; set; }
    public string? UserDisplayName { get; set; }
    public string? Region { get; set; }
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
        Region = null;
        CreatedUtc = DateTime.UtcNow;
    }
}

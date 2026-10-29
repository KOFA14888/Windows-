namespace Win10Sim.Core.Setup;

/// <summary>
/// Длительности этапов установки. Настоящая установка Windows 10 занимает
/// 10–30 минут; для симулятора это неприемлемо, поэтому общий прогон ~45 секунд.
/// Одно место, где это меняется: <see cref="SpeedFactor"/>.
/// </summary>
public static class SetupTimings
{
    /// <summary>
    /// 1.0 — «как в демо» (~45 с от «Установить» до перезагрузки).
    /// 0.2 — быстрый прогон для тестов, 3.0 — медленный, чтобы рассмотреть экраны.
    /// </summary>
    public static double SpeedFactor { get; set; } = 1.0;

    public static readonly TimeSpan LanguageToInstall = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan FirmwareBlackScreen = TimeSpan.FromMilliseconds(900);
    public static readonly TimeSpan WindowsLogo = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan RebootLogo = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RebootCountdown = TimeSpan.FromSeconds(6);

    public static TimeSpan Scale(TimeSpan value)
        => TimeSpan.FromMilliseconds(value.TotalMilliseconds * Math.Max(0.01, SpeedFactor));
}

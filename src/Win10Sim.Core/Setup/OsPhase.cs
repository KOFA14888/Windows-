namespace Win10Sim.Core.Setup;

/// <summary>
/// Фаза «жизни» симулятора. Значения упорядочены: сравнение через &gt;= корректно
/// описывает «мы уже прошли установку / OOBE / загрузились в рабочий стол».
/// </summary>
public enum OsPhase
{
    /// <summary>Чёрный экран до инициализации (POST).</summary>
    Firmware = 0,

    /// <summary>Логотип Windows с вращающимися точками.</summary>
    WindowsLogo = 1,

    // ---------- ЭТАП 1: установка ----------
    SetupLanguage = 2,
    SetupInstall = 3,
    SetupKey = 4,
    SetupEdition = 5,
    SetupEula = 6,
    SetupInstallType = 7,
    SetupDisk = 8,
    SetupCopying = 9,
    Reboot = 10,

    // ---------- ЭТАП 2: OOBE ----------
    OobeWait = 11,
    OobeRegion = 12,
    OobeKeyboard = 13,
    OobeAccount = 14,
    OobePassword = 15,
    OobeHello = 16,
    OobePreparing = 17,

    // ---------- ЭТАП 3: оболочка ----------
    LockScreen = 18,
    Desktop = 19
}

public static class OsPhaseExtensions
{
    // У перечисления нет операторов сравнения: '<=' и '>=' для enum не компилируются,
    // поэтому порядок фаз выражается через приведение к int.
    public static bool IsSetupPhase(this OsPhase phase)
        => (int)phase >= (int)OsPhase.WindowsLogo && (int)phase <= (int)OsPhase.Reboot;

    public static bool IsOobePhase(this OsPhase phase)
        => (int)phase >= (int)OsPhase.OobeWait && (int)phase <= (int)OsPhase.OobePreparing;

    public static bool IsShellPhase(this OsPhase phase)
        => (int)phase >= (int)OsPhase.LockScreen;
}

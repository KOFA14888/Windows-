namespace Win10Sim.Core.Setup;

/// <summary>Редакция Windows 10, доступная в симуляторе.</summary>
public enum WindowsEdition
{
    Home = 0,
    Pro = 1,
    Enterprise = 2
}

/// <summary>Тип установки на экране «Выберите тип установки».</summary>
public enum InstallType
{
    /// <summary>Обновление поверх существующей системы (в симуляторе недоступно — как и при загрузке с носителя).</summary>
    Upgrade = 0,

    /// <summary>Выборочная установка.</summary>
    Custom = 1
}

public static class WindowsEditionExtensions
{
    /// <summary>Официальное имя продукта, одинаковое во всех локалях.</summary>
    public static string ProductName(this WindowsEdition edition) => edition switch
    {
        WindowsEdition.Home => "Windows 10 Home",
        WindowsEdition.Pro => "Windows 10 Pro",
        WindowsEdition.Enterprise => "Windows 10 Enterprise",
        _ => "Windows 10"
    };

    /// <summary>Внутренний код редакции — как в реестре (EditionID).</summary>
    public static string EditionId(this WindowsEdition edition) => edition switch
    {
        WindowsEdition.Home => "Core",
        WindowsEdition.Pro => "Professional",
        WindowsEdition.Enterprise => "Enterprise",
        _ => "Core"
    };
}

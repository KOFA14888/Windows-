namespace Win10Sim.Core.Persistence;

/// <summary>
/// Все пути симулятора в одном месте. Данные пишутся в %LocalAppData%\Win10Sim,
/// а не рядом с exe: иначе установленный в Program Files симулятор не смог бы
/// сохранить состояние без прав администратора.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Win10Sim");

    public static string StateFile => Path.Combine(Root, "state.json");
    public static string LogFile => Path.Combine(Root, "app.log");
    public static string VirtualFileSystemFile => Path.Combine(Root, "vfs.json");
    public static string WebViewDataFolder => Path.Combine(Root, "WebView2");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(WebViewDataFolder);
    }
}

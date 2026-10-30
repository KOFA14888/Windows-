namespace Win10Sim.Core.FileSystem;

/// <summary>
/// Стандартное дерево только что установленной Windows 10.
/// Ничего не читается с реального диска: все пути и файлы создаются здесь.
/// </summary>
public static class VfsSeeder
{
    public static VfsNode CreateDefault(string userName)
    {
        string user = string.IsNullOrWhiteSpace(userName) ? "user" : userName;

        var root = new VfsNode(VfsKind.Directory, "C:");

        VfsNode windows = AddDirectory(root, "Windows");
        VfsNode system32 = AddDirectory(windows, "System32");
        AddFile(system32, "cmd.exe", "MZ\u0090\u0000 (исполняемый файл симулятора)");
        AddFile(system32, "notepad.exe", "MZ\u0090\u0000 (исполняемый файл симулятора)");
        AddFile(system32, "drivers", null);       // «файл» без расширения ниже превратится в папку
        AddFile(system32, "win.ini", "[windows]\r\nload=\r\nrun=\r\n");
        AddDirectory(windows, "Fonts");
        AddDirectory(windows, "Temp");
        AddDirectory(windows, "Boot");
        AddFile(windows, "explorer.exe", "MZ\u0090\u0000 (оболочка Windows)");
        AddFile(windows, "winver.exe", "MZ\u0090\u0000");

        VfsNode programFiles = AddDirectory(root, "Program Files");
        AddDirectory(programFiles, "Windows NT");
        AddDirectory(programFiles, "Internet Explorer");
        AddDirectory(programFiles, "Microsoft Edge");

        VfsNode programFilesX86 = AddDirectory(root, "Program Files (x86)");
        AddDirectory(programFilesX86, "Microsoft");

        VfsNode users = AddDirectory(root, "Users");
        VfsNode profile = AddDirectory(users, user);

        VfsNode desktop = AddDirectory(profile, "Desktop");
        AddFile(desktop, "Добро пожаловать.txt",
            "Добро пожаловать в Windows 10.\r\n\r\n" +
            "Этот рабочий стол — часть симулятора. Файлы, которые вы создаёте в Проводнике,\r\n" +
            "Блокноте или командной строке, хранятся в виртуальном диске C: и переживают\r\n" +
            "перезапуск приложения.\r\n\r\n" +
            "Выход из симулятора — клавиша Insert.\r\n");

        VfsNode documents = AddDirectory(profile, "Documents");
        AddFile(documents, "Заметки.txt", "Список дел:\r\n1. Изучить Проводник\r\n2. Открыть командную строку\r\n3. Проверить Edge\r\n");
        AddDirectory(documents, "Мои рисунки");

        AddDirectory(profile, "Downloads");
        AddDirectory(profile, "Pictures");
        AddDirectory(profile, "Music");
        AddDirectory(profile, "Videos");
        AddDirectory(profile, "AppData");

        VfsNode publicUsers = AddDirectory(users, "Public");
        AddDirectory(publicUsers, "Documents");

        VfsNode recycle = AddDirectory(root, "$Recycle.Bin");
        AddFile(recycle, "desktop.ini", "[.ShellClassInfo]\r\nCLSID2={645FF040-5081-101B-9F08-00AA002F954E}\r\n");

        AddFile(root, "pagefile.sys", string.Empty);
        AddFile(root, "bootmgr", "BOOTMGR (симуляция)");

        return root;
    }

    private static VfsNode AddDirectory(VfsNode parent, string name)
    {
        var node = new VfsNode(VfsKind.Directory, name);
        parent.Children.Add(node);
        return node;
    }

    private static void AddFile(VfsNode parent, string name, string? content)
    {
        if (content is null)
        {
            parent.Children.Add(new VfsNode(VfsKind.Directory, name));
            return;
        }

        parent.Children.Add(new VfsNode(VfsKind.File, name)
        {
            Content = content,
            CreatedUtc = DateTime.UtcNow.AddDays(-7),
            ModifiedUtc = DateTime.UtcNow.AddHours(-3)
        });
    }
}

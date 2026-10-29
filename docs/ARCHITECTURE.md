# Windows 10 Simulator — архитектура проекта

> Стек подтверждён: **C# + WPF (.NET 8)**. Документ описывает архитектуру всего проекта;
> реализация ЭТАПА 1 разобрана в [`STAGE1.md`](STAGE1.md).
> Статус: ЭТАП 0 и ЭТАП 1 реализованы.

---

## 1. Цели и принципы

| Принцип | Что это значит на практике |
|---|---|
| **Один HWND** | Всё приложение — одно безрамочное окно на весь экран. Экраны установки, OOBE и рабочий стол — это подмена содержимого (`ContentControl`), а «окна» Проводника/Блокнота — собственные визуальные контролы внутри `Canvas`. Никаких реальных дочерних Win32-окон. |
| **Логика отдельно от UI** | Вся «операционная система» (диск, пользователи, cmd, настройки) живёт в `Win10Sim.Core` без ссылок на WPF. UI можно переписать на WinForms/Avalonia — ядро не изменится. |
| **Детерминированное состояние** | Есть единый `SessionContext` + `state.json`. «Перезагрузка» — реальная перезагрузка состояния, а не имитация: после рестарта симулятор продолжает с той же фазы. |
| **Точность по метрикам Win10** | Цвета, кегли, отступы, задержки — только из токенов (`Colors.xaml`, `Typography.xaml`, `Timings.cs`), а не «на глаз» по месту. |
| **Никаких `Thread.Sleep` в UI-потоке** | Долгие процессы (копирование файлов, «Подождите…») — `async/await` + `CancellationToken`, прогресс через `IProgress<T>`. |
| **Выход только по `Insert`** | Хук ставится в `OnStartup` **до** показа окна, `ShutdownMode="OnExplicitShutdown"`, Alt+F4 и крестик невозможны физически (нет системного меню). |

---

## 2. Выбор технологии

| Вариант | Плюсы | Минусы | Вердикт |
|---|---|---|---|
| **C# + WPF** | Векторный UI и точные стили Win10 через XAML; `Storyboard` для анимаций (вращающиеся точки, акрил, плавные появления); `Segoe MDL2 Assets` — настоящие иконки Win10; **WebView2** — настоящий Edge; XAML-словари = идеальная локализация Setup (язык ОС меняется на лету) | Только Windows | **Рекомендую** |
| C# + WinForms | Быстрый старт, простой код | Плохая масштабируемость под DPI, ручная отрисовка «стекла»/анимаций, нет нормального стилевого слоя | Только если нужен минимализм |
| C++ + SFML/ImGui | Полный контроль, кроссплатформенность | Свой layout-движок, свой текстовый ввод, своя локализация, **нет WebView2** (придётся встраивать WebView2 COM вручную), в 3–4 раза больше кода на те же экраны | Не рекомендуется |

Проект целиком Windows-only по постановке задачи, поэтому кросс-платформенность C++ не даёт выигрыша, а стоимость UI-слоя — максимальная.

---

## 3. Слои

```
┌───────────────────────────────────────────────────────────────────────┐
│ Win10Sim.App  (WPF, net8.0-windows)                                  │
│  Boot flow · Screens (Setup/OOBE/Shell) · Apps (Explorer/Notepad/Edge│
│  /Cmd) · Controls · Themes · Assets                                  │
└───────────────▲───────────────────────────────────────────────────────┘
                │ ссылки только вниз (Core ничего не знает про WPF)
┌───────────────┴───────────────────────────────────────────────────────┐
│ Win10Sim.Core  (netstandard/net8.0, без UI)                          │
│  Setup · Accounts · FileSystem · Shell(cmd) · Services · Persistence │
└───────────────▲───────────────────────────────────────────────────────┘
                │
┌───────────────┴───────────────────────────────────────────────────────┐
│ Win10Sim.Core.Tests  (xUnit)                                         │
└───────────────────────────────────────────────────────────────────────┘
```

---

## 4. Структура решения

```
WindowsSim.sln
├─ src/
│  ├─ Win10Sim.App/
│  │  ├─ App.xaml(.cs)               старт, хук Insert, DI-контейнер, глобальные ресурсы
│  │  ├─ Boot/
│  │  │  ├─ BootFlow.cs             машина фаз: Firmware → Logo → Setup → Reboot → OOBE → Lock → Shell
│  │  │  ├─ ScreenManager.cs        навигация между экранами (стек + анимация перехода)
│  │  │  ├─ IScreen.cs              базовый контракт экрана
│  │  │  └─ ShellRoot.xaml          корневой ContentControl-fullscreen
│  │  ├─ Screens/
│  │  │  ├─ Setup/                  Language → Install → Key → Edition → Eula → InstallType → Disk → Copy → Reboot
│  │  │  ├─ Oobe/                   JustAMoment → Region → Keyboard → Account → Hello → Preparing
│  │  │  └─ Shell/                  LockScreen → Desktop → Taskbar → StartMenu → Tray → ActionCenter
│  │  ├─ Apps/
│  │  │  ├─ Explorer/               Проводник + SimWindow
│  │  │  ├─ Notepad/
│  │  │  ├─ Edge/                   WebView2-хост
│  │  │  └─ Cmd/
│  │  ├─ Controls/                  SimWindow, Win10Button, ToggleSwitch, Tile, ProgressRing,
│  │  │                             SearchBox, TitleBar, AcrylicPanel
│  │  ├─ Themes/                    Colors.xaml, Typography.xaml, Controls/*.xaml, Strings.ru/en.xaml
│  │  └─ Assets/                    wallpapers, icons, cursors, sounds
│  └─ Win10Sim.Core/
│     ├─ Setup/                     SetupSession, ProductKeyValidator, DiskService, InstallPlan
│     ├─ Accounts/                  UserAccount, CredentialStore (PBKDF2), AccountService
│     ├─ FileSystem/                IVirtualFileSystem, VirtualFileSystem, VfsNode, PathUtils
│     ├─ Shell/                     CommandProcessor, ICommand, commands (dir/cd/cls/ping/echo/…)
│     ├─ Services/                  ClockService, SettingsStore, NetworkProbe, NotificationBus
│     └─ Persistence/               JsonStateStore, AppPaths
└─ tests/Win10Sim.Core.Tests/
```

---

## 5. Ключевые подсистемы

### 5.1 Одно окно — много экранов

```csharp
public interface IScreen
{
    string Id { get; }
    object View { get; }                       // UserControl
    Task OnEnterAsync(object? parameter, CancellationToken ct);
    Task<bool> OnLeaveAsync(CancellationToken ct);   // false — отменить переход
}

public sealed class ScreenManager
{
    public IScreen? Current { get; }
    public Task NavigateAsync(string screenId, object? parameter = null);   // кросс-фейд 150–250 мс
    public Task GoBackAsync();
}
```

`ShellRoot.xaml`:

```xml
<ContentControl x:Name="Host" Focusable="False"/>
```

Экраны регистрируются в `ScreenRegistry` (фабрики, а не синглтоны — состояние экрана не протекает между проходами установки).

### 5.2 Машина фаз и контекст сессии

```csharp
public enum OsPhase
{
    Firmware, WindowsLogo, SetupLanguage, SetupInstall, SetupKey, SetupEdition,
    SetupEula, SetupInstallType, SetupDisk, SetupCopying, Reboot,
    OobeWait, OobeRegion, OobeKeyboard, OobeAccount, OobeHello, OobePreparing,
    LockScreen, Desktop
}

public sealed class SessionContext
{
    public OsPhase Phase { get; set; }
    public string UILanguage { get; set; } = "ru-RU";
    public string TimeFormat { get; set; } = "HH:mm";
    public string KeyboardLayout { get; set; } = "ru-RU";
    public string? ProductKey { get; set; }
    public string? Edition { get; set; }              // Home | Pro | Enterprise
    public InstallType InstallType { get; set; }
    public DiskPlan? Disk { get; set; }
    public List<UserAccount> Users { get; } = new();
}
```

`BootFlow` при старте читает `state.json`; если `Phase == Desktop` — сразу рабочий стол (после Lock Screen), иначе продолжает с сохранённой фазы. «Перезагрузка» на этапе копирования файлов = `Phase = OobeWait` + перезапуск рендера, с кратковременным чёрным экраном и повторным показом логотипа.

### 5.3 Внутренняя оконная система рабочего стола

Критично для достоверности: **не создаём реальные окна**. Иначе появятся тени/рамки/поведение настоящей Windows, и «Пуск» окажется вне нашего UI.

```csharp
public sealed class SimWindowManager          // UI-слой
{
    public IReadOnlyList<SimWindow> Windows { get; }
    public SimWindow Open(SimAppDescriptor app, object? args = null);
    public void Activate(SimWindow window);
    public void Minimize(SimWindow window);   // сворачивается в панель задач
    public void ToggleMaximize(SimWindow window);
    public void Close(SimWindow window);
}
```

`SimWindow` — контрол: заголовок 32 px, кнопки «свернуть/развернуть/закрыть» (Segoe MDL2 `E921`, `E922`, `E8BB`), перетаскивание за заголовок, `ResizeMode`, z-order по `Canvas.ZIndex`, `TaskbarItem`-связка.

### 5.4 Виртуальная файловая система

```csharp
public interface IVirtualFileSystem
{
    VfsNode Root { get; }
    VfsNode? Get(string path);
    VfsNode CreateDirectory(string path);
    VfsNode CreateFile(string path, string content = "");
    void WriteAllText(string path, string content);
    string ReadAllText(string path);
    void Delete(string path, bool recursive);
    void Move(string from, string to);
    IEnumerable<VfsNode> Enumerate(string path);
    long GetFreeSpaceBytes();
}
```

Дерево `VfsNode { Name, IsDirectory, Children, Size, CreatedUtc, ModifiedUtc, Content }`; предзаполняется стандартным деревом (`C:\Windows`, `C:\Users\<name>\Desktop`, `Documents`, `Downloads`, `Pictures`, `Program Files`, `$Recycle.Bin`). Сериализация в `%LocalAppData%\Win10Sim\vfs.json`. Проводник, Блокнот и `cmd` работают с **одним** экземпляром ФС через DI — это даёт согласованность («файл, созданный в cmd, виден в Проводнике»).

### 5.5 Командная строка

```csharp
public interface ICommand
{
    string Name { get; }
    string Help { get; }
    Task<int> ExecuteAsync(CommandContext ctx, IReadOnlyList<string> args, CancellationToken ct);
}

public sealed class CommandProcessor
{
    public string WorkingDirectory { get; set; } = @"C:\Users\user";
    public Task<CommandResult> ExecuteAsync(string line, CancellationToken ct);
}
```

Первая итерация: `help, cls, dir, cd, chdir, md, rd, del, copy, move, echo, type, ver, whoami, hostname, date, time, exit, ping` (ping — реальный ICMP через `System.Net.NetworkInformation.Ping`, с таймаутами и `-t`). История команд — `↑/↓`, автодополнение по `Tab`.

### 5.6 Браузер (Microsoft Edge)

`WebView2` (пакет `Microsoft.Web.WebView2`, runtime распространяется вместе с Windows 10/11). Хост-контрол: адресная строка, назад/вперёд/обновить, вкладки (пока одна), обработка `NavigationStarting/Completed`, страница-заглушка для невалидных URL. Важно: `CoreWebView2Environment` создаётся с `UserDataFolder` внутри `%LocalAppData%\Win10Sim\WebView2`, чтобы не мусорить в профиле пользователя.

> Примечание: WebView2 требует установленный Edge WebView2 Runtime. Если его нет — экран Edge показывает понятную ошибку, симулятор продолжает работать.

### 5.7 Визуальный слой

* **Токены**: акцент `#0078D7` (Win10 default), фон панели задач `#1F1F1F`/акрил, текст `#FFFFFF` / `#000000`.
* **Шрифты**: `Segoe UI` (текст), `Segoe MDL2 Assets` (иконки), `Segoe UI Semibold` 20/24 px для заголовков экранов установки.
* **Акрил/блюр**: `SetWindowCompositionAttribute(ACCENT_ENABLE_BLURBEHIND)` для панели задач и «Пуск»; fallback — полупрозрачный слой с шумом.
* **Анимации**: длительности в `Timings.cs` (`ScreenFade = 200ms`, `StartOpen = 250ms`, `BootSpinner = 1600ms/оборот`), easing `CubicEase.EaseOut`.
* **Иконки**: только глифы MDL2 (`E7C3` Этот компьютер, `E74D` Корзина, `E8B7` Проводник, `E70F` Параметры…) — узнаваемость важнее самодельного арта.

### 5.8 Локализация

Setup — первый экран, и выбранный там язык должен менять **всю** ОС (это самая заметная деталь достоверности). Реализация: `ResourceDictionary` на язык (`Strings.ru.xaml`, `Strings.en.xaml`) + `LocalizationService.Apply(culture)`; строки в XAML берутся через `{DynamicResource Str.InstallNow}`, поэтому переключение мгновенное, без перезапуска.

### 5.9 Прочее

`ClockService` — единственный источник времени для панели задач, экрана блокировки и `cmd date/time`; поддерживает формат из Setup. `SettingsStore` — персонализация (обои, тема, акцент). `NotificationBus` — всплывающие уведомления трея.

---

## 6. Этапы → модули → критерии приёмки

| Этап | Модули | Готово, если |
|---|---|---|
| **1. Boot & Setup** | `Boot/`, `Screens/Setup/*`, `Core/Setup/*` | Полный проход: логотип со спинерами → язык/время/раскладка → Install Now → «У нас нет ключа» → редакция → EULA (кнопка неактивна, пока не принято) → тип установки → выбор диска/форматирование → прогресс 0–100 % с реальным текстом фаз → «Перезагрузка», после которой фаза = OOBE и она сохраняется в `state.json` |
| **2. OOBE** | `Screens/Oobe/*`, `Core/Accounts/*` | «Подождите…» → регион → раскладка → имя пользователя (валидация) + пароль → «Привет» (анимация) → «Мы готовим всё для вас» → рабочий стол. Аккаунт сохраняется и позже принимается на Lock Screen |
| **3. Desktop** | `Screens/Shell/*`, `Controls/*` | Lock Screen с часами и вводом пароля → рабочий стол с обоями Win10, иконками «Этот компьютер»/«Корзина» (двойной клик открывает Проводник) → панель задач (Пуск, поиск, трей, часы) → меню Пуск со списком программ и плитками, живой поиск |
| **4. Apps** | `Apps/*`, `Core/FileSystem`, `Core/Shell` | Проводник с навигацией по `C:` и CRUD файлов; Блокнот с сохранением в VFS; Edge с WebView2 и реальными сайтами; `cmd` с `dir/cd/cls/ping` и единым cwd |

---

## 7. Ограничения и допущения

1. **Exclusive fullscreen в WPF недоступен.** Реализуем «borderless fullscreen»: окно без рамки, растянутое на `rcMonitor` (не на рабочую область), `WS_CAPTION|WS_THICKFRAME|WS_SYSMENU|WS_MINIMIZEBOX|WS_MAXIMIZEBOX` сняты, панель задач скрыта (`Shell_TrayWnd`) и восстанавливается при выходе. Визуально это неотличимо от полноэкранного режима.
2. **Alt+F4 / Alt+Tab / Win** не перехватываются — задача симулятора, а не киоск-локера. Гарантированно закрыть приложение можно только `Insert`.
3. **`Segoe MDL2 Assets`** присутствует в Windows 10/11; на других ОС иконки деградируют до `Path`-геометрий (fallback-словарь).
4. **WebView2 Runtime** должен быть установлен (обычно уже есть в Win10 20H2+).
5. Ограничения на реальные операции с диском, реестром, сетью отсутствуют: симулятор **ничего не форматирует** — «диск C:» живёт в JSON.

---

## 8. Соглашения

* MVVM-lite: код-behind только для визуальных эффектов и фокуса; состояние — во `ViewModel`/сервисах. Никакой бизнес-логики в `xaml.cs`.
* Публичные API ядра — `async` c `CancellationToken` по умолчанию.
* Никаких магических чисел в XAML: отступы/кегли — из `Themes/Tokens`.
* Каждая фаза установки — отдельный класс, ≤ 200 строк, с тестом в `Win10Sim.Core.Tests` (валидация ключа, план диска, парсинг `cd ..` и т. п.).
* Форматирование: `dotnet format`, nullable enabled, `Directory.Build.props` с `TreatWarningsAsErrors` для `Core`.

---

## 9. Шаблон «Fullscreen + Insert» (ЭТАП 0 → перенесён в src/)

Рабочие шаблоны лежат в `templates/`:

* `templates/csharp-wpf/` — **эталон**: WPF, безрамочное окно на весь экран, глобальный хук `Insert`, логотип с точками как тизер ЭТАПА 1.
* `templates/csharp-winforms/` — тот же контракт на WinForms (для сравнения).
* `templates/cpp-sfml/` — SFML 2.6, `sf::Style::None` + `sf::Keyboard::Insert`.

Проверить шаблон: `cd templates/csharp-wpf && dotnet run` → на весь экран появляется логотип; `Insert` возвращает на рабочий стол.

В рабочем проекте эти же три класса перенесены в `src/Win10Sim.App/Platform/`
(`Fullscreen.cs`, `InsertKeyHook.cs`, `TaskbarVisibility.cs`) без изменений логики.

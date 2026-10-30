# Windows 10 Simulator

Детальный симулятор Windows 10 на **C# + WPF (.NET 8)**: загрузка → установка →
OOBE → рабочий стол с Проводником, Блокнотом, Edge (WebView2) и `cmd`.

| Этап | Состояние |
|---|---|
| 0. Каркас окна (fullscreen + выход по `Insert`) | ✅ готов |
| 1. Boot & Setup: логотип, язык, Install Now, ключ, редакция, EULA, тип установки, диск, прогресс, перезагрузка | ✅ готов |
| 2. OOBE: «Подождите…», регион, раскладка, аккаунт, пароль, «Привет», «Мы готовим всё для вас» | ✅ готов |
| 3. Рабочий стол: Lock Screen с паролем, обои, иконки, панель задач, меню «Пуск», центр уведомлений, питание | ✅ готов |
| 4. Проводник на виртуальном диске, Блокнот, Edge (WebView2), `cmd`, Параметры | ✅ готов |

## Скачать готовый .exe (без Visual Studio)

Каждый push собирается на GitHub Actions (Windows-раннер):

* **Releases** → последний релиз → `Win10Sim-standalone-win-x64.exe` (~60 МБ, .NET не нужен);
* либо **Actions → Build → последний запуск → Artifacts**:
  * `Win10Sim-standalone-win-x64` — автономный single-file exe;
  * `Win10Sim-framework-dependent-win-x64` — компактная сборка (нужен .NET 8 Desktop Runtime).

## Сборка из исходников (Windows 10/11)

```powershell
git clone <этот-репозиторий> WindowsSim
cd WindowsSim
dotnet run --project src/Win10Sim.App
```

Требуется .NET 8 SDK (или Visual Studio 2022 с рабочей нагрузкой «.NET desktop development»).
Свой exe: `dotnet publish src/Win10Sim.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.

Или открыть `Win10Sim.sln` в Visual Studio 2022 и нажать F5.

**Управление:** `Insert` — выход на реальный рабочий стол (единственный способ),
`Esc` — шаг назад на экранах установки, `Win` — меню «Пуск», `Ctrl+E` в «Пуске» —
Проводник, `Tab`/`Enter` — обычная навигация WPF.

Состояние установки сохраняется в `%LocalAppData%\Win10Sim\state.json` после
каждого экрана, виртуальный диск — в `vfs.json`, оформление рабочего стола —
в `desktop.json` (в той же папке): закрыли симулятор и запустили снова — вернётесь на тот же шаг.
Чтобы начать установку с нуля, удалите этот файл (или нажмите
«Установить Windows заново» на финальном экране).

## Уже работает: ЭТАП 3 и ЭТАП 4

### ЭТАП 3 — рабочий стол

1. **Экран блокировки** — размытые обои, часы и дата, «нажмите любую клавишу»,
   вход по паролю из OOBE (PBKDF2-SHA256), «тряска» и сообщение при ошибке.
2. **Рабочий стол** — обои, иконки «Этот компьютер» и «Корзина», контекстное меню
   («Создать папку / текстовый документ», «Обновить», «Персонализация»).
3. **Панель задач** — «Пуск», поиск, кнопки открытых окон, язык, сеть, звук,
   часы (формат настраивается), «Показать рабочий стол».
4. **Меню «Пуск»** — список приложений с поиском, плитки, пользователь, питание:
   выключение (честное уведомление), перезагрузка, сон, выход, блокировка.
5. **Центр уведомлений** — быстрые действия Wi-Fi/Bluetooth/самолёт/ночной свет,
   плитки параметров, лента уведомлений.
6. **Внутренние окна** — перетаскивание, изменение размера, свернуть/развернуть/
   закрыть, z-order, одно окно на приложение.

### ЭТАП 4 — приложения

1. **Проводник** — виртуальный диск `C:` (`Windows`, `Program Files`, `Users\<user>\*`,
   `$Recycle.Bin`), навигация с историей, создание/переименование/удаление,
   открытие `.txt` в Блокноте, строка состояния со свободным местом.
2. **Блокнот** — меню «Файл/Правка/Вид», `Ctrl+S/O/N`, `F5`, перенос по словам,
   автосохранение, собственные диалоги открытия и сохранения на виртуальном диске.
3. **Microsoft Edge** — настоящий Chromium через WebView2: адресная строка с
   поиском Bing, назад/вперёд/обновить; без WebView2 Runtime — понятное сообщение.
4. **Командная строка** — `dir, cd, md, rd, del, copy, move, ren, type, echo`
   (с `>` и `>>`), `ver, whoami, hostname, date, time, ping` (настоящий ICMP),
   `cls, help, exit`; история по `↑`/`↓`, `Tab`-дополнение, `Ctrl+C`.
5. **Параметры** — тема, формат часов, язык (ru/en на лету), учётная запись,
   сведения о системе.

## ЭТАП 1 и ЭТАП 2 (как было)

### ЭТАП 2 — первая настройка (OOBE)

1. **«Подождите…»** — синий экран первой настройки.
2. **«Ваш регион?»** — регион определяется по языку установки, можно выбрать другой.
3. **Раскладка клавиатуры** — основная и вторая (необязательная).
4. **«Кто будет использовать этот компьютер?»** — имя с правилами Windows
   (20 символов, запрещённые символы, `CON`/`NUL`/`COM1`… зарезервированы).
5. **Пароль** — PBKDF2-SHA256, 600 000 итераций и случайная соль; в `state.json`
   попадают только соль и хэш. Можно «Продолжить без пароля».
6. **«Привет»** → **«Мы готовим всё для вас»** — шесть этапов с прогрессом.
7. Итог: учётная запись, регион, раскладки — и всё это переживает перезапуск.

## ЭТАП 1 — что уже работает

Полный проход установки, ~45 секунд (ускоряется одной строкой, см.
[`docs/STAGE1.md`](docs/STAGE1.md)):

1. **Загрузка** — чёрный экран, логотип Windows, пять вращающихся точек (1.6 с/оборот).
2. **Язык / формат времени / раскладка** — выбор языка немедленно переключает
   язык **всей** программы установки (ru ⇄ en), без перезапуска.
3. **«Установить сейчас»** — с неактивной ссылкой «Восстановление компьютера».
4. **Ключ продукта** — автоформатирование `XXXXX-XXXXX-XXXXX-XXXXX-XXXXX`,
   фильтрация недопустимых символов, три уровня ошибок; generic-ключи Windows 10
   дают соответствующую редакцию; кнопка **«У меня нет ключа продукта»**.
5. **Выбор редакции** — Home / Pro / Enterprise (только если ключа нет).
6. **Условия лицензии** — флажок активируется только после прокрутки текста до конца.
7. **Тип установки** — «Обновление» недоступно (как при загрузке с носителя), «Выборочная» выбрана.
8. **Где установить Windows** — список дисков и разделов, «Форматировать» с
   модальным оверлеем, «Далее» недоступно до форматирования выбранного раздела.
9. **Копирование файлов 0–100 %** — четыре этапа, реальные пути `C:\Windows\…`,
   проценты, «Компьютер перезагрузится через несколько секунд».
10. **Перезагрузка** — снова логотип и точки, затем честная заглушка ЭТАПА 2
    с итогом установки (ключ, редакция, раздел) и кнопкой «Установить заново».

## Структура

```
Win10Sim.sln
├─ src/
│  ├─ Win10Sim.Core/            логика без UI (net8.0)
│  │  ├─ FileSystem/            VfsPath, VfsNode, VirtualFileSystem, VfsSeeder
│  │  ├─ Shell/                 CommandParser, CommandProcessor, 19 команд cmd
│  │  ├─ Setup/                 OsPhase, SetupSession, ProductKeyValidator, SetupInstaller,
│  │  │                         DiskModel, LicenseAgreement, SetupTimings,
│  │  │                         RegionCatalog, KeyboardCatalog, OobePreparer
│  │  ├─ Accounts/              PasswordHasher (PBKDF2), UserAccount, AccountValidator
│  │  └─ Persistence/           AppPaths, JsonStateStore (атомарная запись)
│  └─ Win10Sim.App/             WPF-приложение (net8.0-windows)
│     ├─ App.xaml(.cs)          старт, хук Insert, DI-контейнер, аварийный выход
│     ├─ MainWindow.xaml(.cs)   единственное окно, без рамки, на весь экран
│     ├─ Boot/                  BootFlow (машина фаз), ScreenManager (кросс-фейд)
│     ├─ Screens/               Boot, Setup (10), Oobe (8), Shell (LockScreen, Desktop,
│     │                         Taskbar, StartMenu)
│     ├─ Apps/                  Explorer, Notepad, Edge, Cmd, Settings
│     ├─ Shell/                 SimWindow (окно), SimWindowManager, AppCatalog
│     ├─ Assets/               Wallpaper.jpg (обои рабочего стола и блокировки)
│     ├─ Controls/              WindowsLogo, BootSpinner
│     ├─ Themes/                Tokens.xaml, Styles.xaml, Lang.ru.xaml, Lang.en.xaml
│     ├─ Infrastructure/        AppServices, SetupScreenBase, UiStrings, IAppHost
│     └─ Platform/              Fullscreen, InsertKeyHook, TaskbarVisibility
├─ tests/Win10Sim.Core.Tests/   141 xUnit-тест: ключи, состояние, установщик, OOBE, пароли,
│                               локализация, виртуальный диск, команды cmd
├─ tools/check_xaml.py          статическая проверка XAML/ресурсов без .NET
├─ docs/ARCHITECTURE.md         архитектура всего проекта
├─ docs/STAGE1.md               разбор ЭТАПА 1: поток, решения, чек-лист
├─ docs/STAGE2.md               разбор ЭТАПА 2: OOBE, хранение пароля, чек-лист
├─ docs/STAGE3.md               разбор ЭТАПА 3: экран блокировки, рабочий стол, окна
├─ docs/STAGE4.md               разбор ЭТАПА 4: VFS, cmd, Проводник, Блокнот, Edge
├─ .github/workflows/build.yml  сборка exe, тесты, релиз
└─ templates/                   минимальные шаблоны «fullscreen + Insert» (WPF/WinForms/C++ SFML)
```

## Критические требования

| Требование | Реализация |
|---|---|
| Полноэкранный запуск, никаких рамок/заголовков/кнопок | `WindowStyle=None` + снятие `WS_CAPTION/WS_THICKFRAME/WS_SYSMENU/WS_MINIMIZEBOX/WS_MAXIMIZEBOX` + `SetWindowPos` по `rcMonitor` (`Platform/Fullscreen.cs`) |
| Выход **только** по `Insert` | глобальный `WH_KEYBOARD_LL`-хук (`Platform/InsertKeyHook.cs`), клавиша «съедается»; `ShutdownMode=OnExplicitShutdown`; резервный `PreviewKeyDown`; аварийный `RequestShutdown()` из обработчика необработанных исключений |

## Проверки без Visual Studio

```bash
python3 tools/check_xaml.py     # XAML well-formed, все Static/DynamicResource объявлены,
                                # x:Class ↔ code-behind, все обработчики событий существуют,
                                # паритет ключей ru/en, все строки из C# объявлены

pip install tree-sitter tree-sitter-c-sharp
python3 tools/check_csharp.py   # синтаксис всех .cs через tree-sitter (без .NET SDK)
```

## Документация

* [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — слои, подсистемы, критерии приёмки по этапам.
* [`docs/STAGE1.md`](docs/STAGE1.md) — как устроен ЭТАП 1 и что проверять руками.
* [`templates/README.md`](templates/README.md) — шаблоны «Fullscreen + Insert» на трёх стеках.

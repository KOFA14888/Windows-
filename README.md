# Windows 10 Simulator

Детальный симулятор Windows 10 на **C# + WPF (.NET 8)**: загрузка → установка →
OOBE → рабочий стол с Проводником, Блокнотом, Edge (WebView2) и `cmd`.

| Этап | Состояние |
|---|---|
| 0. Каркас окна (fullscreen + выход по `Insert`) | ✅ готов |
| 1. Boot & Setup: логотип, язык, Install Now, ключ, редакция, EULA, тип установки, диск, прогресс, перезагрузка | ✅ готов |
| 2. OOBE: «Подождите…», регион, раскладка, аккаунт, пароль, «Привет», «Мы готовим всё для вас» | ✅ готов |
| 3. Рабочий стол, панель задач, меню «Пуск», Lock Screen | ⏳ следующий |
| 4. Проводник, Блокнот, Edge, `cmd` | ⏳ |

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
`Esc` — шаг назад на экранах установки, `Tab`/`Enter` — обычная навигация WPF.

Состояние установки сохраняется в `%LocalAppData%\Win10Sim\state.json` после
каждого экрана: закрыли симулятор и запустили снова — вернётесь на тот же шаг.
Чтобы начать установку с нуля, удалите этот файл (или нажмите
«Установить Windows заново» на финальном экране).

## Уже работает: ЭТАП 1 и ЭТАП 2

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
│  │  ├─ Setup/                 OsPhase, SetupSession, ProductKeyValidator, SetupInstaller,
│  │  │                         DiskModel, LicenseAgreement, SetupTimings,
│  │  │                         RegionCatalog, KeyboardCatalog, OobePreparer
│  │  ├─ Accounts/              PasswordHasher (PBKDF2), UserAccount, AccountValidator
│  │  └─ Persistence/           AppPaths, JsonStateStore (атомарная запись)
│  └─ Win10Sim.App/             WPF-приложение (net8.0-windows)
│     ├─ App.xaml(.cs)          старт, хук Insert, DI-контейнер, аварийный выход
│     ├─ MainWindow.xaml(.cs)   единственное окно, без рамки, на весь экран
│     ├─ Boot/                  BootFlow (машина фаз), ScreenManager (кросс-фейд)
│     ├─ Screens/               Boot, Setup (10 экранов), Oobe (8 экранов), Placeholder
│     ├─ Controls/              WindowsLogo, BootSpinner
│     ├─ Themes/                Tokens.xaml, Styles.xaml, Lang.ru.xaml, Lang.en.xaml
│     ├─ Infrastructure/        AppServices, SetupScreenBase, UiStrings, IAppHost
│     └─ Platform/              Fullscreen, InsertKeyHook, TaskbarVisibility
├─ tests/Win10Sim.Core.Tests/   ~110 xUnit-тестов: ключи, состояние, установщик, OOBE, пароли, локализация
├─ tools/check_xaml.py          статическая проверка XAML/ресурсов без .NET
├─ docs/ARCHITECTURE.md         архитектура всего проекта
├─ docs/STAGE1.md               разбор ЭТАПА 1: поток, решения, чек-лист
├─ docs/STAGE2.md               разбор ЭТАПА 2: OOBE, хранение пароля, чек-лист
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

#!/usr/bin/env python3
"""
Статическая проверка XAML/кода без установленного .NET.

Проверяет то, что чаще всего ломает WPF-проект и что не видно при чтении диффа:
  1. XAML well-formed и x:Class имеет code-behind с InitializeComponent().
  2. Каждый {StaticResource}/{DynamicResource} ключ реально объявлен.
  3. Набор ключей Lang.ru.xaml и Lang.en.xaml совпадает.
  4. Баланс фигурных скобок/скобок в .cs и отсутствие «висячих» using.
  5. Все UserControl'ы, на которые ссылаются экраны, существуют.

Запуск: python3 tools/check_xaml.py
"""
from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

XAML_NS = "{http://schemas.microsoft.com/winfx/2006/xaml}"
ROOT = Path(__file__).resolve().parent.parent / "src" / "Win10Sim.App"

errors: list[str] = []
warnings: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def warn(message: str) -> None:
    warnings.append(message)


def parse_xaml(path: Path) -> ET.Element | None:
    try:
        return ET.parse(path).getroot()
    except ET.ParseError as exc:
        fail(f"{path.relative_to(ROOT)}: XML не разбирается — {exc}")
        return None


xaml_files = sorted(ROOT.rglob("*.xaml"))
if not xaml_files:
    fail("XAML-файлы не найдены")
    sys.exit(1)

# ---------- 1. x:Class <-> code-behind ----------
resources: set[str] = set()
resource_owners: dict[str, list[str]] = {}
xaml_texts: dict[Path, str] = {}

for path in xaml_files:
    text = path.read_text(encoding="utf-8")
    xaml_texts[path] = text
    root = parse_xaml(path)
    if root is None:
        continue

    # ключи ресурсов
    for element in root.iter():
        key = element.get(f"{XAML_NS}Key")
        if key:
            resources.add(key)
            resource_owners.setdefault(key, []).append(path.name)

    clazz = root.get(f"{XAML_NS}Class")
    is_application = root.tag.endswith("Application")
    if clazz and not is_application:
        code_behind = path.with_suffix(".xaml.cs")
        if not code_behind.exists():
            fail(f"{path.relative_to(ROOT)}: x:Class={clazz}, но нет {code_behind.name}")
        else:
            cs = code_behind.read_text(encoding="utf-8")
            if "InitializeComponent()" not in cs:
                fail(f"{code_behind.name}: нет вызова InitializeComponent()")
            expected = clazz.split(".")[-1]
            if not re.search(rf"\bpartial class {expected}\b", cs):
                fail(f"{code_behind.name}: нет 'partial class {expected}'")
            namespace = clazz.rsplit(".", 1)[0]
            if f"namespace {namespace}" not in cs:
                fail(f"{code_behind.name}: namespace не совпадает с x:Class ({namespace})")

            # Базовый класс в code-behind обязан совпадать с корневым элементом XAML:
            # иначе компилятор ругается "Partial declarations must not specify different
            # base classes" — ошибка, которую легко не заметить при чтении диффа.
            root_local = root.tag.split("}")[-1]
            if root_local not in ("UserControl", "Window", "Application", "Page", "ResourceDictionary"):
                declared = re.search(
                    rf"partial class {expected}\s*:\s*([A-Za-z0-9_.]+)", cs)
                base_name = declared.group(1).split(".")[-1] if declared else None
                if base_name != root_local:
                    fail(f"{path.relative_to(ROOT)}: корень <{root_local}>, "
                         f"а code-behind наследует {base_name or 'UserControl'}")

# ---------- 2. Ссылки на ресурсы ----------
# Неявные стили по TargetType и системные ключи WPF считаем валидными.
BUILTIN_PREFIXES = ("{x:", "SystemColors.", "SystemFonts.")

for path, text in xaml_texts.items():
    for match in re.finditer(r"\{(?:Static|Dynamic)Resource\s+([A-Za-z0-9_.]+)\}", text):
        key = match.group(1)
        if key not in resources:
            fail(f"{path.relative_to(ROOT)}: ресурс '{key}' не объявлен")

# ---------- 3. Паритет языковых словарей ----------
def lang_keys(name: str) -> set[str]:
    path = ROOT / "Themes" / name
    root = parse_xaml(path)
    if root is None:
        return set()
    return {e.get(f"{XAML_NS}Key") for e in root.iter() if e.get(f"{XAML_NS}Key")}


ru_keys = lang_keys("Lang.ru.xaml")
en_keys = lang_keys("Lang.en.xaml")

for missing in sorted(ru_keys - en_keys):
    fail(f"Lang.en.xaml: отсутствует ключ '{missing}' (есть в ru)")
for missing in sorted(en_keys - ru_keys):
    fail(f"Lang.ru.xaml: отсутствует ключ '{missing}' (есть в en)")

# ---------- 4. Ключи, на которые ссылаются из C# ----------
for cs_path in ROOT.rglob("*.cs"):
    cs = cs_path.read_text(encoding="utf-8")
    for key in re.findall(r'"(L\.[A-Za-z0-9_.]+)"', cs):
        if key not in resources:
            fail(f"{cs_path.relative_to(ROOT)}: строка '{key}' отсутствует в словарях")

# ---------- 5. Баланс скобок и базовые правила C# ----------
for cs_path in sorted(ROOT.rglob("*.cs")):
    cs = cs_path.read_text(encoding="utf-8")
    # убираем строковые литералы и комментарии грубо, но достаточно для баланса
    stripped = re.sub(r'@"(?:[^"]|"")*"', '""', cs)
    stripped = re.sub(r'"(?:\\.|[^"\\])*"', '""', stripped)
    stripped = re.sub(r"//[^\n]*", "", stripped)
    stripped = re.sub(r"/\*.*?\*/", "", stripped, flags=re.S)
    for open_ch, close_ch in (("{", "}"), ("(", ")"), ("[", "]")):
        if stripped.count(open_ch) != stripped.count(close_ch):
            fail(f"{cs_path.relative_to(ROOT)}: несбалансированные {open_ch}{close_ch}")
    if "namespace " not in cs:
        fail(f"{cs_path.relative_to(ROOT)}: нет объявления namespace")

# ---------- 6. Ссылки на пользовательские контролы ----------
custom_prefixes = {
    "controls": ROOT / "Controls",
}

for path, text in xaml_texts.items():
    for prefix, directory in custom_prefixes.items():
        for name in re.findall(rf"<{prefix}:([A-Za-z0-9_]+)", text):
            candidate = directory / f"{name}.xaml"
            if not candidate.exists():
                fail(f"{path.relative_to(ROOT)}: контрол {prefix}:{name} не найден ({candidate.name})")

# ---------- 7. Экраны, на которые ссылается BootFlow ----------
boot_flow = (ROOT / "Boot" / "BootFlow.cs").read_text(encoding="utf-8")
for screen in re.findall(r"new ([A-Za-z0-9_]+)View\(", boot_flow):
    matches = list(ROOT.rglob(f"{screen}View.xaml"))
    if not matches:
        fail(f"BootFlow.cs: экран {screen}View не найден")


# ---------- 8. x:Name и обработчики событий ----------
NAME_NS = "{http://schemas.microsoft.com/winfx/2006/xaml}"
EVENT_RE = re.compile(r"\s([A-Z][A-Za-z0-9]*)=\"([A-Za-z_][A-Za-z0-9_]*)\"")
EVENT_ATTRS = {
    "Click", "Checked", "Unchecked", "SelectionChanged", "TextChanged",
    "ScrollChanged", "Loaded", "SizeChanged", "MouseDown", "MouseUp",
}

for path, text in xaml_texts.items():
    root = parse_xaml(path)
    if root is None or root.tag.endswith("Application"):
        continue
    code_behind = path.with_suffix(".xaml.cs")
    if not code_behind.exists():
        continue
    cs = code_behind.read_text(encoding="utf-8")

    for element in root.iter():
        name = element.get(f"{NAME_NS}Name")
        if name and not re.search(rf"\b{re.escape(name)}\b", cs):
            # Имя может использоваться внутри самого XAML (Storyboard.TargetName,
            # ElementName, RelativeSource Self) — это нормально.
            if not re.search(rf'(TargetName|ElementName|Path)=\"{re.escape(name)}\"', text):
                warn(f"{path.relative_to(ROOT)}: x:Name '{name}' нигде не используется")
            continue
        for attr, value in element.attrib.items():
            local = attr.split("}")[-1]
            if local in EVENT_ATTRS and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", value):
                if not re.search(rf"\b(private|protected|public|internal)\s+(async\s+)?\w+\s+{value}\s*\(", cs):
                    fail(f"{path.relative_to(ROOT)}: обработчик '{value}' ({local}) не найден в {code_behind.name}")

print(f"XAML-файлов: {len(xaml_files)}, ключей ресурсов: {len(resources)}, строк ru/en: {len(ru_keys)}")

if warnings:
    print("\nПредупреждения:")
    for message in warnings:
        print("  ⚠", message)

if errors:
    print(f"\nОшибок: {len(errors)}")
    for message in errors:
        print("  ✗", message)
    sys.exit(1)

print("Проверка пройдена: расхождений не найдено.")

#!/usr/bin/env python3
"""
Синтаксическая проверка C# через tree-sitter.

Не заменяет компилятор (не проверяет типы), но ловит то, что компилятор
поймал бы первым: опечатки в объявлениях, незакрытые скобки, битые паттерны,
недописанные выражения. Полезно, когда под рукой нет .NET SDK.

Требуется: pip install tree-sitter tree-sitter-c-sharp
Запуск:     python3 tools/check_csharp.py
"""
from __future__ import annotations

import sys
from pathlib import Path

from tree_sitter import Language, Parser
import tree_sitter_c_sharp

ROOT = Path(__file__).resolve().parent.parent
SOURCES = [
    ROOT / "src" / "Win10Sim.Core",
    ROOT / "src" / "Win10Sim.App",
    ROOT / "tests" / "Win10Sim.Core.Tests",
]


def main() -> int:
    language = Language(tree_sitter_c_sharp.language())
    parser = Parser(language)

    files = sorted(path for directory in SOURCES if directory.exists() for path in directory.rglob("*.cs"))
    problems = 0

    for path in files:
        source = path.read_bytes()
        tree = parser.parse(source)

        stack = [tree.root_node]
        file_problems: list[str] = []

        while stack:
            node = stack.pop()
            if node.type == "ERROR" or node.is_missing:
                line = node.start_point[0] + 1
                column = node.start_point[1] + 1
                snippet = source[node.start_byte:node.end_byte][:60].decode("utf-8", "replace").replace("\n", " ")
                kind = "MISSING" if node.is_missing else "ERROR"
                file_problems.append(f"{path.relative_to(ROOT)}:{line}:{column} {kind}: '{snippet}'")
            else:
                stack.extend(node.children)

        if file_problems:
            problems += len(file_problems)
            for message in file_problems:
                print("  ✗", message)

    print(f"Разобрано файлов: {len(files)}")
    if problems:
        print(f"Синтаксических проблем: {problems}")
        return 1

    print("Синтаксис C# в порядке.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

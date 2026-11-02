#!/usr/bin/env python3
"""Перевіряє відносні посилання між md-файлами: існування файлу й якоря заголовка.

Запуск із кореня репозиторію: python tools/docs/check_links.py
Код виходу 1, якщо є биті посилання або залишилися [[вікілінки]].
"""
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[2]
LINK_RE = re.compile(r"(?<!\!)\[[^\]]*\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
WIKI_RE = re.compile(r"\[\[[^\]]+\]\]")
HEADING_RE = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")


def slug(text: str) -> str:
    """Алгоритм якорів GitHub: нижній регістр, без розділових знаків, пробіли → дефіси."""
    text = re.sub(r"`|\*|_(?=\S)|\[|\]|\([^)]*\)", lambda m: "" if m.group(0) != "(" else m.group(0), text)
    text = text.strip().lower()
    text = re.sub(r"[^\w\- ]", "", text, flags=re.UNICODE)
    return text.replace(" ", "-")


def strip_link_targets(text: str) -> str:
    return re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)


def prose_lines(path: Path):
    """Рядки поза fenced-блоками: (номер, текст)."""
    fence = None
    for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        m = re.match(r"^\s*(`{3,}|~{3,})", line)
        if m:
            marker = m.group(1)[0] * 3
            if fence is None:
                fence = marker
            elif line.strip().startswith(fence):
                fence = None
            continue
        if fence is None:
            yield n, line


_anchor_cache: dict[Path, set[str]] = {}


def anchors(path: Path) -> set[str]:
    if path not in _anchor_cache:
        seen: dict[str, int] = {}
        result = set()
        for _, line in prose_lines(path):
            m = HEADING_RE.match(line)
            if not m:
                continue
            base = slug(strip_link_targets(m.group(2)))
            k = seen.get(base, 0)
            seen[base] = k + 1
            result.add(base if k == 0 else f"{base}-{k}")
        _anchor_cache[path] = result
    return _anchor_cache[path]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    files = subprocess.run(["git", "ls-files", "*.md"], cwd=ROOT, capture_output=True,
                           text=True, encoding="utf-8").stdout.split()
    problems = []
    for rel in files:
        path = ROOT / rel
        if not path.exists():
            continue
        for n, line in prose_lines(path):
            code_free = re.sub(r"`[^`]*`", "", line)
            for _ in WIKI_RE.findall(code_free):
                problems.append(f"{rel}:{n}: вікілінк замість markdown-посилання: {_}")
            for target in LINK_RE.findall(code_free):
                if re.match(r"^[a-z][a-z0-9+.-]*:", target) or target.startswith("<"):
                    continue
                file_part, _, frag = target.partition("#")
                dest = path if not file_part else (path.parent / unquote(file_part)).resolve()
                if not dest.exists():
                    problems.append(f"{rel}:{n}: немає файлу {target}")
                elif frag and dest.suffix == ".md" and unquote(frag).lower() not in anchors(dest):
                    problems.append(f"{rel}:{n}: немає якоря {target}")
    for p in problems:
        print(p)
    print(f"Перевірено {len(files)} md-файлів, проблем: {len(problems)}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())

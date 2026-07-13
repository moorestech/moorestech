#!/usr/bin/env python3
"""moores-code-review Step 3: unified diffから発火レンズを選択する。

Usage: python3 select_lenses.py <PATCH_PATH>

lenses/*.md 先頭YAMLの各グループはAND結合（グループ内はOR、空グループは制約なし）:
    ---
    paths:            # 変更ファイルパスの正規表現
      - "Server\\.Protocol"
    extensions:
      - .cs
    keywords:         # diff追加行 or 変更ファイルパスへの部分一致
      - "DataStore"
    model: opus       # 省略時 opus
    always: true      # 無条件発火
    ---

出力: `<レンズ絶対パス>\t<モデル>` のTSV。
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

LENSES_DIR = Path(__file__).resolve().parent.parent / "lenses"


def parse_yaml_header(text: str) -> dict[str, list[str]]:
    # 依存を避けるための簡易YAMLパース（リストとスカラーのみ）
    # Minimal YAML parsing (lists and scalars only) to avoid dependencies
    if not text.startswith("---"):
        return {}
    end = text.find("\n---", 4)
    if end == -1:
        return {}
    result: dict[str, list[str]] = {}
    current_key: str | None = None
    for raw in text[4:end].splitlines():
        line = raw.rstrip()
        if not line.strip():
            continue
        if line.lstrip().startswith("- "):
            if current_key is not None:
                result.setdefault(current_key, []).append(
                    line.lstrip()[2:].strip().strip('"').strip("'"))
        elif ":" in line:
            key, _, inline = line.partition(":")
            current_key = key.strip()
            inline = inline.strip()
            if inline in ("[]", "{}"):
                result[current_key] = []
                current_key = None
            elif inline:
                result[current_key] = [inline.strip('"').strip("'")]
                current_key = None
            else:
                result[current_key] = []
    return result


def extract_changed_files_and_added(diff: str) -> tuple[list[str], str]:
    files: list[str] = []
    added: list[str] = []
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            files.append(line[6:])
        elif line.startswith("+++ ") and line[4:].strip() != "/dev/null":
            files.append(line[4:].strip())
        elif line.startswith("+") and not line.startswith("+++"):
            added.append(line[1:])
    return files, "\n".join(added)


def matches(header: dict[str, list[str]], files: list[str], added: str) -> bool:
    always = header.get("always", [])
    if always and always[0].strip().lower() == "true":
        return True
    paths = [p for p in header.get("paths", []) if p]
    exts = [e for e in header.get("extensions", []) if e]
    kws = [k for k in header.get("keywords", []) if k]
    path_ok = (not paths) or any(re.search(p, f) for p in paths for f in files)
    ext_ok = (not exts) or any(f.endswith(ext) for ext in exts for f in files)
    kw_ok = (not kws) or any(kw in added or any(kw in f for f in files) for kw in kws)
    return path_ok and ext_ok and kw_ok


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    diff = Path(argv[1]).read_text(encoding="utf-8", errors="replace")
    if not diff.strip():
        return 0
    files, added = extract_changed_files_and_added(diff)
    for md in sorted(LENSES_DIR.glob("*.md")):
        header = parse_yaml_header(md.read_text(encoding="utf-8"))
        if matches(header, files, added):
            model = (header.get("model") or ["opus"])[0]
            print(f"{md}\t{model}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

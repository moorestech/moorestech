#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
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
    keywords_re:      # diff追加行への正規表現一致。keywordsとはOR結合（どちらかが当たれば可）
      - "\\{\\s*get;\\s*set;\\s*\\}"
    keywords_all:     # keywords群と同じ対象への部分一致だが、列挙した全語の出現が必要（AND）
      - "MasterHolder"
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
    kws_re = [k for k in header.get("keywords_re", []) if k]
    kws_all = [k for k in header.get("keywords_all", []) if k]
    path_ok = (not paths) or any(re.search(p, f) for p in paths for f in files)
    ext_ok = (not exts) or any(f.endswith(ext) for ext in exts for f in files)
    # keywords と keywords_re は同一OR群（部分一致か正規表現のどちらかが当たれば可）
    # keywords and keywords_re form one OR group (substring or regex hit suffices)
    kw_sub_hit = any(kw in added or any(kw in f for f in files) for kw in kws)
    kw_re_hit = any(re.search(p, added, re.MULTILINE) for p in kws_re)
    kw_ok = (not kws and not kws_re) or kw_sub_hit or kw_re_hit
    # keywords_all は列挙全語の出現を要求（採用ゼロレンズの発火厳格化・2026-08-16裁定）
    # keywords_all requires every listed term to appear (stricter firing per 2026-08-16 adjudication)
    kw_all_ok = all(kw in added or any(kw in f for f in files) for kw in kws_all)
    return path_ok and ext_ok and kw_ok and kw_all_ok


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

#!/usr/bin/env python3
"""moores-code-review Step 2: moorestech固有規約の決定論チェック。

Usage: python3 deterministic_checks.py <PATCH_PATH> --repo-root <path>

出力JSON:
    {
      "confirmed":  [...],   # 検出正確・裏取り不要。Criticalとして統合に載せる
      "candidates": { "event_tag_sync": [...] }  # server-state-syncレンズの裏付けデータ
    }
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

# 検査パターン定義: (id, パス条件, 追加行の正規表現, メッセージ)
# Check patterns: (id, path predicate, added-line regex, message)
LINE_CHECKS = [
    ("master_default_fallback",
     lambda p: ("Core.Master" in p or "BlockTemplate" in p) and p.endswith(".cs"),
     re.compile(r"\?\?\s*\w*\.?Default[A-Z]|const\s+\w+\s+Default[A-Z]\w*\s*="),
     "マスタ欠損フォールバック禁止: Default定数・??補完はスキーマ必須化+JSON更新で解決する"),
    ("csharp_partial",
     lambda p: p.endswith(".cs"),
     re.compile(r"\bpartial\s+(class|struct|interface)\b"),
     "partial禁止（AGENTS.md）"),
    ("csharp_try_catch",
     lambda p: p.endswith(".cs") and "/Tests/" not in p,
     re.compile(r"^\s*try\s*(\{|$)|(^|\s)catch\s*[({]"),
     "try-catch原則禁止（AGENTS.md）: 条件分岐・nullチェックで対応する"),
]

# optional:trueは正当な例外（存在に意味があるフィールド）がありうるためcandidates扱い
# optional:true has legitimate exceptions (presence-meaningful fields) so it is a candidate
CANDIDATE_LINE_CHECKS = [
    ("schema_optional_true",
     lambda p: p.startswith("VanillaSchema/") and p.endswith(".yml"),
     re.compile(r"optional:\s*true"),
     "optional:true新設候補: 原則禁止（必須化+default+全JSON更新が正規手順）。「存在しないことに意味がある」フィールドのみ正当 — master-data-defenseレンズが裁定"),
]

EVENT_TAG_RE = re.compile(r'EventTag\s*=\s*"(va:event:[^"]+)"')


def parse_patch(text: str):
    # ファイルごとに (path, is_new, [(行番号, 追加行)]) を返す簡易パーサ
    # Minimal parser returning (path, is_new, [(lineno, added_line)]) per file
    files, path, is_new, was_new, added, lineno = [], None, False, False, [], 0
    for line in text.splitlines():
        if line.startswith("--- "):
            is_new = line[4:].strip() == "/dev/null"
        elif line.startswith("+++ "):
            if path is not None:
                files.append((path, was_new, added))
            raw = line[6:] if line.startswith("+++ b/") else line[4:].strip()
            path = None if raw == "/dev/null" else raw
            was_new, added = is_new, []
        elif line.startswith("@@"):
            m = re.search(r"\+(\d+)", line)
            lineno = int(m.group(1)) - 1 if m else 0
        elif line.startswith("+") and not line.startswith("+++"):
            lineno += 1
            added.append((lineno, line[1:]))
        elif not line.startswith("-"):
            lineno += 1
    if path is not None:
        files.append((path, was_new, added))
    return files


def check_lines(files, checks=None):
    findings = []
    for path, _, added in files:
        for check_id, path_pred, pattern, msg in (checks or LINE_CHECKS):
            if not path_pred(path):
                continue
            for lineno, text in added:
                if pattern.search(text):
                    findings.append({"check": check_id, "file": path, "line": lineno, "message": msg})
    return findings


def check_new_files(files, repo_root: Path):
    # 新規ファイルの配置規約: PacketResponse直下・10ファイル制限・200行制限
    # New-file placement rules: PacketResponse root, 10-file dir limit, 200-line limit
    findings = []
    for path, is_new, added in files:
        if not is_new or not path.endswith(".cs"):
            continue
        content = "\n".join(t for _, t in added)
        parent = str(Path(path).parent)
        if parent.endswith("Server.Protocol/PacketResponse") and "IPacketResponse" not in content:
            findings.append({"check": "packet_response_root", "file": path, "line": 1,
                             "message": "PacketResponse直下はIPacketResponse実装のみ。DTO/データクラスは別階層へ"})
        if len(added) > 200:
            findings.append({"check": "file_line_limit", "file": path, "line": 1,
                             "message": f"新規ファイルが200行超（{len(added)}行）。責務分割する（AGENTS.md）"})
        abs_dir = repo_root / parent
        if abs_dir.is_dir():
            cs_count = len(list(abs_dir.glob("*.cs")))
            if cs_count > 10:
                findings.append({"check": "dir_file_limit", "file": path, "line": 1,
                                 "message": f"1ディレクトリ10ファイル超（{parent} に{cs_count}個の.cs）。サブディレクトリへ構造分割（AGENTS.md）"})
    return findings


def check_event_tag_sync(files, patch_text: str, repo_root: Path):
    # 新規EventTagにクライアント購読が存在するか（diff内 or リポジトリ内）
    # For each new EventTag, verify a client-side subscription exists (in diff or repo)
    candidates = []
    client_root = repo_root / "moorestech_client" / "Assets" / "Scripts"
    for path, _, added in files:
        if "Server.Event" not in path:
            continue
        for lineno, text in added:
            m = EVENT_TAG_RE.search(text)
            if not m:
                continue
            tag = m.group(1)
            class_name = Path(path).stem
            subscribed = f"{class_name}.EventTag" in patch_text or tag in patch_text.replace(text, "")
            if not subscribed and client_root.is_dir():
                for cs in client_root.rglob("*.cs"):
                    src = cs.read_text(encoding="utf-8", errors="replace")
                    if f"{class_name}.EventTag" in src or tag in src:
                        subscribed = True
                        break
            if not subscribed:
                candidates.append({"check": "event_tag_sync", "file": path, "line": lineno,
                                   "message": f"新規イベント {tag} のクライアント購読（SubscribeEventResponse）が見つからない。3点セット（イベント+初期データ+購読）を確認"})
    return candidates


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    patch_text = Path(argv[1]).read_text(encoding="utf-8", errors="replace")
    repo_root = Path(argv[argv.index("--repo-root") + 1]).resolve() if "--repo-root" in argv else Path.cwd()
    files = parse_patch(patch_text)
    result = {
        "confirmed": check_lines(files) + check_new_files(files, repo_root),
        "candidates": {
            "event_tag_sync": check_event_tag_sync(files, patch_text, repo_root),
            "schema_optional_true": check_lines(files, CANDIDATE_LINE_CHECKS),
        },
    }
    json.dump(result, sys.stdout, ensure_ascii=False, indent=2)
    print()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

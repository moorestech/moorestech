#!/usr/bin/env python3
"""writing-plans の判断台帳関所（sim-gate.sh前例踏襲）。

track: plan（docs/superpowers/plans/*.md）へのWrite/Editを状態ファイルに記録
stop : 各planの frontmatter `spec:` を解決し、plan本文の Modify:/Create: 対象のうち
       lenses/*.md の paths 正規表現にマッチするファイルが、spec の判断台帳
       （## 判断記録（ADR） または ## 判断台帳）にbasenameで言及されているか検査。
       未掲載があれば exit 2 でブロック（自前カウンタ上限2で無限ブロック防止）。
"""
from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from select_lenses import parse_yaml_header  # noqa: E402

LENSES_DIR = Path(__file__).resolve().parent.parent / "lenses"
LEDGER_HEADINGS = ("## 判断記録（ADR）", "## 判断台帳")
TARGET_RE = re.compile(r"^\s*-\s*(?:Modify|Create):\s*`?([^`\s:]+)", re.MULTILINE)


def lens_path_patterns() -> list[str]:
    patterns: list[str] = []
    for md in sorted(LENSES_DIR.glob("*.md")):
        patterns.extend(p for p in parse_yaml_header(md.read_text(encoding="utf-8")).get("paths", []) if p)
    return patterns


def ledger_text(spec_path: Path) -> str:
    if not spec_path.is_file():
        return ""
    text = spec_path.read_text(encoding="utf-8", errors="replace")
    for heading in LEDGER_HEADINGS:
        idx = text.find(heading)
        if idx != -1:
            return text[idx:]
    return ""


def resolve_spec(plan_path: Path, spec_ref: str) -> Path:
    # 相対specはcwdでなくplan位置由来のリポジトリルートで解決する（worktree誤読防止）
    # Resolve relative spec against the repo root derived from the plan location, not cwd
    spec = Path(spec_ref)
    if spec.is_absolute():
        return spec
    parents = plan_path.resolve().parents
    if len(parents) >= 4:  # <root>/docs/superpowers/plans/<plan>.md
        candidate = parents[3] / spec_ref
        if candidate.is_file():
            return candidate
    return Path.cwd() / spec_ref


def missing_entries(plan_path: Path) -> list[str]:
    plan_text = plan_path.read_text(encoding="utf-8", errors="replace")
    spec_match = re.search(r"^spec:\s*(\S+)", plan_text, re.MULTILINE)
    if not spec_match:
        return [f"{plan_path.name}: frontmatterに spec: が無い（判断台帳の所在を特定できない）"]
    ledger = ledger_text(resolve_spec(plan_path, spec_match.group(1)))
    if not ledger:
        return [f"{plan_path.name}: spec {spec_match.group(1)} に判断台帳セクションが無い"]
    patterns = lens_path_patterns()
    missing: list[str] = []
    for target in dict.fromkeys(TARGET_RE.findall(plan_text)):
        if any(re.search(p, target) for p in patterns) and Path(target).name not in ledger:
            missing.append(f"{Path(target).name}（{target}）")
    return missing


def main() -> int:
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    data = json.load(sys.stdin) if not sys.stdin.isatty() else {}
    sid = data.get("session_id", "")
    if not sid:
        return 0
    state_dir = Path(os.environ.get("TMPDIR", "/tmp")) / "claude-ledger-gate"
    state_dir.mkdir(parents=True, exist_ok=True)
    plans_state = state_dir / f"{sid}.plans"
    blocks_state = state_dir / f"{sid}.blocks"

    if mode == "track":
        file_path = data.get("tool_input", {}).get("file_path", "")
        if "/docs/superpowers/plans/" in file_path and file_path.endswith(".md"):
            existing = plans_state.read_text().splitlines() if plans_state.is_file() else []
            if file_path not in existing:
                plans_state.write_text("\n".join(existing + [file_path]) + "\n")
        return 0

    if mode == "stop":
        if not plans_state.is_file():
            return 0
        count = int(blocks_state.read_text()) if blocks_state.is_file() else 0
        if count >= 2:
            return 0
        problems: list[str] = []
        for plan in plans_state.read_text().splitlines():
            if plan.strip():
                problems.extend(missing_entries(Path(plan)))
        if not problems:
            return 0
        blocks_state.write_text(str(count + 1))
        print(
            "ledger-gate: planのModify/Create対象にレンズpaths該当ファイルがありますが、"
            "specの判断台帳に未掲載です: " + " / ".join(problems)
            + " — specの『## 判断記録（ADR）』へagent前提として1行追記（対象ファイル名を含める）するか、"
            "plan frontmatterの spec: パスを修正してください。掲載なき判断は免責力を持ちません。",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())

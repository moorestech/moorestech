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
"""writing-plans の判断台帳関所（sim-gate.sh前例踏襲）。

track: plan（docs/superpowers/plans/*.md）へのWrite/Editを状態ファイルに記録
stop : 各planの先頭frontmatter `spec:` を解決し、plan本文の Modify:/Create: 対象のうち
       lenses/*.md の paths（＋extensions）にマッチするファイルが、specの判断台帳
       （## 判断記録（ADR）/ ## 判断台帳。次の##見出しまで）にbasenameで言及されて
       いるか検査。未掲載があれば exit 2 でブロック（自前カウンタ上限2）。
       レンズ該当対象が無いplanは spec: 欠落でもブロックしない（既存plan互換）。
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
LEDGER_HEADING_RE = re.compile(r"^##\s*(判断記録（ADR）|判断台帳)")
# checkbox・太字・行番号サフィックス付きの表記揺れも拾う（fail-open防止）
# Also match checkbox/bold variants and strip :line-range suffixes
TARGET_RE = re.compile(
    r"^\s*-\s*(?:\[[ xX]\]\s*)?(?:\*\*)?(?:Modify|Create):?(?:\*\*)?:?\s*`?([^`\s]+)",
    re.MULTILINE)


def lens_rules() -> list[tuple[list[str], list[str]]]:
    rules: list[tuple[list[str], list[str]]] = []
    for md in sorted(LENSES_DIR.glob("*.md")):
        header = parse_yaml_header(md.read_text(encoding="utf-8"))
        paths = [p for p in header.get("paths", []) if p]
        if paths:
            rules.append((paths, [e for e in header.get("extensions", []) if e]))
    return rules


def matches_lens(target: str, rules: list[tuple[list[str], list[str]]]) -> bool:
    for paths, exts in rules:
        if any(re.search(p, target) for p in paths) and (not exts or target.endswith(tuple(exts))):
            return True
    return False


def frontmatter_spec(plan_text: str) -> str | None:
    lines = plan_text.splitlines()
    if not lines or lines[0].strip() != "---":
        return None
    for line in lines[1:30]:
        if line.strip() == "---":
            return None
        m = re.match(r"^spec:\s*(\S+)", line)
        if m:
            return m.group(1)
    return None


def resolve_spec(plan_path: Path, spec_ref: str) -> Path | None:
    # 相対specはcwdでなくplan位置由来のリポジトリルートで解決する（worktree誤読防止）
    # Resolve relative spec against the repo root derived from the plan location, not cwd
    spec = Path(spec_ref)
    if spec.is_absolute():
        return spec if spec.is_file() else None
    parents = plan_path.resolve().parents
    if len(parents) >= 4:  # <root>/docs/superpowers/plans/<plan>.md
        candidate = parents[3] / spec_ref
        if candidate.is_file():
            return candidate
    candidate = Path.cwd() / spec_ref
    return candidate if candidate.is_file() else None


def ledger_text(spec_path: Path) -> str:
    lines = spec_path.read_text(encoding="utf-8", errors="replace").splitlines()
    start = None
    for i, line in enumerate(lines):
        if start is None and LEDGER_HEADING_RE.match(line.strip()):
            start = i + 1
        elif start is not None and line.startswith("## "):
            return "\n".join(lines[start:i])
    return "\n".join(lines[start:]) if start is not None else ""


def missing_entries(plan_path: Path, rules: list[tuple[list[str], list[str]]]) -> list[str]:
    plan_text = plan_path.read_text(encoding="utf-8", errors="replace")
    targets = [t.rstrip("`").split("`")[0] for t in dict.fromkeys(TARGET_RE.findall(plan_text))]
    gated = [re.sub(r":\d+(?:-\d+)?$", "", t) for t in targets]
    gated = [t for t in gated if matches_lens(t, rules)]
    if not gated:
        return []
    spec_ref = frontmatter_spec(plan_text)
    if not spec_ref:
        return [f"{plan_path.name}: レンズ該当対象があるのに先頭frontmatterに spec: が無い"]
    spec_path = resolve_spec(plan_path, spec_ref)
    if spec_path is None:
        return [f"{plan_path.name}: spec {spec_ref} が存在しない（frontmatterのパスを確認）"]
    ledger = ledger_text(spec_path)
    if not ledger:
        return [f"{plan_path.name}: spec {spec_ref} に判断台帳セクション（## 判断記録（ADR））が無い"]
    return [f"{Path(t).name}（{t}）" for t in gated if Path(t).name not in ledger]


def main() -> int:
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    raw = "" if sys.stdin.isatty() else sys.stdin.read()
    if not raw.strip():
        return 0
    # hooks stdin は外部境界のためJSON不正は素通し（sim-gate.shのfail-open前例に一致）
    # Hook stdin is an external boundary; malformed JSON falls open like sim-gate.sh
    try:
        data = json.loads(raw)
    except ValueError:
        return 0
    sid = data.get("session_id", "")
    if not sid:
        return 0
    state_dir = Path(os.environ.get("TMPDIR") or "/tmp") / "claude-ledger-gate"
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
        rules = lens_rules()
        problems: list[str] = []
        alive = [p for p in plans_state.read_text().splitlines() if p.strip() and Path(p).is_file()]
        plans_state.write_text("\n".join(alive) + ("\n" if alive else ""))
        for plan in alive:
            problems.extend(missing_entries(Path(plan), rules))
        if not problems:
            return 0
        blocks_state.write_text(str(count + 1))
        print(
            "ledger-gate: planのModify/Create対象にレンズpaths該当ファイルがありますが、"
            "specの判断台帳に未掲載です: " + " / ".join(problems)
            + " — specの『## 判断記録（ADR）』へ1行追記（対象ファイル名を含める）するか、"
            "plan frontmatterの spec: パスを修正してください。掲載なき判断は免責力を持ちません。",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())

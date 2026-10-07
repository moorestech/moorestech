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
"""ledger_gate stop 用: このセッション中に作られた・変更された plan を追跡に頼らず拾う。

PostToolUse(Write|Edit) の track だけでは Bash（cat > / python 書き出し）で作った plan が
素通りした（2026-10-01 exhibition-language-gate-cursor）。Stop 時に次の2経路で候補を集め、
「セッション開始以後に mtime が更新され、かつ git 上で base（origin/master との merge-base）から
変わっている／未追跡」のものだけを返す。git 条件は worktree 作成直後の checkout で全 plan の
mtime が新しくなっても旧 plan を巻き込まないため。
  1. transcript の全 tool_use 入力に現れる docs/superpowers/plans/*.md のパス
  2. セッション cwd が属する repo の docs/superpowers/plans/*.md 全件

Discover plans created or modified during the session without relying on PostToolUse tracking,
so plans written via Bash are gated too. A plan qualifies when its mtime is at/after the session
start and git reports it untracked or changed against merge-base(HEAD, origin/master).
"""
from __future__ import annotations

import json
import re
import subprocess
from datetime import datetime
from pathlib import Path

PLANS_REL = Path("docs/superpowers/plans")
CD_TARGET_RE = re.compile(r"""(?:\bcd|\bgit\s+-C)\s+("[^"]+"|'[^']+'|[^\s;&|]+)""")
PLAN_PATH_RE = re.compile(r"""((?:[^\s'"`<>|;&()]*/)?docs/superpowers/plans/[^\s'"`<>|;&()/]+\.md)""")


def transcript_records(transcript_path: str) -> list[dict]:
    path = Path(transcript_path) if transcript_path else None
    if path is None or not path.is_file():
        return []
    records = []
    # transcript はハーネスが書く外部ファイル。壊れた行は飛ばす
    # The transcript is written by the harness; skip malformed lines
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            rec = json.loads(line)
        except ValueError:
            continue
        if isinstance(rec, dict):
            records.append(rec)
    return records


def session_start(records: list[dict]) -> float | None:
    for rec in records:
        stamp = rec.get("timestamp")
        if isinstance(stamp, str):
            return datetime.fromisoformat(stamp.replace("Z", "+00:00")).timestamp()
    return None


def _strings(value) -> list[str]:
    if isinstance(value, str):
        return [value]
    if isinstance(value, dict):
        return [s for v in value.values() for s in _strings(v)]
    if isinstance(value, list):
        return [s for v in value for s in _strings(v)]
    return []


def mentioned_plan_paths(records: list[dict], cwd: Path) -> set[Path]:
    # assistant の tool_use 入力（Bash command・Write file_path 等）だけを見る
    # Only tool_use inputs of assistant messages (Bash command, Write file_path, ...)
    found: set[Path] = set()
    for rec in records:
        content = rec.get("message", {}).get("content") if rec.get("type") == "assistant" else None
        for block in content if isinstance(content, list) else []:
            if not isinstance(block, dict) or block.get("type") != "tool_use":
                continue
            for text in _strings(block.get("input", {})):
                # 相対パスはセッションcwdと、同じコマンド内の `cd <dir>`／`git -C <dir>` の両方で解決する
                # Resolve relative paths against the session cwd and any `cd <dir>` / `git -C <dir>` in the same text
                bases = [cwd] + [cwd / Path(d.strip("'\"")).expanduser() for d in CD_TARGET_RE.findall(text)]
                for raw in PLAN_PATH_RE.findall(text):
                    p = Path(raw).expanduser()
                    found.update([p] if p.is_absolute() else [b / p for b in bases])
    return found


def repo_plans(cwd: Path) -> set[Path]:
    for base in (cwd, *cwd.parents):
        plans_dir = base / PLANS_REL
        if plans_dir.is_dir():
            return set(plans_dir.glob("*.md"))
    return set()


def _git(root: Path, *args: str) -> str | None:
    proc = subprocess.run(["git", "-C", str(root), *args], capture_output=True, text=True, check=False)
    return proc.stdout if proc.returncode == 0 else None


def changed_plans_in_repo(any_path: Path) -> set[Path] | None:
    """repo 内で base から変わった／未追跡の plan 集合。git が使えなければ None（mtime だけで判定）。"""
    top = _git(any_path.parent, "rev-parse", "--show-toplevel")
    if top is None:
        return None
    root = Path(top.strip())
    base = (_git(root, "merge-base", "HEAD", "origin/master") or "").strip() or "HEAD"
    names = set()
    status = _git(root, "status", "--porcelain=v1", "-uall", "--", str(PLANS_REL)) or ""
    names |= {line[3:].strip().strip('"') for line in status.splitlines() if len(line) > 3}
    names |= set((_git(root, "diff", "--name-only", base, "--", str(PLANS_REL)) or "").split())
    return {(root / n).resolve() for n in names}


def session_plans(transcript_path: str, cwd: str) -> tuple[list[str], str]:
    """(このセッションで作成・変更された plan の絶対パス, 判定できなかった理由。空なら判定済み)。"""
    records = transcript_records(transcript_path)
    start = session_start(records)
    if start is None:
        return [], f"transcript からセッション開始時刻を読めない（{transcript_path or '未指定'}）"
    cwd_path = Path(cwd) if cwd else Path.cwd()
    candidates = mentioned_plan_paths(records, cwd_path) | repo_plans(cwd_path)
    fresh = [p.resolve() for p in candidates if p.is_file() and p.stat().st_mtime >= start]
    cache: dict[Path, set[Path] | None] = {}
    result = []
    for plan in sorted(set(fresh)):
        if plan.parent not in cache:
            cache[plan.parent] = changed_plans_in_repo(plan)
        changed = cache[plan.parent]
        if changed is None or plan in changed:
            result.append(str(plan))
    return result, ""

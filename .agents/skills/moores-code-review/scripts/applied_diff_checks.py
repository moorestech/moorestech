#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""applied_diff_checks.py — 反映 diff の機械的動作確認（orchestrator-steps.md「反映 diff の機械的動作確認」）。

1. 録画シナリオ: 反映前（snapshot の前側）と反映後の両方で録画シナリオ全件をコンパイルし（scenario_compile.py）、
   反映後にだけ増えた診断をこの反映の破壊として返す。反映前の診断が無ければ「未確認」で、既存扱いにしない。
   - record: 編集する前に、作業ツリーの現状を <run-dir>/refix/<name>-scenarios.json へ記録する。
   - check : 反映 diff が .cs（テスト以外）に触れていれば、反映後を <to> として記録し <from> の記録と比べる。
2. セーブ/ロード: 反映 diff が Save/Load・DataStore・Json 系に触れたら、既存のセーブ往復テストを選ぶ regex を返す。
   既存テストで覆えない経路は unverified として返す（新しいテスト基盤は作らない）。

Mechanical checks for an applied diff: before/after compile diagnostics of every playtest scenario, and the
save/load round-trip tests to run.

usage:
  applied_diff_checks.py record --repo-root R --run-dir D --name s0 [--if-missing]
  applied_diff_checks.py check <diff> --repo-root R --run-dir D --from s0 --to s1   → stdout に JSON
exit: 0=ok / 2=diff が読めない
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from refix_snapshot import is_non_source_path  # noqa: E402
from scenario_compile import compare, record  # noqa: E402

SAVE_LOAD_PATH_RE = re.compile(r"Save|Load|DataStore|Datastore|Json")
SAVE_LOAD_LINE_RE = re.compile(r"\b\w*(?:Save|Load)\w*\s*\(|Json|DataStore|Datastore")
# 既存のセーブ往復テスト（サーバー側・EditMode）。ブロック・電線・チェーン・接続を含むワールドのセーブ→再ロードを覆う
# Existing save/load round-trip tests (server, EditMode) covering blocks, wires, chains and connections
SAVE_LOAD_TEST_REGEX = r"Tests\.(UnitTest|CombinedTest)\..*(SaveLoad|SaveJsonFile)"
SAVE_LOAD_UNVERIFIED = ["クライアントが線・チェーンを含むセーブから起動する経路（クライアント起動を通す既存の自動テストが無い）"]


def parse_diff(text: str) -> dict[str, dict[str, list[str]]]:
    files: dict[str, dict[str, list[str]]] = {}
    current = None
    for line in text.splitlines():
        m = re.match(r"^diff --git a/(.*) b/(.*)$", line)
        if m:
            current = files.setdefault(m.group(2), {"added": [], "removed": []})
            continue
        if current is None or line.startswith(("+++", "---")):
            continue
        if line.startswith("+"):
            current["added"].append(line[1:])
        elif line.startswith("-"):
            current["removed"].append(line[1:])
    return files


def touches_cs(files: dict) -> list[str]:
    # 録画シナリオ自体の追従編集も対象（シナリオは .agents 配下で、テスト扱いの path ではない）
    # Scenario edits count too (scenarios live under .agents and are not test paths)
    return sorted(p for p in files if p.endswith(".cs") and not is_non_source_path(p))


def save_load(files: dict) -> dict:
    touched = sorted(p for p, f in files.items() if p.endswith(".cs") and (
        SAVE_LOAD_PATH_RE.search(p) or any(SAVE_LOAD_LINE_RE.search(l) for l in f["added"] + f["removed"])))
    return {"touched": bool(touched), "files": touched,
            "test_regex": SAVE_LOAD_TEST_REGEX if touched else None,
            "unverified": SAVE_LOAD_UNVERIFIED if touched else []}


def record_path(run_dir: Path, name: str) -> Path:
    return run_dir / "refix" / f"{name}-scenarios.json"


def cmd_record(args) -> int:
    out = record_path(Path(args.run_dir).resolve(), args.name)
    if args.if_missing and out.is_file():
        data = json.loads(out.read_text(encoding="utf-8"))
        print(json.dumps({"record": str(out), "status": data.get("status"), "reused": True}, ensure_ascii=False))
        return 0
    data = record(Path(args.repo_root).resolve(), out)
    print(json.dumps({"record": str(out), "status": data["status"], "reason": data["reason"],
                      "scenarios": len(data["scenarios"])}, ensure_ascii=False))
    return 0


def cmd_check(args) -> int:
    diff_path, repo_root, run_dir = Path(args.diff), Path(args.repo_root).resolve(), Path(args.run_dir).resolve()
    if not diff_path.is_file():
        print(f"diff が無い: {diff_path}", file=sys.stderr)
        return 2
    files = parse_diff(diff_path.read_text(encoding="utf-8", errors="replace"))
    cs_files = touches_cs(files)
    if cs_files:
        before_path = record_path(run_dir, args.src)
        before = json.loads(before_path.read_text(encoding="utf-8")) if before_path.is_file() else None
        after = record(repo_root, record_path(run_dir, args.dst))
        scenarios = compare(before, after)
        scenarios.update({"cs_files": cs_files, "before": str(before_path), "after": str(record_path(run_dir, args.dst))})
    else:
        scenarios = {"required": False, "status": "not_required", "new": [], "unverified": []}
    print(json.dumps({"scenarios": scenarios, "save_load": save_load(files)}, ensure_ascii=False, indent=1))
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("record")
    r.add_argument("--repo-root", required=True)
    r.add_argument("--run-dir", required=True)
    r.add_argument("--name", required=True)
    r.add_argument("--if-missing", action="store_true", help="記録が既にあれば取り直さない")
    c = sub.add_parser("check")
    c.add_argument("diff")
    c.add_argument("--repo-root", required=True)
    c.add_argument("--run-dir", required=True)
    c.add_argument("--from", dest="src", required=True)
    c.add_argument("--to", dest="dst", required=True)
    args = ap.parse_args(argv)
    for name in (getattr(args, "name", None), getattr(args, "src", None), getattr(args, "dst", None)):
        if name is not None and not re.fullmatch(r"[A-Za-z0-9_.-]+", name):
            print("snapshot 名は英数字・_ . - のみ", file=sys.stderr)
            return 2
    return cmd_record(args) if args.cmd == "record" else cmd_check(args)


if __name__ == "__main__":
    sys.exit(main())

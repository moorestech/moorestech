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
"""check_all.py — スクリプト実行系の統一窓口。

1コマンドで機械チェック全観点を同時に実行し、単一JSONに束ねる:
  - deterministic_checks.py  (confirmed/candidates: 規約の機械判定)
  - dead_member_gate.py      (IL解析: 参照0・非production参照のみ)
  - ts_dead_code_gate.py     (knip: webui死コード・テスト専用参照)
  - select_lenses.py / select_reviewers.py (発火する観点とモデル)
さらに candidates の件数から「起動すべきverifier」を計算して明示する。
オーケストレータはこの出力だけでStep 2〜4の全機械層を把握できる。

Single entry point: runs every mechanical check at once and merges the
results (plus which verifiers to launch) into one JSON for the orchestrator.

使い方 / Usage:
  python3 check_all.py <PATCH> --repo-root <ROOT> [--context <CONTEXT_MD>] > result.json
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent

# candidates種別 → 起動すべきverifier（SKILL.md Step 4と一致させる）
# Candidate kind → verifier to launch (kept in sync with SKILL.md Step 4)
VERIFIER_MAP = {
    "comparison_operator": ("verifiers/comparison-operator-verifier.md", "sonnet"),
    "try_catch_boundary": ("verifiers/try-catch-boundary-verifier.md", "opus"),
    "server_elapsed_time": ("verifiers/server-elapsed-time-verifier.md", "sonnet"),
    "dead_member": ("verifiers/dead-member-verifier.md", "sonnet"),
    "ts_dead_code": ("verifiers/ts-dead-code-verifier.md", "sonnet"),
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patch")
    ap.add_argument("--repo-root", required=True)
    ap.add_argument("--context", default=None)
    ap.add_argument("--skip-dead-member", action="store_true",
                    help="IL解析ゲートを飛ばす（dotnet不在環境など） / Skip the IL gate")
    args = ap.parse_args()

    result = {
        "deterministic": run_deterministic(args),
        "dead_member": run_dead_member(args),
        "ts_dead_code": run_ts_dead_code(args),
        "lenses": run_selector("select_lenses.py", args.patch),
        "reviewers": run_selector("select_reviewers.py", args.patch),
    }
    result["verifiers_to_launch"] = plan_verifiers(result)
    result["summary"] = summarize(result)
    print(json.dumps(result, ensure_ascii=False, indent=1))
    return 0


def run_deterministic(args) -> dict:
    cmd = [sys.executable, str(SCRIPTS / "deterministic_checks.py"),
           args.patch, "--repo-root", args.repo_root]
    if args.context:
        cmd += ["--context", args.context]
    return run_json(cmd, "deterministic_checks")


def run_dead_member(args) -> dict:
    if args.skip_dead_member:
        return {"status": "skipped", "note": "--skip-dead-member指定", "candidates": []}
    cmd = [sys.executable, str(SCRIPTS / "dead_member_gate.py"),
           args.patch, "--repo-root", args.repo_root]
    return run_json(cmd, "dead_member_gate")


def run_ts_dead_code(args) -> dict:
    cmd = [sys.executable, str(SCRIPTS / "ts_dead_code_gate.py"),
           args.patch, "--repo-root", args.repo_root]
    return run_json(cmd, "ts_dead_code_gate")


def run_selector(script: str, patch: str) -> list:
    # セレクタのTSV（パス<TAB>モデル）を構造化する / Structure the selector TSV output
    run = subprocess.run([sys.executable, str(SCRIPTS / script), patch],
                         capture_output=True, text=True, timeout=120)
    if run.returncode != 0:
        return [{"error": run.stderr.strip()[:300]}]
    rows = []
    for line in run.stdout.splitlines():
        if "\t" in line:
            path, model = line.split("\t", 1)
            rows.append({"path": path.strip(), "model": model.strip()})
    return rows


def run_json(cmd: list, label: str) -> dict:
    run = subprocess.run(cmd, capture_output=True, text=True, timeout=900)
    if run.returncode != 0:
        return {"error": f"{label}失敗: {run.stderr.strip()[:300]}"}
    out = run.stdout.strip()
    if not out.startswith("{"):
        return {"error": f"{label}が非JSONを返した: {out[:200]}"}
    return json.loads(out)


def plan_verifiers(result: dict) -> list:
    # 候補が1件以上あるverifierだけを起動対象として列挙する
    # List only the verifiers whose candidate kind has at least one entry
    plans = []
    candidates = result["deterministic"].get("candidates", {})
    for kind, (path, model) in VERIFIER_MAP.items():
        if kind in ("dead_member", "ts_dead_code"):
            count = len((result.get(kind) or {}).get("candidates", []))
        else:
            count = len(candidates.get(kind, []))
        if count > 0:
            plans.append({"verifier": path, "model": model,
                          "candidate_kind": kind, "count": count})
    return plans


def summarize(result: dict) -> dict:
    det = result["deterministic"]
    return {
        "confirmed": len(det.get("confirmed", [])),
        "candidates_total": sum(len(v) for v in det.get("candidates", {}).values()),
        "dead_member_status": result["dead_member"].get("status", "error"),
        "dead_member_candidates": len(result["dead_member"].get("candidates", [])),
        "ts_dead_code_status": result["ts_dead_code"].get("status", "error"),
        "ts_dead_code_candidates": len(result["ts_dead_code"].get("candidates", [])),
        "lenses": len([l for l in result["lenses"] if "path" in l]),
        "reviewers": len([r for r in result["reviewers"] if "path" in r]),
        "verifiers_to_launch": len(result["verifiers_to_launch"]),
        # セレクタの失敗（error 行）も errors へ集約する。落とすと「レビュアー0体」が成功として通る（2026-08-20 C3）
        # Selector failures are collected too; otherwise a zero-reviewer review passes as success
        "errors": [v.get("error") for v in
                   (det, result["dead_member"], result["ts_dead_code"]) if v.get("error")]
                  + [row["error"] for key in ("lenses", "reviewers") for row in result[key] if "error" in row],
    }


if __name__ == "__main__":
    sys.exit(main())

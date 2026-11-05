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
"""build_workflow_args.py — Workflow へ渡す args と、結合済み Workflow スクリプトを書く。

mode=bug-pass（`--bug-pass`・SKILL.md Step 7.5）: 系統を workflow_args/bug_pass.py の許可集合へ絞り、
context から免責の節を外し、前周 Warning の出所（`--carry-from` の本レビュー $RUNDIR）を渡す。


Builds the Workflow args JSON (review or bug-pass mode), writes the output contract and the
concatenated workflow script into the run dir, and fails closed on any selection error.

usage:
  build_workflow_args.py --run-dir <RUNDIR> --patch <PATCH> --context <CONTEXT>
      --repo-root <REPO> [--checks <checks.json>] [--chunks <chunks.tsv>]
      [--base-ref <sha>]
      [--bug-pass --carry-from <本レビューの RUNDIR>]
  → <RUNDIR>/workflow-args.json と <RUNDIR>/review_workflow.js を書き、args のパスを標準出力へ
exit: 0=ok / 2=引数の組み合わせ不正 / 3=checks.json の errors 非空 / 4=セレクタ error 行あり / 5=入力欠損・選択失敗
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from workflow_args import bug_pass  # noqa: E402
from workflow_args.assemble import workflow_script_path, write_workflow_script  # noqa: E402
from workflow_args.systems import (  # noqa: E402
    CODEX_KINDS, SCRIPTS, SKILL_ROOT, codex_jobs, expected_systems, fable_generalist,
    investigators, resolve_chunks, system, verifiers, write_contract)

CODEX_WAIT_MAX_MINUTES = 20
# 反映 diff 再レビューの周回上限。指摘は尽きないので収束条件は「再現可能な誤動作が無いこと」、超えたら未収束として親へ渡す
# Cap on re-review rounds; findings never run out, so convergence is 'no reproducible wrong behavior'
REFIX_MAX_ROUNDS = 3


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--run-dir", required=True)
    ap.add_argument("--patch", required=True)
    ap.add_argument("--context", required=True)
    ap.add_argument("--repo-root", required=True)
    ap.add_argument("--checks", default=None, help="既定: <run-dir>/checks.json（bug-pass では使わない）")
    ap.add_argument("--chunks", default=None,
                    help="既定: <run-dir>/chunks.tsv（既定パスの不在は below-threshold 扱い。明示指定の不在はエラー）")
    ap.add_argument("--base-ref", default=None, help="final.diff の比較元（Step 1 の base コミット）")
    ap.add_argument("--verify-by-repro", action="store_true", help="採用ロジックバグだけ隔離環境で反証・実証する")
    ap.add_argument("--bug-pass", action="store_true", help="SKILL.md Step 7.5 の最終バグ確認パス")
    ap.add_argument("--carry-from", default=None, help="bug-pass 用。前周 Warning を持ち込む本レビューの $RUNDIR")
    args = ap.parse_args()

    if args.bug_pass and not args.carry_from:
        print("--bug-pass は --carry-from 必須", file=sys.stderr)
        return 2
    run_dir = Path(args.run_dir).resolve()
    (run_dir / "agents").mkdir(parents=True, exist_ok=True)
    plan = plan_bug_pass(args, run_dir) if args.bug_pass else plan_review(args, run_dir)
    if isinstance(plan, int):
        return plan

    payload = {
        "mode": "bug-pass" if args.bug_pass else "review",
        "runDir": str(run_dir),
        "patchPath": str(Path(args.patch).resolve()),
        "repoRoot": str(Path(args.repo_root).resolve()),
        "skillRoot": str(SKILL_ROOT),
        "baseRef": args.base_ref,
        "verifyByRepro": args.verify_by_repro,
        "reproVerifierPath": str(SKILL_ROOT / "verifiers/logic-bug-repro.md"),
        "codexWaitMaxMinutes": CODEX_WAIT_MAX_MINUTES,
        "integratorPath": str(SKILL_ROOT / "integrators" / "finding-integrator.md"),
        "orchestratorStepsPath": str(SKILL_ROOT / "references" / "orchestrator-steps.md"),
        "selectPostChecksScript": str(SCRIPTS / "select_post_checks.py"),
        "deterministicChecksScript": str(SCRIPTS / "deterministic_checks.py"),
        "codexRecoverScript": str(SCRIPTS / "codex_recover.py"),
        "appliedDiffChecksScript": str(SCRIPTS / "applied_diff_checks.py"),
        "integrationRulesPath": str(SKILL_ROOT / "references" / "integration-rules.md"),
        # 反映 diff の再レビュー（Refix フェーズ）。reviewer は post-checks の applied-diff-correctness を流用する（2026-09-08 c9baa79 較正の実行形）
        # Applied-diff re-review (Refix phase); reuses the applied-diff-correctness post-check as the reviewer
        "refixReviewerPath": str(SKILL_ROOT / "post-checks" / "applied-diff-correctness.md"),
        "refixSnapshotScript": str(SCRIPTS / "refix_snapshot.py"),
        "refixMaxRounds": REFIX_MAX_ROUNDS,
        "workflowScriptPath": str(workflow_script_path(run_dir)),
        **plan,
    }
    write_workflow_script(run_dir, payload)
    out = run_dir / "workflow-args.json"
    out.write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")
    print(str(out))
    return 0


def plan_review(args, run_dir: Path) -> dict | int:
    checks_path = Path(args.checks) if args.checks else run_dir / "checks.json"
    if not checks_path.is_file():
        print(f"checks.json が無い: {checks_path}（check_all.py を先に実行する）", file=sys.stderr)
        return 5
    checks = json.loads(checks_path.read_text(encoding="utf-8"))
    errors = (checks.get("summary") or {}).get("errors") or []
    if errors:
        print(f"checks.json の summary.errors が空でない: {errors}", file=sys.stderr)
        return 3

    # セレクタの失敗は error 行として混ざる。1行でもあれば「選択が壊れている」ので先へ進めない
    # Selector failures arrive as error rows; even one means selection is broken, so stop here
    selector_errors = [row["error"] for row in checks.get("reviewers", []) if "error" in row]
    if selector_errors:
        print(f"セレクタが失敗している（reviewer が0体になる）: {selector_errors}", file=sys.stderr)
        return 4

    chunks_path, chunks_reason = resolve_chunks(args.chunks, run_dir)
    if chunks_path is None and chunks_reason.startswith("error"):
        print(chunks_reason, file=sys.stderr)
        return 5

    systems = [system("rev", row) for row in checks.get("reviewers", [])]
    systems.append(fable_generalist())
    systems += investigators(chunks_path, None)
    systems += verifiers(checks.get("verifiers_to_launch", []))

    contract = write_contract(run_dir, "")
    return {
        "userPromptPath": str(Path(args.context).resolve()),
        "checksPath": str(checks_path.resolve()),
        "chunksTsv": str(chunks_path.resolve()) if chunks_path else None,
        "chunksReason": chunks_reason,
        "contractPath": str(contract),
        "systems": systems,
        "expectedSystems": expected_systems(checks, systems),
        "codexJobs": codex_jobs(run_dir, CODEX_KINDS),
    }


def plan_bug_pass(args, run_dir: Path) -> dict | int:
    patch = Path(args.patch).resolve()
    reviewers = bug_pass.select_reviewers(patch)
    sources = bug_pass.carried_warning_sources(Path(args.carry_from).resolve())
    if reviewers is None or sources is None:
        return 5
    chunks_path = bug_pass.write_chunks(patch, run_dir)
    systems = [system("rev", row) for row in reviewers]
    systems += investigators(chunks_path, set(bug_pass.BUG_PASS_INVESTIGATORS))
    if not systems:
        print("bug-pass の系統が0件（patch が空か、レビュー対象ファイルが無い）", file=sys.stderr)
        return 5
    investigator_count = len(systems) - len(reviewers)
    context = bug_pass.strip_exemptions(Path(args.context), run_dir)
    bug_pass.carry_decisions(Path(args.carry_from).resolve(), context)
    return {
        "userPromptPath": str(context),
        "checksPath": None,
        "chunksTsv": str(chunks_path) if chunks_path else None,
        "chunksReason": f"bug-pass(split_chunks --threshold={bug_pass.BUG_PASS_CHUNK_THRESHOLD})",
        "contractPath": str(write_contract(run_dir, bug_pass.CONTRACT_ADDENDUM)),
        "systems": systems,
        "expectedSystems": bug_pass.expected_systems(reviewers, investigator_count),
        "codexJobs": codex_jobs(run_dir, bug_pass.BUG_PASS_CODEX_KINDS),
        "carriedWarningSources": sources,
    }


if __name__ == "__main__":
    sys.exit(main())

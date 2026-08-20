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
"""build_workflow_args.py — Workflow（scripts/review_workflow.js）へ渡す args を組み立てる。

Step 2 の checks.json（lenses / reviewers / verifiers_to_launch）と split_chunks の
chunks.tsv、investigators/ の YAML model、Fable全般、post-check を、起動名・モデル・
絶対パス付きの1つの JSON に畳む。あわせて共通出力契約 `references/output-contract.md`
を `$RUNDIR/contract.md` へ書く（report-only ではその旨の前提を末尾に足す）。
Workflow スクリプトはファイルシステムを持たないので、選択の実行はここで済ませ、
JS 側は「起動・再起動・統合・適用の順序」だけを担う。

Builds the args JSON for scripts/review_workflow.js from checks.json, chunks.tsv,
investigator YAML models, the Fable generalist and post-checks, and writes the
shared output contract into $RUNDIR/contract.md. The workflow script has no
filesystem, so all selection happens here and the JS only sequences launches.

usage:
  build_workflow_args.py --run-dir <RUNDIR> --patch <PATCH> --context <CONTEXT>
      --repo-root <REPO> [--checks <checks.json>] [--chunks <chunks.tsv>]
      [--base-ref <sha>] [--report-only] [--detchecks <detchecks.json>]
  → <RUNDIR>/workflow-args.json を書き、そのパスを標準出力へ
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = SKILL_ROOT / "scripts"
MODEL_RE = re.compile(r"^model:\s*(\S+)", re.M)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--run-dir", required=True)
    ap.add_argument("--patch", required=True)
    ap.add_argument("--context", required=True)
    ap.add_argument("--repo-root", required=True)
    ap.add_argument("--checks", default=None, help="既定: <run-dir>/checks.json")
    ap.add_argument("--chunks", default=None, help="既定: <run-dir>/chunks.tsv（無ければ第6系統なし）")
    ap.add_argument("--base-ref", default=None, help="final.diff の比較元（Step 1 の base コミット）")
    ap.add_argument("--report-only", action="store_true",
                    help="pr-independent-review 用。Step 6 適用を省き post-check は patch で発火")
    ap.add_argument("--detchecks", default=None,
                    help="report-only 時に convention-guard へ渡す決定論JSON（既定: <run-dir>/detchecks.json）")
    args = ap.parse_args()

    run_dir = Path(args.run_dir).resolve()
    checks_path = Path(args.checks) if args.checks else run_dir / "checks.json"
    chunks_path = Path(args.chunks) if args.chunks else run_dir / "chunks.tsv"
    checks = json.loads(checks_path.read_text(encoding="utf-8"))
    errors = (checks.get("summary") or {}).get("errors") or []
    if errors:
        print(f"checks.json の summary.errors が空でない: {errors}", file=sys.stderr)
        return 2

    (run_dir / "agents").mkdir(parents=True, exist_ok=True)
    contract_path = write_contract(run_dir, args.report_only)

    systems = []
    systems += [system("lens", row) for row in checks.get("lenses", []) if "path" in row]
    systems += [system("rev", row) for row in checks.get("reviewers", []) if "path" in row]
    systems.append(fable_generalist())
    systems += investigators(chunks_path)
    systems += verifiers(checks.get("verifiers_to_launch", []))

    post_checks = []
    if args.report_only:
        # 適用が無いので最終diff＝patch・候補＝Step 2 の決定論JSON（PIR Step 6 の規定どおり）
        # No apply step: final diff is the patch and candidates are the Step 2 deterministic JSON
        det = Path(args.detchecks) if args.detchecks else run_dir / "detchecks.json"
        post_checks = select_post_checks(Path(args.patch), det)

    payload = {
        "runDir": str(run_dir),
        "patchPath": str(Path(args.patch).resolve()),
        "userPromptPath": str(Path(args.context).resolve()),
        "repoRoot": str(Path(args.repo_root).resolve()),
        "skillRoot": str(SKILL_ROOT),
        "checksPath": str(checks_path.resolve()),
        "chunksTsv": str(chunks_path.resolve()) if chunks_path.is_file() else None,
        "contractPath": str(contract_path),
        "baseRef": args.base_ref,
        "reportOnly": bool(args.report_only),
        "systems": systems,
        "postChecks": post_checks,
        "integratorPath": str(SKILL_ROOT / "integrators" / "finding-integrator.md"),
        "orchestratorStepsPath": str(SKILL_ROOT / "references" / "orchestrator-steps.md"),
        "selectPostChecksScript": str(SCRIPTS / "select_post_checks.py"),
        "deterministicChecksScript": str(SCRIPTS / "deterministic_checks.py"),
        "codexRecoverScript": str(SCRIPTS / "codex_recover.py"),
    }
    out = run_dir / "workflow-args.json"
    out.write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")
    print(str(out))
    return 0


def system(kind: str, row: dict) -> dict:
    path = Path(row["path"])
    return {"kind": kind, "name": f"{kind}-{path.stem}", "path": str(path), "model": row["model"]}


def fable_generalist() -> dict:
    path = SKILL_ROOT / "generalists" / "fable-holistic-review.md"
    return {"kind": "fable", "name": "fable-holistic-review", "path": str(path),
            "model": read_model(path, "fable")}


def investigators(chunks_path: Path) -> list:
    # split_chunks が below-threshold なら chunks.tsv は空・不在 → 第6系統は不発火
    # Empty/absent chunks.tsv means below-threshold: the sixth system does not fire
    if not chunks_path.is_file():
        return []
    rows = [l.split("\t") for l in chunks_path.read_text(encoding="utf-8").splitlines()
            if l.strip() and "\t" in l]
    result = []
    for chunk_id, _label, files in (r[:3] for r in rows if len(r) >= 3):
        for md in sorted((SKILL_ROOT / "investigators").glob("*.md")):
            short = md.stem.replace("chunk-", "")
            result.append({"kind": "investigator", "name": f"investigator-{chunk_id}-{short}",
                           "path": str(md), "model": read_model(md, "sonnet"),
                           "chunkId": chunk_id, "chunkFiles": files})
    return result


def verifiers(plans: list) -> list:
    result = []
    for plan in plans:
        path = Path(plan["verifier"])
        if not path.is_absolute():
            path = SKILL_ROOT / path
        result.append({"kind": "verifier", "name": f"verifier-{path.stem}", "path": str(path),
                       "model": plan["model"], "candidateKind": plan.get("candidate_kind"),
                       "count": plan.get("count")})
    return result


def select_post_checks(diff_path: Path, checks_json: Path) -> list:
    if not checks_json.is_file():
        return []
    run = subprocess.run([sys.executable, str(SCRIPTS / "select_post_checks.py"),
                          str(diff_path), str(checks_json)],
                         capture_output=True, text=True, timeout=120)
    rows = []
    for line in run.stdout.splitlines():
        if "\t" in line:
            path, model = line.split("\t", 1)
            rows.append({"kind": "postcheck", "name": f"postcheck-{Path(path).stem}",
                         "path": path.strip(), "model": model.strip()})
    return rows


def read_model(md: Path, default: str) -> str:
    m = MODEL_RE.search(md.read_text(encoding="utf-8")[:400])
    return m.group(1).strip() if m else default


def write_contract(run_dir: Path, report_only: bool) -> Path:
    text = (SKILL_ROOT / "references" / "output-contract.md").read_text(encoding="utf-8")
    if report_only:
        text += ("\n重要な前提（report-only）: コードへの修正適用はしない。指摘はすべて報告ファイルへ出す。"
                 "PRが差分自身で追加したADR・.decisions/由来の免責は `[agent前提]`（免責力なし）へ降格済みで、"
                 "免責としてそのまま採用しない。\n")
    target = run_dir / "contract.md"
    target.write_text(text, encoding="utf-8")
    return target


if __name__ == "__main__":
    sys.exit(main())

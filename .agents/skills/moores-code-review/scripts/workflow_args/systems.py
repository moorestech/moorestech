# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""workflow_args/systems.py — build_workflow_args.py が組む起動計画の部品。

checks.json の reviewer 行・investigators/ の YAML model・verifier 計画・post-check 選択・
Codex 成果物パス・共通出力契約を、Workflow が起動できる dict へ畳む。fail-closed の判定
（YAML model 欠落・select_post_checks 失敗）はここで止める。

Building blocks for build_workflow_args.py: turns reviewer rows, investigator YAML models,
verifier plans, post-check selection, Codex artifact paths and the output contract into
launchable dicts, failing closed on missing models or selector failures.
"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPTS = SKILL_ROOT / "scripts"
MODEL_RE = re.compile(r"^model:\s*(\S+)", re.M)
CODEX_KINDS = ("audit", "bughunt", "design")


def resolve_chunks(explicit: str | None, run_dir: Path) -> tuple[Path | None, str]:
    # 明示指定の不在は実行忘れ・別パス書き出しの疑いなのでエラー。既定パスの不在だけ below-threshold 扱い
    # An explicitly named missing file is an error (forgotten run / wrong path); only the default may be absent
    if explicit:
        p = Path(explicit)
        if not p.is_file():
            return None, f"error: --chunks で指定した {p} が無い（split_chunks.py を先に実行する）"
        return p, "explicit"
    p = run_dir / "chunks.tsv"
    if not p.is_file():
        return None, "default-path-missing(below-threshold 扱い。split_chunks.py の stderr を確認すること)"
    return p, "default"


def system(kind: str, row: dict) -> dict:
    path = Path(row["path"])
    return {"kind": kind, "name": f"{kind}-{path.stem}", "path": str(path), "model": row["model"]}


def fable_generalist() -> dict:
    path = SKILL_ROOT / "generalists" / "fable-holistic-review.md"
    return {"kind": "fable", "name": "fable-holistic-review", "path": str(path), "model": read_model(path)}


def investigators(chunks_path: Path | None, only: set[str] | None) -> list:
    # split_chunks が below-threshold なら chunks.tsv は空・不在 → 第5系統は不発火。only はファイル stem の許可集合（None=全観点）
    # Empty/absent chunks.tsv means below-threshold; `only` restricts investigator stems (None = all)
    if chunks_path is None:
        return []
    rows = [l.split("\t") for l in chunks_path.read_text(encoding="utf-8").splitlines()
            if l.strip() and "\t" in l]
    mds = [md for md in sorted((SKILL_ROOT / "investigators").glob("*.md")) if only is None or md.stem in only]
    result = []
    for chunk_id, _label, files in (r[:3] for r in rows if len(r) >= 3):
        for md in mds:
            short = md.stem.replace("chunk-", "")
            result.append({"kind": "investigator", "name": f"investigator-{chunk_id}-{short}",
                           "path": str(md), "model": read_model(md),
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


def expected_systems(checks: dict, systems: list) -> dict:
    # 本体の検死①用: checks.json 由来の独立した期待値（args 自身の長さと循環比較しないため）
    # Independent expectation for the parent's post-mortem (never compare args against itself)
    summary = checks.get("summary") or {}
    investigator_count = len([s for s in systems if s["kind"] == "investigator"])
    return {
        "reviewers": summary.get("reviewers", len(checks.get("reviewers", []))),
        "verifiers": len(checks.get("verifiers_to_launch", [])),
        "fable": 1,
        "investigators": investigator_count,
        "total": summary.get("reviewers", 0)
        + len(checks.get("verifiers_to_launch", [])) + 1 + investigator_count,
    }


def select_post_checks(diff_path: Path, checks_json: Path) -> list | None:
    run = subprocess.run([sys.executable, str(SCRIPTS / "select_post_checks.py"),
                          str(diff_path), str(checks_json)],
                         capture_output=True, text=True, timeout=120)
    if run.returncode != 0:
        print(f"select_post_checks.py が失敗: {run.stderr.strip()[:300]}", file=sys.stderr)
        return None
    rows = []
    for line in run.stdout.splitlines():
        if "\t" in line:
            path, model = line.split("\t", 1)
            rows.append({"kind": "postcheck", "name": f"postcheck-{Path(path).stem}",
                         "path": path.strip(), "model": model.strip()})
    return rows


def codex_jobs(run_dir: Path, kinds: tuple[str, ...]) -> list:
    # 本体が起動した Codex の成果物パス。プロンプトが無い種類は起動されていない（対象外）
    # Artifact paths of the Codex jobs the parent launched; kinds without a prompt were not launched
    jobs = []
    for kind in kinds:
        prompt = run_dir / f"codex-{kind}.md"
        if prompt.is_file():
            jobs.append({"name": f"codex-{kind}", "prompt": str(prompt),
                         "out": str(run_dir / f"codex-{kind}.out.md"),
                         "final": str(run_dir / f"codex-{kind}.final.md")})
    return jobs


def read_model(md: Path) -> str:
    # YAML の model が正。見つからなければ暗黙の既定へ落とさず止める（無言のモデル差し替え防止）
    # The YAML model is authoritative; never fall back silently to an implicit default
    m = MODEL_RE.search(md.read_text(encoding="utf-8"))
    if not m:
        raise SystemExit(f"{md} に `model:` が無い（先頭YAMLで指定すること）")
    return m.group(1).strip()


def write_contract(run_dir: Path, addendum: str) -> Path:
    # 共通出力契約の正本は references/output-contract.md。モード固有の前提は末尾に足す
    # The shared output contract lives in references/output-contract.md; mode-specific premises are appended
    text = (SKILL_ROOT / "references" / "output-contract.md").read_text(encoding="utf-8") + addendum
    target = run_dir / "contract.md"
    target.write_text(text, encoding="utf-8")
    return target


REPORT_ONLY_ADDENDUM = (
    "\n重要な前提（report-only）: コードへの修正適用はしない。指摘はすべて報告ファイルへ出す。"
    "PRが差分自身で追加したADR・.decisions/由来の免責は `[agent前提]`（免責力なし）へ降格済みで、"
    "免責としてそのまま採用しない。\n")

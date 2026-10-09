# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""workflow_args/assemble.py — scripts/review_workflow/*.js を1本の Workflow スクリプトへ結合し args を埋め込む。

Workflow ランタイムはファイルシステムも import も持たず、本体を関数本体として評価する
（トップレベル return が通るのはそのため）。別ファイルの関数を共有する手段が無いので、
部品をファイル名順に連結して $RUNDIR/review_workflow.js を書き、それを scriptPath にする。
先頭部品（00_meta.js）が `export const meta` を持つ。args は `const A = args` の1行を
workflow-args.json と同じ JSON に置き換えて埋め込む（本体は scriptPath だけを渡し、巨大な args を
会話へ載せない。同じファイルでの再起動はそのまま resumeFromRunId のキャッシュに当たる）。

The Workflow runtime has neither filesystem nor import and evaluates the body as a function body,
so parts are concatenated in file-name order into $RUNDIR/review_workflow.js (the scriptPath),
with the args JSON embedded in place of the `const A = args` line.
"""
from __future__ import annotations

import json
from pathlib import Path

from workflow_args.systems import SCRIPTS

PARTS_DIR = SCRIPTS / "review_workflow"
OUTPUT_NAME = "review_workflow.js"
ARGS_LINE = "const A = args\n"


def workflow_parts() -> list[Path]:
    parts = sorted(PARTS_DIR.glob("*.js"))
    if not parts or "export const meta" not in parts[0].read_text(encoding="utf-8"):
        raise SystemExit(f"{PARTS_DIR} の先頭部品に `export const meta` が無い（00_meta.js を確認する）")
    return parts


def assemble_text() -> str:
    # 部品の境界に由来コメントを挟む（Workflow のエラー行から元ファイルを辿れるように）
    # Mark part boundaries so a runtime error line can be traced back to its source file
    chunks = []
    for part in workflow_parts():
        chunks.append(f"// ---- from scripts/review_workflow/{part.name} ----\n" + part.read_text(encoding="utf-8"))
    return "\n".join(chunks)


def embed_args(text: str, payload: dict) -> str:
    # 置換点が1箇所でなければ部品の形が変わっている。黙って args 依存のまま書き出さない
    # The marker must occur exactly once; otherwise the parts changed shape, so never emit an args-dependent script
    if text.count(ARGS_LINE) != 1:
        raise SystemExit(f"結合スクリプト中の `{ARGS_LINE.strip()}` が1箇所でない（00_meta.js を確認する）")
    return text.replace(ARGS_LINE, f"const A = {json.dumps(payload, ensure_ascii=False)}\n", 1)


def workflow_script_path(run_dir: Path) -> Path:
    return run_dir / OUTPUT_NAME


def write_workflow_script(run_dir: Path, payload: dict) -> Path:
    target = workflow_script_path(run_dir)
    target.write_text(embed_args(assemble_text(), payload), encoding="utf-8")
    return target

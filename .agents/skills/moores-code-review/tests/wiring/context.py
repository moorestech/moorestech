from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent.parent
# 2026-08-18にSKILL.mdは委譲ディスパッチャへ分割され、実行手順はreferences/orchestrator-steps.mdへ移った。
# 配線検査の対象は「SKILL.md + references配下の手順書」の全体（片方だけ見ると検査が空振りする）。
# The skill body was split into references/ on 2026-08-18; wiring lives across both.
SKILL_MD = "\n".join(
    [(SKILL_DIR / "SKILL.md").read_text(encoding="utf-8")]
    + [f.read_text(encoding="utf-8") for f in sorted((SKILL_DIR / "references").glob("*.md"))]
)
REPO_ROOT = SKILL_DIR.parent.parent.parent
# Workflow スクリプトは部品（scripts/review_workflow/*.js）を build_workflow_args.py が結合する。検査は結合後の全文に当てる
# The workflow script is assembled from scripts/review_workflow/*.js; checks run against the concatenated text
WORKFLOW_SRC = "\n".join(f.read_text(encoding="utf-8") for f in sorted((SKILL_DIR / "scripts/review_workflow").glob("*.js")))



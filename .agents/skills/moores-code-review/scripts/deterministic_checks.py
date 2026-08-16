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
"""moores-code-review 決定論チェック（汎用 + moorestech固有の統合版）。

Usage:
    python3 deterministic_checks.py <PATCH_PATH> [--repo-root <path>] [--context <USER_PROMPT_PATH>]

出力JSON:
    {
      "confirmed": [...],   # 検出正確・裏取り不要。Criticalとして統合に載せる
      "candidates": {
        "comparison_operator":  [...],  # verifiers/comparison-operator-verifier.md(sonnet)で裁定
        "try_catch_boundary":   [...],  # verifiers/try-catch-boundary-verifier.md(opus)で裁定
        "comment_length":       [...],  # post-checks/comment-convention-guard.md(sonnet)で裁定
        "region_internal":      [...],  # core-cs-region-internal reviewer の裏付けデータ
        "schema_optional_true": [...],  # master-data-defense レンズの裏付けデータ
        "event_tag_sync":       [...],  # server-state-sync レンズの裏付けデータ
        "guid_literal":         [...],  # hardcoded-content-enumeration レンズの裏付けデータ
        "event_action":         [...],  # domain-boundary レンズの裏付けデータ（UniRx規約）
        "mutable_auto_property":[...],  # redundant-member-duplication レンズの裏付けデータ
        "passthrough_property": [...]   # redundant-member-duplication レンズの裏付けデータ
      }
    }

confirmed は汎用(checks_static: partial・try-catch・デフォルト引数・SerializeField命名・200行・10ファイル)
と moorestech固有(checks_moores: master_default_fallback・packet_response_root)、
および --context 指定時の出所ラベル欠落(checks_context: context_source_label)の和。
空リストは対応 verifier/レンズ裏付けを起動しない合図（0トークン）。
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import checks_comment_length
import checks_comparison
import checks_lens_evidence
import checks_moores
import checks_region
import checks_static
from patch_util import parse_patch


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    patch_path = Path(argv[1])
    repo_root = Path.cwd()
    if "--repo-root" in argv:
        repo_root = Path(argv[argv.index("--repo-root") + 1]).resolve()
    context_findings: list[dict] = []
    if "--context" in argv:
        import checks_context
        context_findings = checks_context.run(Path(argv[argv.index("--context") + 1]), repo_root)
    patch_text = patch_path.read_text(encoding="utf-8", errors="replace")
    files = parse_patch(patch_text)
    result = {
        "confirmed": checks_static.run(files, repo_root) + checks_moores.run_confirmed(files) + context_findings,
        "candidates": {
            "comparison_operator": checks_comparison.run(files),
            "try_catch_boundary": checks_static.try_catch_boundary(files),
            "server_elapsed_time": checks_moores.server_elapsed_time(files),
            "comment_length": checks_comment_length.run(files),
            "region_internal": checks_region.run(files, repo_root),
            "schema_optional_true": checks_moores.schema_optional_true(files),
            "event_tag_sync": checks_moores.event_tag_sync(files, patch_text, repo_root),
            "guid_literal": checks_lens_evidence.guid_literal(files),
            "event_action": checks_lens_evidence.event_action(files),
            "mutable_auto_property": checks_lens_evidence.mutable_auto_property(files),
            "passthrough_property": checks_lens_evidence.passthrough_property(files),
        },
    }
    json.dump(result, sys.stdout, ensure_ascii=False, indent=2)
    print()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

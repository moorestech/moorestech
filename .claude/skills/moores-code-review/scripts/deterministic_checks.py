#!/usr/bin/env python3
"""moores-code-review 決定論チェック（汎用 + moorestech固有の統合版）。

Usage:
    python3 deterministic_checks.py <PATCH_PATH> [--repo-root <path>]

出力JSON:
    {
      "confirmed": [...],   # 検出正確・裏取り不要。Criticalとして統合に載せる
      "candidates": {
        "comparison_operator":  [...],  # verifiers/comparison-operator-verifier.md(sonnet)で裁定
        "comment_length":       [...],  # post-checks/comment-convention-guard.md(sonnet)で裁定
        "region_internal":      [...],  # core-cs-region-internal reviewer の裏付けデータ
        "schema_optional_true": [...],  # master-data-defense レンズの裏付けデータ
        "event_tag_sync":       [...]   # server-state-sync レンズの裏付けデータ
      }
    }

confirmed は汎用(checks_static: partial・try-catch・デフォルト引数・SerializeField命名・200行・10ファイル)
と moorestech固有(checks_moores: master_default_fallback・packet_response_root)の和。
空リストは対応 verifier/レンズ裏付けを起動しない合図（0トークン）。
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import checks_comment_length
import checks_comparison
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
    patch_text = patch_path.read_text(encoding="utf-8", errors="replace")
    files = parse_patch(patch_text)
    result = {
        "confirmed": checks_static.run(files, repo_root) + checks_moores.run_confirmed(files),
        "candidates": {
            "comparison_operator": checks_comparison.run(files),
            "comment_length": checks_comment_length.run(files),
            "region_internal": checks_region.run(files, repo_root),
            "schema_optional_true": checks_moores.schema_optional_true(files),
            "event_tag_sync": checks_moores.event_tag_sync(files, patch_text, repo_root),
        },
    }
    json.dump(result, sys.stdout, ensure_ascii=False, indent=2)
    print()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

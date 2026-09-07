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
"""moores-code-review Step 6.5: 発火すべきpost-checkガードを選択する。

Usage: python3 select_post_checks.py <FINAL_DIFF_PATH> <CHECKS_FINAL_JSON_PATH> [<APPLY_DIFF_PATH>]

発火条件（2026-08-16裁定・空振り回の無条件起動を廃止）:
  - comment-rationale-guard : 最終diffにコメントの削除行があるときだけ
  - comment-convention-guard: checks-final.json の candidates.comment_length が1件以上のときだけ
  - applied-diff-correctness: 第3引数の反映diff（Step 6 が適用した差分だけ）に、テスト以外の
    ソースファイルでコメント/空行以外の変更行があるときだけ（2026-09-08・cmux-connector c9baa79 の較正:
    レビュー出力を反映した diff がどの系統の入力にもならず 2 日間の機能停止を通した）

出力: `<post-check絶対パス>\t<モデル>` のTSV（select_lenses/select_reviewersと同形式）。
条件を満たすガードが無ければ何も出力しない（=post-checksスキップ）。
モデルは各post-check先頭YAMLの `model` が正。
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

POST_CHECKS_DIR = Path(__file__).resolve().parent.parent / "post-checks"

# 削除行のコメント判定: //・/*・*（ブロック継続）・#（py/sh/yaml。#region等の過検知はガード側が裁く）
# Deleted-comment detection: // , /* , * (block continuation), # (py/sh/yaml; guard adjudicates #region overmatch)
DELETED_COMMENT_RE = re.compile(r"^-\s*(//|/\*|\*[ /]|#)")


def read_model(md_path: Path) -> str:
    text = md_path.read_text(encoding="utf-8")
    m = re.search(r"^model:\s*(\S+)", text, re.MULTILINE)
    return m.group(1) if m else "opus"


def has_deleted_comment(diff_text: str) -> bool:
    for line in diff_text.splitlines():
        if line.startswith("---"):
            continue
        if DELETED_COMMENT_RE.match(line):
            return True
    return False


# 反映diffの再レビューが対象にするソース拡張子（doc・設定・データのみの反映は対象外）
# Source extensions the applied-diff re-review targets (doc/config/data-only applies are exempt)
SOURCE_SUFFIXES = (".cs", ".ts", ".tsx", ".js", ".mjs", ".py", ".swift")
# テストは200行規約と同じ判定で除外（deterministic_checks と同じ観念。テストのみの反映は再レビューしない）
# Tests are excluded with the same notion as the 200-line rule (test-only applies are not re-reviewed)
TEST_PATH_RE = re.compile(r"(Tests?\.cs$|\.test\.tsx?$|\.spec\.ts$|(^|/)(Tests?|tests?|e2e)/)")
COMMENT_ONLY_RE = re.compile(r"^[+-]\s*(//|/\*|\*|#|$)")


def applied_diff_touches_source(diff_text: str) -> bool:
    current_is_source = False
    for line in diff_text.splitlines():
        if line.startswith("+++ "):
            path = line[4:].strip()
            path = path[2:] if path.startswith("b/") else path
            current_is_source = path.endswith(SOURCE_SUFFIXES) and not TEST_PATH_RE.search(path)
            continue
        if line.startswith("--- ") or line.startswith("@@"):
            continue
        if current_is_source and line[:1] in "+-" and not COMMENT_ONLY_RE.match(line):
            return True
    return False


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(__doc__, file=sys.stderr)
        return 2
    diff_text = Path(argv[1]).read_text(encoding="utf-8", errors="replace")
    checks = json.loads(Path(argv[2]).read_text(encoding="utf-8"))
    comment_candidates = (checks.get("candidates") or {}).get("comment_length") or []

    fire: list[str] = []
    if has_deleted_comment(diff_text):
        fire.append("comment-rationale-guard")
    if comment_candidates:
        fire.append("comment-convention-guard")
    if len(argv) >= 4 and Path(argv[3]).exists():
        apply_text = Path(argv[3]).read_text(encoding="utf-8", errors="replace")
        if applied_diff_touches_source(apply_text):
            fire.append("applied-diff-correctness")

    for name in fire:
        md = POST_CHECKS_DIR / f"{name}.md"
        print(f"{md}\t{read_model(md)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

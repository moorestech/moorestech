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
"""レンズ裏付け候補の機械抽出（2026-08-16・LLM観察の決定論化第2弾）。

schema_optional_true / event_tag_sync と同じ「候補はスクリプトが数え、裁定はレンズ」方式。
いずれも確定検出にしない理由: 正当な例外が実在する（DTOのシリアライズ・Unity/外部interopの
event・生成コードのGUID等）ため、文脈判断はレンズ側が担う。

  - guid_literal          : 追加行のGUID文字列リテラル → hardcoded-content-enumeration の裏付け
  - event_action          : 追加行の `event Action` 宣言（UniRx規約違反疑い） → domain-boundary の裏付け
  - mutable_auto_property : 追加行の `{ get; set; }`（SetHogeメソッド規約違反疑い） → redundant-member-duplication の裏付け
  - passthrough_property  : 追加行のバッキングフィールド素通しプロパティ → redundant-member-duplication の裏付け

Mechanical candidate extraction backing the lenses; adjudication stays with the lens.
"""
from __future__ import annotations

import re

from cs_lex import strip_line
from patch_util import FileDiff

GUID_RE = re.compile(r'["\'][0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}["\']')
EVENT_ACTION_RE = re.compile(r"\bevent\s+(Action|EventHandler)\b")
MUTABLE_AUTO_RE = re.compile(r"\{\s*get;\s*set;\s*\}")
PASSTHROUGH_RE = re.compile(r"(=>\s*_\w+\s*;|\{\s*get\s*(=>|\{\s*return)\s*_\w+\s*;?\s*\}?)")

TEST_PATH_RE = re.compile(r"(Tests?/|Test\.cs$|Tests\.cs$|\.Test/|/e2e/)")


def guid_literal(files: list[FileDiff]) -> list[dict]:
    return _scan(files, GUID_RE, "guid-literal", "GUID文字列リテラルのコード内直書き")


def event_action(files: list[FileDiff]) -> list[dict]:
    return _scan(files, EVENT_ACTION_RE, "event-action", "event Action宣言（イベント発火はUniRxが規約）")


def mutable_auto_property(files: list[FileDiff]) -> list[dict]:
    return _scan(files, MUTABLE_AUTO_RE, "mutable-auto-property",
                 "public可変auto-property（値SetはSetHogeメソッドが規約。private setは対象外）")


def passthrough_property(files: list[FileDiff]) -> list[dict]:
    return _scan(files, PASSTHROUGH_RE, "passthrough-property",
                 "バッキングフィールド素通しプロパティ（{ get; private set; }へ畳む規約）")


#region Internal


def _scan(files: list[FileDiff], pattern: re.Pattern, rule: str, evidence: str) -> list[dict]:
    rows: list[dict] = []
    for f in files:
        if not f.path.endswith(".cs") or TEST_PATH_RE.search(f.path):
            continue
        for no, text in f.added():
            # GUIDは文字列リテラル内が本体のためstrip_lineを通さない（他は文字列/コメント除去後を見る）
            # GUIDs live inside string literals, so scan raw text for them; others use stripped code
            target = text if rule == "guid-literal" else strip_line(text)
            if not pattern.search(target):
                continue
            rows.append({"file": f.path, "line": no, "kind": rule,
                         "evidence": evidence, "code": text.strip()[:120]})
    return rows


#endregion

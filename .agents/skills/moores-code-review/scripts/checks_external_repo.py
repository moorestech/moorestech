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
"""外部repo直接参照の決定論チェック（confirmed: external_repo_reference）。

コードが moorestech-client-private（PersonalAssets）や moorestech_master へ直接言及すると、
moorestech repo 単体（CI含む）で動かなくなる。起点は moorestech-sm831: 生成マッププレビュー
テストが非公開Prefabに依存し、CI（client-privateを取得しない）で全PRが落ちた（2026-09-25ユーザー裁定）。

起動パイプライン等どうしても指定が要る箇所は external_repo_reference_allowlist.json に
理由付きで列挙し、検査対象外とする。コメント行・ドキュメント・Unity YAMLは見ない。

Code that names moorestech-client-private (PersonalAssets) or moorestech_master directly breaks
when the moorestech repo runs alone (e.g. CI). Pipelines that must name them are allowlisted
with a reason in external_repo_reference_allowlist.json. Comments, docs and Unity YAML are skipped.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

from patch_util import FileDiff

ALLOWLIST_PATH = Path(__file__).resolve().parent / "external_repo_reference_allowlist.json"

EXTERNAL_REPO_RE = re.compile(
    r"moorestech[-_]client[-_]private|PersonalAssets|moorestech_master", re.IGNORECASE)

# `//` コメントの言語と `#` コメントの言語。それ以外の拡張子（md・meta・prefab等）は対象外
# Languages with `//` comments vs `#` comments; any other extension (md/meta/prefab...) is skipped
SLASH_COMMENT_EXTS = (".cs", ".ts", ".tsx", ".js", ".mjs", ".cjs", ".json", ".asmdef")
HASH_COMMENT_EXTS = (".py", ".sh", ".bash", ".zsh", ".yml", ".yaml", ".tf")

# スキル・裁定・ドキュメントは規約の説明として外部repo名を書くので見ない
# Skills, rulings and docs mention the repos to explain the rules, so they are not code
NON_CODE_PREFIXES = (".agents/", ".claude/", ".codex/", ".decisions/", "docs/")

HASH_COMMENT_RE = re.compile(r"(^|\s)#.*$")
SLASH_COMMENT_RE = re.compile(r"(^|\s)//.*$")
BLOCK_COMMENT_LINE_RE = re.compile(r"^\s*(/\*|\*)")

MESSAGE = (
    "コードから外部repo（moorestech-client-private/PersonalAssets・moorestech_master）へ直接言及しない。"
    "moorestech repo単体（CIはclient-privateを取得しない）で動く形にする: テストは公開側のフィクスチャ・"
    "テスト用マスタ、マスタ参照はPinnedMasterRepository・ServerDirectory等の既存入口を経由する（起点: moorestech-sm831）。"
    "起動パイプライン等どうしても指定が要る場合のみ scripts/external_repo_reference_allowlist.json へ理由付きで追加する")


def run(files: list[FileDiff]) -> list[dict]:
    allowed_prefixes = [entry["path"] for entry in load_allowlist()]
    findings = []
    for f in files:
        if not _is_code_path(f.path) or any(f.path.startswith(p) for p in allowed_prefixes):
            continue
        for lineno, text in f.added():
            if EXTERNAL_REPO_RE.search(_strip_comment(f.path, text)):
                findings.append({
                    "rule": "external-repo-reference", "file": f.path, "line": lineno,
                    "evidence": text.strip(), "message": MESSAGE, "fix_class": "judgement"})
    return findings


def load_allowlist() -> list[dict]:
    return json.loads(ALLOWLIST_PATH.read_text(encoding="utf-8"))["allow"]


def _is_code_path(path: str) -> bool:
    if path.startswith(NON_CODE_PREFIXES):
        return False
    return path.endswith(SLASH_COMMENT_EXTS) or path.endswith(HASH_COMMENT_EXTS)


def _strip_comment(path: str, text: str) -> str:
    # 文字列リテラルは残す（パス指定の本体はそこに書かれる）。コメントだけ落とす
    # Keep string literals (the path itself lives there); drop only comments
    # cs_lex.strip_line は文字列まで空白化するので使えない。URLの`://`は前置空白が無いので残る
    # cs_lex.strip_line blanks strings too, so it is unusable; `://` in URLs has no leading space and survives
    if path.endswith(HASH_COMMENT_EXTS):
        return HASH_COMMENT_RE.sub("", text)
    if BLOCK_COMMENT_LINE_RE.match(text):
        return ""
    return SLASH_COMMENT_RE.sub("", text)

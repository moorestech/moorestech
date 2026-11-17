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
"""パッケージ・アセンブリ間の参照の追加を機械抽出する（違反判定はしない）。

依存の向きの悪化（汎用層がドメイン層を参照する等）は、推奨案の文面に帰結が書かれないと
一括承認でそのまま出荷される。参照の追加は宣言ファイルの差分から確実に取れるので、
LLM の自己申告に頼らず列挙し、integrator が向きを判定して統合報告と設計判断の各案に書く。

Extract added package/assembly references from manifest diffs (no verdict). Direction
regressions ship silently under bulk approval when the recommendation omits them, so the
list comes from the diff, and the integrator judges the direction.
"""
from __future__ import annotations

import re
from pathlib import Path

from patch_util import FileDiff

# asmdef の references 配列要素（キー無しの文字列行）/ Bare string rows in an asmdef references array
ASMDEF_REF_RE = re.compile(r'^\s*"([^"]+)"\s*,?\s*$')
# package.json の "名前": "版" のうち版らしい値 / "name": "version" rows whose value looks like a version spec
NPM_DEP_RE = re.compile(r'^\s*"([^"]+)"\s*:\s*"((?:[\^~<>=]*\d|workspace:|file:|link:|npm:|git\+|github:)[^"]*)"\s*,?\s*$')
CSPROJ_REF_RE = re.compile(r'<(ProjectReference|PackageReference)\s+Include="([^"]+)"')
SWIFT_PKG_RE = re.compile(r'\.(package|product)\(\s*(?:name:\s*)?(?:url:\s*)?"([^"]+)"')


def run(files: list[FileDiff]) -> list[dict]:
    changes: list[dict] = []
    for f in files:
        name = Path(f.path).name
        for line_no, text in f.added():
            ref = _added_reference(name, text)
            if ref:
                changes.append({"file": f.path, "line": line_no, "kind": ref[0], "reference": ref[1],
                                "message": "参照が増えた。汎用層→ドメイン層など依存の向きが悪化していないかを"
                                           "integrator が判定し、統合報告の「依存の変化」節と該当する設計判断の案に書く"})
    return changes


def _added_reference(file_name: str, text: str) -> tuple[str, str] | None:
    if file_name.endswith(".asmdef"):
        m = ASMDEF_REF_RE.match(text)
        return ("asmdef-reference", m.group(1)) if m else None
    if file_name == "package.json":
        m = NPM_DEP_RE.match(text)
        return ("npm-dependency", m.group(1)) if m else None
    if file_name.endswith(".csproj"):
        m = CSPROJ_REF_RE.search(text)
        return (m.group(1), m.group(2)) if m else None
    if file_name == "Package.swift":
        m = SWIFT_PKG_RE.search(text)
        return (f"swift-{m.group(1)}", m.group(2)) if m else None
    return None

# 「置換なのに変更前が見えない」カードを patch.diff と照合して落とす。規約の明文化だけでは守られないため
# Fail cards that show a replacement without its "before", cross-checked against patch.diff; prose rules alone were not kept
from __future__ import annotations

from ..models import Document
from .lines import code_card_lines, iter_code_cards


def _hunks(patch_text: str) -> list[dict]:
    # patch.diff を hunk 単位で {追加行集合, 削除行の有無} へ畳む
    # Fold patch.diff into per-hunk records of {added line set, whether it deletes}
    hunks: list[dict] = []
    current: dict | None = None
    for line in patch_text.splitlines():
        if line.startswith("@@"):
            current = {"added": set(), "deletes": False}
            hunks.append(current)
            continue
        if current is None or line.startswith(("+++", "---")):
            continue
        if line.startswith("+"):
            body = line[1:].strip()
            if body:
                current["added"].add(body)
        elif line.startswith("-"):
            current["deletes"] = True
    return hunks


def missing_deletion_problems(doc: Document, patch_text: str) -> list[str]:
    # 追加行を持つカードだけを検査し、対応hunkが全て削除を伴うのに - 行が無ければ問題として返す
    # Inspect only cards with added lines; report when every matched hunk deletes but the card has no "-" line
    hunks = _hunks(patch_text)
    problems = []
    for finding in doc.findings:
        for card in iter_code_cards(finding.body_md):
            lines = code_card_lines(card)
            added = {code.strip() for _, kind, _, code in lines if kind == "add" and code.strip()}
            if not added or any(kind == "del" for _, kind, _, _ in lines):
                continue
            matched = [h for h in hunks if added & h["added"]]
            if matched and all(h["deletes"] for h in matched):
                problems.append(
                    f"{finding.id}: 置換を扱うカードに削除行がありません。"
                    f"patch.diff の該当hunkは行を削除しています。`-<旧行番号>|<コード>` を足してください")
    return problems

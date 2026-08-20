# 「置換なのに変更前が見えない」カードを patch.diff と照合して落とす。規約の明文化だけでは守られないため
# Fail cards that show a replacement without its "before", cross-checked against patch.diff; prose rules alone were not kept
from __future__ import annotations

from ..models import Document
from .lines import code_card_lines, iter_code_cards


def _change_blocks(patch_text: str) -> list[dict]:
    # 連続する +/- 行のかたまり（変更ブロック）単位で {追加行集合, 削除の有無} へ畳む
    # Fold the patch into contiguous runs of +/- lines, recording {added line set, whether it deletes}
    # hunk単位にすると、3行の文脈を挟んだ無関係な削除まで「置換」と誤判定する
    # Hunk granularity would misread an unrelated deletion three context lines away as part of the replacement
    blocks: list[dict] = []
    current: dict | None = None
    in_hunk = False
    for line in patch_text.splitlines():
        # ファイル見出しに戻ったらhunk外へ抜ける。--- / +++ のヘッダはこの区間にしか現れない
        # A file header ends the hunk; the --- / +++ headers only ever appear in that gap
        if line.startswith("diff --git "):
            in_hunk, current = False, None
            continue
        if line.startswith("@@"):
            in_hunk, current = True, None
            continue
        if not in_hunk:
            continue
        # 改行欠落マーカーは直前の変更行の続きなのでブロックを切らない
        # The "no newline" marker continues the preceding changed line, so it must not split the block
        if line.startswith("\\"):
            continue
        if line[:1] not in ("+", "-"):
            current = None
            continue
        if current is None:
            current = {"added": set(), "deletes": False}
            blocks.append(current)
        if line.startswith("+"):
            body = line[1:].strip()
            if body:
                current["added"].add(body)
        else:
            current["deletes"] = True
    return blocks


def missing_deletion_problems(doc: Document, patch_text: str) -> list[str]:
    # 追加行を持つカードだけを検査し、対応ブロックが全て削除を伴うのに - 行が無ければ問題として返す
    # Inspect only cards with added lines; report when every matched block deletes but the card has no "-" line
    blocks = _change_blocks(patch_text)
    problems = []
    for finding in doc.findings:
        for card in iter_code_cards(finding.body_md):
            lines = code_card_lines(card)
            added = {code.strip() for _, kind, _, code in lines if kind == "add" and code.strip()}
            if not added or any(kind == "del" for _, kind, _, _ in lines):
                continue
            matched = [b for b in blocks if added & b["added"]]
            if matched and all(b["deletes"] for b in matched):
                problems.append(
                    f"{finding.id}: 置換を扱うカードに削除行がありません。"
                    f"patch.diff の該当箇所は行を削除しています。`-<旧行番号>|<コード>` を足してください")
    return problems

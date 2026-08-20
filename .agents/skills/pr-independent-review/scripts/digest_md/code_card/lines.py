# code-card 1行の記法 [フラグ]<行番号>|<コード> を分解する。全ての読み手はここを経由する
# Parse the code-card line syntax [flags]<lineno>|<code>; every reader goes through this module
from __future__ import annotations

import re

from ..errors import DigestError

# フラグ文字と行種別の対応。* は行種別と直交する注目マーク
# Flag characters mapped to line kinds; "*" is an orthogonal highlight marker
_KIND_FLAGS = {"+": "add", "-": "del"}
# フラグは必ず行番号の前に並ぶ。`36-38` のような範囲表記を削除行として黙って飲まないため厳密に切る
# Flags always precede the line number; a strict split keeps a range like `36-38` from silently becoming a deletion
_HEAD_RE = re.compile(r"^(?P<flags>[*+-]*)(?P<num>\d+)$")


def code_card_lines(body: str) -> list[tuple[str, str, bool, str]]:
    # 各行を (行番号, kind, 注目行か, コード) へ分解する。kind は add / del / ctx
    # Split each line into (line number, kind, is-highlight, code); kind is add / del / ctx
    parsed = []
    for raw in body.splitlines():
        if "|" not in raw:
            raise DigestError(f"code-card の行に | がありません: {raw!r}")
        head, code = raw.split("|", 1)
        matched = _HEAD_RE.match(head.strip())
        if not matched:
            raise DigestError(f"code-card の行頭が [フラグ]<行番号> の形ではありません: {raw!r}")
        flags = matched.group("flags")
        kinds = [k for flag, k in _KIND_FLAGS.items() if flag in flags]
        if len(kinds) > 1:
            raise DigestError(f"code-card の行に + と - を同時に付けられません: {raw!r}")
        parsed.append((matched.group("num"), kinds[0] if kinds else "ctx", "*" in flags, code))
    return parsed


def iter_code_cards(body_md: str) -> list[str]:
    # finding本文から code-card フェンスの中身だけを出現順に取り出す
    # Extract the bodies of code-card fences from a finding body, in order of appearance
    cards, lines, i = [], body_md.splitlines(), 0
    while i < len(lines):
        if lines[i].startswith("```code-card"):
            buf = []
            i += 1
            while i < len(lines) and not lines[i].startswith("```"):
                buf.append(lines[i])
                i += 1
            cards.append("\n".join(buf))
        i += 1
    return cards

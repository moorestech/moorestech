# 文字列のエスケープとインライン記法の変換。エスケープは必ずマークアップ付与の前に行う
# String escaping and inline markup; escaping always runs before markup is added
from __future__ import annotations

import re

from .parse import DigestError

_STRONG = re.compile(r"\*\*(.+?)\*\*")
_CODE = re.compile(r"`([^`]+)`")
_REF = re.compile(r"\[F:([A-Za-z0-9_-]+)\]")


def escape(text: str) -> str:
    # & を最初に置換しないと、後続で付けた実体参照まで二重エスケープされる
    # Ampersand must go first, otherwise entities added later get double-escaped
    out = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    return out.replace('"', "&quot;").replace("'", "&#39;")


def inline_html(text: str, refs: dict[str, str]) -> str:
    # エスケープ済み文字列に対してのみマークアップを付ける
    # Markup is applied only on top of already-escaped text
    out = escape(text)

    # コードスパンを先に退避し、参照・強調から保護してから最後に復元する
    # Stash code spans first so refs/strong never touch their contents, restore last
    stash: list = []

    def stash_code(m):
        stash.append(m.group(1))
        return f"\x00CODE{len(stash) - 1}\x00"

    out = _CODE.sub(stash_code, out)

    def ref(m):
        slug = m.group(1)
        if slug not in refs:
            raise DigestError(f"未定義の参照です: [F:{slug}]")
        fid = refs[slug]
        return f'<a href="#{fid.lower()}">{fid}</a>'

    out = _REF.sub(ref, out)
    out = _STRONG.sub(lambda m: f"<strong>{m.group(1)}</strong>", out)

    # プレースホルダをコードスパンの実内容へ戻す
    # Restore placeholders back to their original code span content
    for idx, code in enumerate(stash):
        out = out.replace(f"\x00CODE{idx}\x00", f"<code>{code}</code>")
    return out

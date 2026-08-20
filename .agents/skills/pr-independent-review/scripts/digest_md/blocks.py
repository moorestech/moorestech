# ブロック記法（段落・箇条書き・h3・コードフェンス）のHTML化。未知記法は落とす
# Block-level markdown to HTML (paragraph, list, h3, fences); unknown syntax fails loudly
from __future__ import annotations

from .code_card.html import code_card_html
from .inline import escape, inline_html
from .parse import DigestError
from .sectioning import read_fence

_KNOWN_FENCES = ("code-card", "")


def _is_unknown_markup(line: str) -> bool:
    # 先頭文字が未対応記法（引用・テーブル・裸の*/+）かを判定する
    # Detect whether the leading char is unsupported markup (quote, table, bare */+)
    return line[0] in "#>|" or (line[0] in "*+" and not line.startswith("**"))


def blocks_html(md: str, refs: dict[str, str], indent: str, lang: str) -> str:
    # 空行区切りのブロックへ割ってから、種別ごとに変換する。langはcode-cardの構文着色言語
    # Split on blank lines and convert each block by kind; lang is the code-card highlight language
    out = []
    lines = md.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        if line.startswith("```"):
            lang_fence = line[3:].strip()
            if lang_fence not in _KNOWN_FENCES:
                raise DigestError(f"未対応のコードフェンス種別です: {lang_fence}")
            body, i = read_fence(lines, i)
            if lang_fence == "code-card":
                out.append(code_card_html(body, indent, lang))
            else:
                out.append(f"{indent}<pre><code>{escape(body)}</code></pre>")
            continue
        if line.startswith("### "):
            out.append(f"{indent}<h3>{inline_html(line[4:].strip(), refs)}</h3>")
            i += 1
            continue
        if line.startswith("- "):
            items, i = _collect_list(lines, i)
            body = "\n".join(f"{indent}  <li>{inline_html(x, refs)}</li>" for x in items)
            out.append(f"{indent}<ul>\n{body}\n{indent}</ul>")
            continue
        if _is_unknown_markup(line):
            raise DigestError(f"未対応の記法です: {line!r}")
        para, i = _collect_paragraph(lines, i)
        out.append(f"{indent}<p>{inline_html(para, refs)}</p>")
    return "\n".join(out)


def _collect_list(lines: list, i: int) -> tuple:
    # 連続した - で始まる行をリスト項目として集める
    # Collect consecutive lines starting with "- " as list items
    items = []
    while i < len(lines) and lines[i].startswith("- "):
        items.append(lines[i][2:].strip())
        i += 1
    return items, i


def _collect_paragraph(lines: list, i: int) -> tuple:
    # 空行や記法開始まで連続した行を段落として集める
    # Collect consecutive plain text lines as a paragraph until blank line or markup
    buf = []
    while i < len(lines) and lines[i].strip() and not lines[i].startswith(("```", "- ", "### ")):
        raw = lines[i].strip()
        # 継続行も未知記法チェックの対象とし、無言で握り潰さない
        # Continuation lines are checked too, so unknown markup never slips through silently
        if _is_unknown_markup(raw):
            raise DigestError(f"未対応の記法です: {lines[i]!r}")
        buf.append(raw)
        i += 1
    return " ".join(buf), i

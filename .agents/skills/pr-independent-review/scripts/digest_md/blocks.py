# ブロック記法（段落・箇条書き・h3・コードフェンス）のHTML化。未知記法は落とす
# Block-level markdown to HTML (paragraph, list, h3, fences); unknown syntax fails loudly
from __future__ import annotations

from .inline import escape, inline_html
from .parse import DigestError

_KNOWN_FENCES = ("code-card", "")


def code_card_html(body: str, indent: str) -> str:
    # 各行は [フラグ]<行番号>|<コード>。+ は追加行、* は問題行
    # Each line is [flags]<lineno>|<code>; "+" marks an insertion, "*" marks the offending line
    rendered = []
    for raw in body.splitlines():
        if "|" not in raw:
            raise DigestError(f"code-card の行に | がありません: {raw!r}")
        head, code = raw.split("|", 1)
        head = head.strip()
        ins, hl = "+" in head, "*" in head
        num = head.replace("+", "").replace("*", "").strip()
        if not num.isdigit():
            raise DigestError(f"code-card の行番号が数字ではありません: {raw!r}")
        inner = escape(code)
        inner = f"<ins>{inner}</ins>" if ins else inner
        line = f'<span class="ln">{num}</span>{inner}'
        rendered.append(f'<span class="hl">{line}</span>' if hl else line)
    return f'{indent}<pre class="code-card"><code>' + "\n".join(rendered) + "</code></pre>"


def blocks_html(md: str, refs: dict, indent: str) -> str:
    # 空行区切りのブロックへ割ってから、種別ごとに変換する
    # Split on blank lines, then convert each block by its kind
    out = []
    lines = md.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        if line.startswith("```"):
            lang = line[3:].strip()
            if lang not in _KNOWN_FENCES:
                raise DigestError(f"未対応のコードフェンス種別です: {lang}")
            body, i = _collect_fence(lines, i)
            if lang == "code-card":
                out.append(code_card_html(body, indent))
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
        if line[0] in "#>|" or (line[0] in "*+" and not line.startswith("**")):
            raise DigestError(f"未対応の記法です: {line!r}")
        para, i = _collect_paragraph(lines, i)
        out.append(f"{indent}<p>{inline_html(para, refs)}</p>")
    return "\n".join(out)


def _collect_fence(lines: list, i: int) -> tuple:
    # コードフェンスの開始行をスキップして本体を抽出する
    # Skip opening fence line and extract body until closing fence
    body = []
    i += 1
    while i < len(lines) and not lines[i].startswith("```"):
        body.append(lines[i])
        i += 1
    if i >= len(lines):
        raise DigestError("閉じられていないコードフェンスがあります")
    return "\n".join(body), i + 1


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
        buf.append(lines[i].strip())
        i += 1
    return " ".join(buf), i

# code-cardを行番号・差分グリフ・コードの3spanで描く。構文着色はこの構造の上にJSが当てる
# Render a code-card as three spans per line (gutter, diff sign, code); the JS highlighter sits on top
from __future__ import annotations

from ..inline import escape
from .lines import code_card_lines

# 行種別ごとのグリフ。色が潰れても記号で差分が読めるようにする
# Glyph per line kind, so the diff stays readable even when colors wash out
SIGN = {"add": "+", "del": "-", "ctx": " "}


def code_card_html(body: str, indent: str, lang: str) -> str:
    # 各行は [フラグ]<行番号>|<コード>。+ は追加行、- は削除行、* は注目行
    # Each line is [flags]<lineno>|<code>; "+" adds, "-" deletes, "*" marks the offending line
    rendered = []
    for num, kind, hl, code in code_card_lines(body):
        classes = f"cl {kind} hl" if hl else f"cl {kind}"
        rendered.append(f'<span class="{classes}"><span class="ln">{num}</span>'
                        f'<span class="sign">{SIGN[kind]}</span>'
                        f'<span class="code">{escape(code)}</span></span>')
    attr = f' data-lang="{escape(lang)}"' if lang else ""
    return f'{indent}<pre class="code-card"{attr}><code>' + "\n".join(rendered) + "</code></pre>"

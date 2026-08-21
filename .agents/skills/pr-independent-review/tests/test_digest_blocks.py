# Markdown片のHTML化とエスケープ、code-cardの行マークアップを検証する
# Verify markdown-to-HTML conversion, escaping, and code-card line markup
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.blocks import blocks_html
from digest_md.inline import escape, inline_html
from digest_md.parse import DigestError


def test_escape_order_keeps_ampersand_first():
    assert escape('a & b < c > d " e \' f') == "a &amp; b &lt; c &gt; d &quot; e &#39; f"


def test_inline_html_converts_strong_code_and_ref():
    got = inline_html("**主張:** `Subject<int>` は [F:gear] を壊す", {"gear": "F03"})
    assert got == '<strong>主張:</strong> <code>Subject&lt;int&gt;</code> は <a href="#f03">F03</a> を壊す'


def test_inline_html_unknown_ref_is_error():
    with pytest.raises(DigestError) as e:
        inline_html("[F:nope]", {"gear": "F03"})
    assert "nope" in str(e.value)


def test_blocks_html_paragraph_and_list():
    got = blocks_html("段落だ。\n\n- 一つ目\n- 二つ目", {}, "      ", "")
    assert "<p>段落だ。</p>" in got
    assert "<ul>" in got and "<li>一つ目</li>" in got


def test_blocks_html_rejects_unknown_syntax():
    with pytest.raises(DigestError) as e:
        blocks_html("> 引用は未対応", {}, "", "")
    assert "未対応" in str(e.value)


def test_blocks_html_rejects_unknown_fence():
    with pytest.raises(DigestError) as e:
        blocks_html("```mermaid\ngraph TD\n```", {}, "", "")
    assert "mermaid" in str(e.value)


def test_inline_html_code_span_protects_strong_and_ref():
    # コードスパン内の ** と [F:slug] は変換されず生のまま残るべき
    # "**" and "[F:slug]" inside a code span must stay raw, not get converted
    got = inline_html("code has `a**b**c` inside", {})
    assert got == "code has <code>a**b**c</code> inside"
    got_ref = inline_html("code has `[F:gear]` inside", {"gear": "F03"})
    assert got_ref == "code has <code>[F:gear]</code> inside"


def test_blocks_html_rejects_unknown_syntax_on_continuation_line():
    # 段落の2行目以降でも未知記法はエラーで落ちるべき（先頭行限定であってはならない）
    # Unknown markup must error even on a paragraph's continuation line, not just its first
    with pytest.raises(DigestError) as e:
        blocks_html("段落一行目\n> 引用っぽい二行目", {}, "", "")
    assert "未対応" in str(e.value)

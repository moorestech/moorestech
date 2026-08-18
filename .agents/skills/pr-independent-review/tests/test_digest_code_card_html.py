# code-cardのHTML描画（3span構造・グリフ・注目行・data-lang）を検証する
# Verify code-card HTML rendering: the 3-span structure, signs, highlight, and data-lang
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.html import code_card_html


def test_context_line_has_blank_sign():
    got = code_card_html(" 36|void A()", "  ", "csharp")
    assert ('<span class="cl ctx"><span class="ln">36</span>'
            '<span class="sign"> </span><span class="code">void A()</span></span>') in got


def test_added_and_deleted_lines_carry_their_sign():
    got = code_card_html("+38|added();\n-37|removed();", "  ", "csharp")
    assert '<span class="cl add"><span class="ln">38</span><span class="sign">+</span>' in got
    assert '<span class="cl del"><span class="ln">37</span><span class="sign">-</span>' in got


def test_highlight_is_a_class_not_a_wrapper():
    got = code_card_html("*+40|hot();", "  ", "csharp")
    assert '<span class="cl add hl">' in got
    assert "<ins>" not in got


def test_code_is_escaped():
    got = code_card_html("+1|B<int>(a && b);", "  ", "csharp")
    assert '<span class="code">B&lt;int&gt;(a &amp;&amp; b);</span>' in got


def test_lang_becomes_data_attribute():
    assert '<pre class="code-card" data-lang="csharp">' in code_card_html(" 1|x", "  ", "csharp")


def test_empty_lang_omits_data_attribute():
    assert '<pre class="code-card">' in code_card_html(" 1|x", "  ", "")

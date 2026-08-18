# 案（options）がカード本文へ描かれること、手書き代替案が拒否されることを検証する
# Verify options are rendered into the card body and that hand-written alternatives are rejected
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.errors import DigestError
from digest_md.finding_parser import finding_from
from digest_md.models import Finding
from digest_md.render import options_html

META = ("```yaml\n"
        "slug: s\ncategory: design-decision\nseverity: medium\nmust_read: true\n"
        "summary: 一言\nfiles: [a/B.cs:1]\noptions:\n  - 直す\n  - 戻す\n"
        "```\n")


def _finding(options):
    return Finding(slug="s", title="t", category="design-decision", severity="medium",
                   summary="一言", files=["a/B.cs:1"], body_md="", options=options)


def test_options_render_as_keyed_list():
    got = options_html(_finding(["供給側へ通す", "元へ戻す"]), "        ")
    assert '<p class="options-head"><strong>選べる案</strong></p>' in got
    assert '<ul class="plain options-list">' in got
    assert '<li><strong>案A</strong><span class="opt-recommended">推奨</span> — 供給側へ通す</li>' in got
    assert "<li><strong>案B</strong> — 元へ戻す</li>" in got


def test_options_are_escaped():
    got = options_html(_finding(["Subject<int> を使う"]), "  ")
    assert "Subject&lt;int&gt; を使う" in got


def test_no_options_renders_nothing():
    assert options_html(_finding([]), "  ") == ""


def test_hand_written_alternatives_paragraph_is_rejected():
    # 案の正本は options 一本。本文の代替案段落は二重管理になるので落とす
    # options is the single source for alternatives; a body paragraph would duplicate it
    with pytest.raises(DigestError) as e:
        finding_from("t", META + "\n**代替案:** **案A（推奨）** — 直す")
    assert "代替案" in str(e.value)


def test_recommendation_key_is_rejected():
    # recommendation は options 先頭から自動で埋まるので書かせない
    # recommendation is auto-filled from the first option, so it must not be written
    with pytest.raises(DigestError) as e:
        finding_from("t", META.replace("options:", "recommendation: 案A: 直す\noptions:"))
    assert "recommendation" in str(e.value)


def test_body_without_alternatives_passes():
    f = finding_from("t", META + "\n**PR側の主張:** 一致させる")
    assert f.options == ["直す", "戻す"]


def test_code_card_quoting_the_word_alternatives_passes():
    # 抜粋は逐語転記が規約なので、コードが「代替案」という語を含むだけで落としてはならない
    # Excerpts are transcribed verbatim, so merely quoting the word must not fail the build
    f = finding_from("t", META + "\n```code-card\n 1|        // 代替案としてキャッシュを使う\n```")
    assert f.options == ["直す", "戻す"]

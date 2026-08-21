# code-card行の分解（追加・削除・文脈・注目行）とフェンス抽出を検証する
# Verify code-card line parsing (add/del/ctx/highlight) and fence extraction
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.lines import code_card_lines, iter_code_cards
from digest_md.errors import DigestError


def test_kinds_are_add_del_and_ctx():
    body = " 36|void A()\n+38|added();\n-37|removed();\n*+40|hot();"
    got = code_card_lines(body)
    assert [(n, k, h) for n, k, h, _ in got] == [
        ("36", "ctx", False),
        ("38", "add", False),
        ("37", "del", False),
        ("40", "add", True),
    ]


def test_highlighted_deletion_is_allowed():
    assert code_card_lines("*-12|gone();")[0][:3] == ("12", "del", True)


def test_add_and_del_on_same_line_is_error():
    with pytest.raises(DigestError) as e:
        code_card_lines("+-12|both();")
    assert "+" in str(e.value)


def test_missing_pipe_is_error():
    with pytest.raises(DigestError):
        code_card_lines("36 void A()")


def test_non_numeric_line_number_is_error():
    with pytest.raises(DigestError):
        code_card_lines("+xx|void A()")


def test_iter_code_cards_returns_every_fence_in_order():
    body = "段落\n\n```code-card\n+1|a\n```\n\n本文\n\n```code-card\n 2|b\n```\n"
    assert iter_code_cards(body) == ["+1|a", " 2|b"]


def test_iter_code_cards_ignores_plain_fences():
    body = "```\nplain\n```\n\n```code-card\n+1|a\n```\n"
    assert iter_code_cards(body) == ["+1|a"]


def test_line_range_shorthand_is_error():
    # `36-38` を削除行の 3638 行目として黙って描かない（大声のエラーのまま保つ）
    # A range like `36-38` must never be silently rendered as deletion of line 3638
    with pytest.raises(DigestError):
        code_card_lines(" 36-38|        void A()")


def test_flag_after_the_number_is_error():
    with pytest.raises(DigestError):
        code_card_lines("36+|added();")

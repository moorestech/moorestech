# テンプレートがダークモードを持ち、色がトークン化されていることを検証する
# Verify the template ships a dark mode and that colors are tokenized
import re
from pathlib import Path

TEMPLATE = Path(__file__).resolve().parent.parent / "assets" / "digest-template.html"
_STYLE = re.compile(r"(?s)<style>(.*?)</style>")
_HEX = re.compile(r"#[0-9a-fA-F]{3,8}\b")


def _style_text() -> str:
    return "\n".join(_STYLE.findall(TEMPLATE.read_text(encoding="utf-8")))


def test_template_has_dark_media_query():
    assert "@media (prefers-color-scheme: dark)" in _style_text()


def test_template_has_no_theme_toggle():
    # R11: 切替UIは追加しない
    # R11: no manual toggle is added
    text = TEMPLATE.read_text(encoding="utf-8")
    assert "theme-toggle" not in text
    assert "prefers-color-scheme" in text


def test_hex_colors_live_only_in_token_blocks():
    # 色定義は :root と dark メディアクエリ（およびvendorテーマ）に閉じ、部品側は var() を使う
    # Color literals stay inside :root and the dark media query; components use var()
    lines = _style_text().splitlines()
    depth_in_tokens, offenders = False, []
    for line in lines:
        stripped = line.strip()
        if stripped.startswith(":root") or stripped.startswith("@media (prefers-color-scheme: dark)"):
            depth_in_tokens = True
        if depth_in_tokens and stripped == "}":
            depth_in_tokens = False
            continue
        if depth_in_tokens:
            continue
        if stripped.startswith("{{HLJS"):
            continue
        if _HEX.search(stripped):
            offenders.append(stripped)
    assert not offenders, f"トークン外に生の16進が残っています: {offenders[:5]}"

# モバイル表示の必須契約をテンプレートに固定する
# Lock the template's mobile layout contract so future digests stay phone-native
from pathlib import Path


TEMPLATE = Path(__file__).resolve().parent.parent / "assets" / "digest-template.html"


def _text() -> str:
    return TEMPLATE.read_text(encoding="utf-8")


def _compact() -> str:
    return "".join(_text().split())


def test_template_constrains_long_content_to_viewport():
    text = _text()
    assert 'content="width=device-width, initial-scale=1, viewport-fit=cover"' in text
    assert "main>*{min-width:0;}" in text
    assert "pre,.code-card,.table-wrap{max-width:100%;}" in text
    assert "h1,h2,h3,h4,p,li,td,th,.file-path,.lead-item,.callout,.compare-col,.ci-body,.panel-empty" in text
    assert "overflow-wrap:anywhere" in text


def test_template_mobile_cards_and_controls_fit_a_390px_viewport():
    text = _compact()
    assert "@media(max-width:640px)" in text
    assert ".verdict-card,.suppressed-card{padding:12px10px;}" in text
    assert "width:calc(100vw-24px-env(safe-area-inset-left,0px)-env(safe-area-inset-right,0px));" in text
    assert ".comment-panel{left:max(12px,env(safe-area-inset-left,0px));right:max(12px,env(safe-area-inset-right,0px))" in text
    assert "env(safe-area-inset-bottom,0px)" in text
    assert "env(safe-area-inset-top,0px)" in text
    assert "env(safe-area-inset-left,0px)" in text
    assert "env(safe-area-inset-right,0px)" in text
    assert "width:min(360px,calc(100vw-24px-env(safe-area-inset-left,0px)-env(safe-area-inset-right,0px)))" in text


def test_template_mobile_interactive_controls_have_touch_targets():
    text = _text()
    assert ".figure-comment-btn,.comment-fab,.panel-toggle,.btn-primary,.btn-ghost,.ci-act,.ci-del" in text
    assert "min-height:44px" in text
    assert ".ci-act,.ci-del{min-width:44px;}" in text

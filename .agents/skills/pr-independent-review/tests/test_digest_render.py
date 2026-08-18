# ゾーン骨格の自動生成と、カードのHTML形状（data-finding-idの付与先を含む）を検証する
# Verify auto-generated zone skeleton and card HTML shape, including where data-finding-id lands
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.findings import assign_ids
from digest_md.parse import Document, Finding
from digest_md.render import render_html

TEMPLATE = (Path(__file__).resolve().parent.parent / "assets" / "digest-template.html").read_text(encoding="utf-8")


def _doc():
    meta = {"pr": "1155", "head": "0" * 40, "verdict": "reject", "verdict_line": "Critical 1件",
            "date": "2026-08-18", "generated_at": "2026-08-18T00:00:00+09:00", "title": "テスト"}
    ruling = Finding(slug="gear", title="歯車の要求トルク率", category="design-decision",
                     severity="medium", summary="需要だけ膨らむ。", files=["a/Gear.cs:40"],
                     body_md="**PR側の主張:** 一致させる", options=["供給側に通す", "戻す"], must_read=True)
    crit = Finding(slug="latch", title="再ラッチ漏れ", category="critical", severity="critical",
                   summary="分母がズレる。", files=["b/Latch.cs:10"],
                   body_md="[F:gear] と同根。", options=["直す"])
    notes = {k: "該当なし（0件）。" for k in
             ["must-read", "other-rulings", "suppressed", "new-shape", "criticals"]}
    return Document(meta=meta, notes=notes, ledger_md="- 台帳の中身",
                    appendix_md="## Warning全件\n\n- なし", findings=[ruling, crit])


def test_render_places_zones_in_fixed_order():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    ids = re.findall(r'<section id="([a-z-]+)"', html)
    assert ids == ["you-decide", "must-read", "other-rulings", "suppressed",
                   "new-shape", "criticals", "ledger", "appendix"]


def test_render_puts_finding_id_on_the_card_element():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    # 裁定サイトはこの属性でボタン注入位置を決める。figureラッパではなくカードに付ける
    # The adjudication site injects buttons at this attribute; it must sit on the card, not the wrapper
    assert '<section class="verdict-card critical" id="f01" data-finding-id="F01">' in html
    assert '<section class="verdict-card ruling" id="f02" data-finding-id="F02">' in html


def test_render_sets_verdict_and_placeholders():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    assert '<section class="verdict-header" data-verdict="reject">' in html
    assert "{{TITLE}}" not in html and "{{DATE}}" not in html and "{{SUBTITLE}}" not in html
    assert "pr-review-1155-comments-v1" in html


def test_render_keeps_template_shell_untouched():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    assert html.count("<script") == 1
    assert "使い方:" not in html
    assert '<div id="comment-ui-root" data-comment-ui>' in html


def test_render_resolves_cross_reference_to_anchor():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    assert '<a href="#f02">F02</a> と同根。' in html


def test_render_index_lists_must_read_findings():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    index = html.split('<section id="you-decide">')[1].split("</section>")[0]
    assert 'href="#f02"' in index and "必読の設計判断 1件" in index

# 文書モデルからHTMLを組む。外枠はテンプレをそのまま使い <main> の中身だけ差し替える
# Build HTML from the document model; reuse the template shell and replace only <main>
from __future__ import annotations

import re

from .blocks import blocks_html
from .code_card.lang import language_of
from .findings import OPTION_KEYS, sort_key
from .inline import escape, inline_html
from .parse import DigestError, Document, Finding
from .sectioning import split_blocks

VERDICT_TEXT = {"auto": "自動マージ可", "ruling": "新形につき裁定行き",
                "reject": "Critical差し戻し", "stub": "未測定（スタブ）"}
BADGE = {"design-decision": ("badge-new", "設計判断", "verdict-card ruling"),
         "critical": ("badge-sup", "Critical", "verdict-card critical"),
         "novelty": ("badge-new", "新形", "verdict-card")}
ZONES = [("must-read", "必読の設計判断"),
         ("other-rulings", "残りの設計判断（推奨案どおりで良ければ一言で足りる）"),
         ("suppressed", "suppressed（判断台帳で免責された指摘）"),
         ("new-shape", "新形（このリポジトリに前例のない形）"),
         ("criticals", "Critical要点（裁定不要の修正リスト）")]


def _zone_of(f: Finding) -> str:
    # suppressed・カテゴリの順で優先判定し、最後にmust_readで振り分ける
    # Check suppressed, then category, in priority order; must_read decides the remainder
    if f.suppressed:
        return "suppressed"
    if f.category == "novelty":
        return "new-shape"
    if f.category == "critical":
        return "criticals"
    return "must-read" if f.must_read else "other-rulings"


def options_html(f: Finding, indent: str) -> str:
    # 案はoptionsが正本。キー採番と推奨マークはfindings.jsonと同じ規則で機械的に付ける
    # options is the single source for alternatives; keys and the recommended mark follow findings.json
    if not f.options:
        return ""
    items = []
    for n, summary in enumerate(f.options):
        mark = '<span class="opt-recommended">推奨</span>' if n == 0 else ""
        items.append(f'{indent}  <li><strong>案{OPTION_KEYS[n]}</strong>{mark} — {escape(summary)}</li>')
    body = "\n".join(items)
    return (f'\n{indent}<p class="options-head"><strong>選べる案</strong></p>'
            f'\n{indent}<ul class="plain options-list">\n{body}\n{indent}</ul>')


def _card_html(f: Finding, refs: dict) -> str:
    # data-finding-id はカード要素そのものに付ける（裁定サイトの注入位置の正）
    # data-finding-id sits on the card element itself: the anchor the adjudication site injects at
    if f.suppressed:
        badge_class, badge_text, card_class = "badge-sup", "suppressed", "suppressed-card"
    else:
        badge_class, badge_text, card_class = BADGE[f.category]
    names = " / ".join(f"<strong>{escape(p.split(':')[0].split('/')[-1])}</strong>" for p in f.files)
    head, *rest = f.files
    paths = f"<code>{escape(head)}</code>"
    if rest:
        paths += "（＋ " + ", ".join(f"<code>{escape(p)}</code>" for p in rest) + "）"
    label = f.label or f"{f.title}のカード（実コード抜粋つき）"
    body = blocks_html(f.body_md, refs, "        ", language_of(f.files))
    opts = options_html(f, "        ")
    extra = ""
    if f.suppressed:
        extra = f'\n        <p><strong>suppressed-by:</strong> {inline_html(f.suppress_reason, refs)}</p>'
    return f"""    <div class="figure" data-label="{escape(label)}">
      <button class="figure-comment-btn" data-comment-ui>コメント</button>
      <section class="{card_class}" id="{f.id.lower()}" data-finding-id="{f.id}">
        <h2><span class="badge {badge_class}">{badge_text}</span> {names}</h2>
        <p class="file-path">{paths}</p>
        <p class="summary-line">{inline_html(f.summary, refs)}</p>
{body}{opts}{extra}
      </section>
    </div>"""


def _index_html(doc: Document, refs: dict) -> str:
    # 「あなたが判断すること」はカードから機械的に導出する
    # The "what you decide" index is derived mechanically from the cards
    ordered = sorted(doc.findings, key=sort_key)
    by_zone = {z: [f for f in ordered if _zone_of(f) == z] for z, _ in ZONES}
    rows = []
    must = by_zone["must-read"]
    links = " ／ ".join(f'<a href="#{f.id.lower()}">{f.id} {escape(f.index_label or f.summary)}</a>'
                       for f in must)
    rows.append(f"<strong>必読の設計判断 {len(must)}件</strong>" + (f" — {links}" if links else ""))
    rows.append(f'<strong>suppressed {len(by_zone["suppressed"])}件</strong> — '
                f'<a href="#suppressed">suppressedセクション</a>')
    rows.append(f'<strong>新形の入国審査 {len(by_zone["new-shape"])}件</strong> — '
                f'<a href="#new-shape">新形セクション</a>')
    crit = "・".join(f'<a href="#{f.id.lower()}">{f.id}</a>' for f in by_zone["criticals"])
    rows.append('<strong>裁定不要</strong>: <a href="#criticals">Critical要点</a>'
                + (f"（{crit}）" if crit else "（0件）"))
    items = "\n".join(
        f'      <li class="lead-item"><span class="badge">{n}</span><div>{row}</div></li>'
        for n, row in enumerate(rows, start=1))
    return f"""  <section id="you-decide">
    <h2>あなたが判断すること</h2>
    <ul class="lead-list">
{items}
    </ul>
  </section>"""


def _appendix_html(md: str, refs: dict) -> str:
    # ## 見出しごとに details へ畳む（split_blocksはフェンス内の見出しを無視する）
    # Fold each "## " heading into its own details block (split_blocks ignores headings inside fences)
    blocks, _ = split_blocks(md)
    out = []
    for level, title, body in blocks:
        if level != "2":
            continue
        out.append(f"    <details>\n      <summary>{inline_html(title, refs)}</summary>\n"
                   f"{blocks_html(body, refs, '      ', '')}\n    </details>")
    return "\n".join(out)


def render_html(doc: Document, template: str, refs: dict, assets: dict) -> str:
    # 検証 → verdictヘッダとインデックス → ゾーン → 判断台帳/折りたたみ参考、の順で <main> を組む
    # Validate, then assemble <main> as verdict header + index, zones, ledger/appendix
    if "<main>" not in template:
        raise DigestError("テンプレートに <main> がありません")
    # 使い方コメント内に文字列 "<main>" が出現するため、<main>置換より先に剥がしておく
    # The usage comment contains the literal string "<main>", so strip it before the <main> swap
    template, n = re.subn(r"<!--.*?使い方:.*?-->\n", "", template, flags=re.S)
    if n != 1:
        raise DigestError("テンプレの使い方コメントを剥がせませんでした")
    meta = doc.meta
    verdict = meta["verdict"]
    text = escape(VERDICT_TEXT[verdict])
    parts = [f"""  <section class="verdict-header" data-verdict="{verdict}">
    <h2>verdict: {text}</h2>
    <p class="verdict-line"><strong>verdict: {text}</strong> — {escape(meta['verdict_line'])}</p>
  </section>""", _index_html(doc, refs)]

    # ゾーンごとにカードを振り分け、注記本文の後ろへ連結する
    # Route cards into zones and append them after each zone's note body
    ordered = sorted(doc.findings, key=sort_key)
    for zone_id, heading in ZONES:
        cards = [_card_html(f, refs) for f in ordered if _zone_of(f) == zone_id]
        note = blocks_html(doc.notes[zone_id], refs, "    ", "")
        body = note + ("\n" + "\n".join(cards) if cards else "")
        parts.append(f'  <section id="{zone_id}">\n    <h2>{escape(heading)}</h2>\n{body}\n  </section>')

    # 判断台帳の箇条書きはテンプレの ul.plain 体裁で出す（出所リストの詰まった見た目を保つ）
    # The ledger's lists use the template's ul.plain style, keeping the dense source-list look
    ledger = blocks_html(doc.ledger_md, refs, "    ", "").replace("<ul>", '<ul class="plain">')
    parts.append('  <section id="ledger">\n    <h2>判断台帳</h2>\n'
                 f'{ledger}\n  </section>')
    parts.append('  <section id="appendix">\n    <h2>折りたたみ参考</h2>\n'
                 f'{_appendix_html(doc.appendix_md, refs)}\n  </section>')

    # 文書見出しは仕様上すでに `PR #<番号>` を含むため、番号を二重に付けない
    # The document heading already carries "PR #<number>" per spec, so never prepend it twice
    heading = meta["title"]
    title = f"独立レビュー: {heading}" if heading.startswith("PR #") else f"独立レビュー: PR #{meta['pr']} {heading}"
    # 置換前にテンプレ側の全トークン存在を確認し、欠落を無言で通さない
    # Verify every template token exists before replacing, so a missing one never slips through silently
    for token in ("{{TITLE}}", "{{DATE}}", "{{SUBTITLE}}", "{{HLJS_JS}}",
                  "{{HLJS_CSS_LIGHT}}", "{{HLJS_CSS_DARK}}",
                  "REPLACE_WITH_UNIQUE_STORAGE_KEY", "REPLACE_WITH_COPY_HEADING"):
        if token not in template:
            raise DigestError(f"テンプレに {token} がありません")
    # 置換は本文を差し込む前のテンプレへ当てる。後だと抜粋がトークンを引用しただけで展開されてしまう
    # Substitute into the template before the body lands; afterwards an excerpt quoting a token would expand it
    template = template.replace("{{TITLE}}", escape(title)).replace("{{DATE}}", escape(meta["date"]))
    template = template.replace("{{SUBTITLE}}", escape(f"verdict: {VERDICT_TEXT[verdict]}"))
    template = template.replace("REPLACE_WITH_UNIQUE_STORAGE_KEY", f"pr-review-{meta['pr']}-comments-v1")
    template = template.replace("REPLACE_WITH_COPY_HEADING", f"PR #{meta['pr']} 独立レビュー裁定")
    # vendor資産は固定バージョンの自前管理物なのでエスケープせず素通しする（唯一の生HTML注入点）
    # Vendored assets are self-managed at a pinned version, so they pass through unescaped (the only raw-HTML injection)
    template = template.replace("{{HLJS_JS}}", assets["hljs_js"])
    template = template.replace("{{HLJS_CSS_LIGHT}}", assets["hljs_css_light"])
    template = template.replace("{{HLJS_CSS_DARK}}", assets["hljs_css_dark"])

    if template.count("<main>") != 1 or template.count("</main>") != 1:
        raise DigestError("テンプレの<main>が1個ではありません")
    main = "<main>\n\n" + "\n\n".join(parts) + "\n\n</main>"
    return re.sub(r"<main>.*</main>", lambda _: main, template, flags=re.S)

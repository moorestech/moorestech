"""PR報告ページのHTMLを組み立てる。書式はここだけが決める（PRごとに書式を変えさせないため）。

Renders the PR report page. This module alone owns the layout so every PR report looks the same.
"""
from __future__ import annotations

from html import escape

VIDEO_SUFFIXES = {".mp4", ".webm"}

_STYLE = """
:root{--bg:#f7f7f5;--fg:#1d1d1b;--muted:#6b6b66;--card:#fff;--line:#e2e2dc;--accent:#2f6fde}
@media (prefers-color-scheme:dark){:root{--bg:#161615;--fg:#ecece8;--muted:#a0a09a;--card:#21211f;--line:#34342f;--accent:#7aa7ff}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);
font:15px/1.7 -apple-system,BlinkMacSystemFont,"Hiragino Sans","Noto Sans JP",sans-serif}
main{max-width:1100px;margin:0 auto;padding:24px 16px 64px}
h1{font-size:22px;line-height:1.4;margin:0 0 4px}h2{font-size:17px;margin:32px 0 12px;
padding-bottom:6px;border-bottom:1px solid var(--line)}.meta{color:var(--muted);font-size:13px}
.meta a{color:var(--accent)}ul{padding-left:20px;margin:8px 0}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(100%,480px),1fr));gap:16px}
figure{margin:0;background:var(--card);border:1px solid var(--line);border-radius:10px;overflow:hidden}
figure img,figure video{display:block;width:100%;height:auto;background:#000}
figcaption{padding:8px 12px;font-size:13px}.none{color:var(--muted)}
"""


def render(pr: dict, manifest: dict, media: list[dict]) -> str:
    """pr: gh pr view の number/title/url。media: 公開先のファイル名・見出し・グループを持つ辞書
    pr: number/title/url from gh pr view. media: dicts with published name, caption and group"""
    title = f"PR #{pr['number']} {pr['title']}"
    parts = [
        f"<h1>{escape(title)}</h1>",
        f'<p class="meta"><a href="{escape(pr["url"])}">{escape(pr["url"])}</a>'
        f'{" ・ head " + escape(manifest["head"]) if manifest.get("head") else ""}</p>',
        "<h2>概要</h2>", _list(manifest["summary"]),
        "<h2>画像・動画</h2>", _media_section(manifest, media),
        "<h2>検証</h2>", _list(manifest["verification"]),
    ]
    if manifest.get("notes"):
        parts += ["<h2>補足</h2>", _list(manifest["notes"])]
    body = "\n".join(parts)
    return (
        '<!doctype html><html lang="ja"><head><meta charset="utf-8">'
        '<meta name="viewport" content="width=device-width,initial-scale=1">'
        f"<title>{escape(title)}</title><style>{_STYLE}</style></head>"
        f"<body><main>\n{body}\n</main></body></html>\n"
    )


def _list(items: list[str]) -> str:
    return "<ul>" + "".join(f"<li>{escape(item)}</li>" for item in items) + "</ul>"


def _media_section(manifest: dict, media: list[dict]) -> str:
    if not media:
        return f'<p class="none">画像・動画なし: {escape(manifest["no_media_reason"])}</p>'
    # グループは manifest に現れた順で並べる（撮影順の物語を崩さない）
    # Groups keep manifest order so the capture story stays intact
    groups: dict[str, list[dict]] = {}
    for item in media:
        groups.setdefault(item["group"], []).append(item)
    out = []
    for group, items in groups.items():
        if group:
            out.append(f"<h3>{escape(group)}</h3>")
        out.append('<div class="grid">' + "".join(_figure(item) for item in items) + "</div>")
    return "\n".join(out)


def _figure(item: dict) -> str:
    src = escape(item["name"])
    caption = escape(item["caption"])
    if item["suffix"] in VIDEO_SUFFIXES:
        tag = f'<video src="{src}" controls preload="metadata" playsinline></video>'
    else:
        tag = f'<a href="{src}"><img src="{src}" alt="{caption}" loading="lazy"></a>'
    return f"<figure>{tag}<figcaption>{caption}</figcaption></figure>"

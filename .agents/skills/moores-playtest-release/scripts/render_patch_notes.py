#!/usr/bin/env python3
"""patch-notes.md をテスター共有用の1枚HTMLにする（ライト/ダーク両対応）。

Usage: render_patch_notes.py <patch-notes.md> <status文> > patch-notes.html
md の形: 1行目 "# 見出し"、空行、メタ行（前回/今回/掲載順）、空行、"1. **題**" + 字下げ本文 の繰り返し。
"""
import html
import re
import sys

if len(sys.argv) != 3 or sys.argv[1] in ("-h", "--help"):
    sys.exit(__doc__)
lines = open(sys.argv[1], encoding="utf-8").read().splitlines()
title = lines[0].lstrip("# ").strip()
meta, items, current = [], [], None
for line in lines[1:]:
    heading = re.match(r"\d+\. \*\*(.+)\*\*", line)
    if heading:
        current = [heading.group(1), ""]
        items.append(current)
    elif current and line.strip():
        current[1] += line.strip()
    elif not current and line.strip():
        meta.append(line.strip())
if not items:
    sys.exit("項目（'1. **題**'）が見つかりません")
esc = html.escape
body = "".join(f"<li><h2>{esc(t)}</h2><p>{esc(b)}</p></li>" for t, b in items)
print(f"""<!doctype html><html lang="ja"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>moorestech プレイテスト更新</title>
<style>:root{{--bg:#fafafa;--fg:#222;--sub:#555;--line:#ddd;--note:#fff4d5;--noteb:#b78100}}@media (prefers-color-scheme:dark){{:root{{--bg:#16181c;--fg:#e8e8e8;--sub:#aaa;--line:#333;--note:#3a3220;--noteb:#d9a520}}}}
body{{max-width:900px;margin:40px auto;padding:0 16px;font-family:system-ui;line-height:1.9;color:var(--fg);background:var(--bg)}}h1{{font-size:28px}}h2{{font-size:20px;margin:0}}p{{margin:8px 0}}ol{{padding-left:24px}}li{{padding:18px 8px;border-bottom:1px solid var(--line)}}.meta{{color:var(--sub)}}.status{{padding:16px;background:var(--note);border-left:4px solid var(--noteb)}}</style></head><body>
<h1>{esc(title)}</h1><p class="status">{esc(sys.argv[2])}</p><p class="meta">{"<br>".join(esc(m) for m in meta)}</p><ol>{body}</ol></body></html>""")

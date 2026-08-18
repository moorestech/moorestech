#!/usr/bin/env python3
# digest.md から digest.html と findings.json を生成するCLI入口
# CLI entry point that builds digest.html and findings.json from digest.md
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from digest_md.findings import assign_ids, build_findings
from digest_md.parse import DigestError, parse_document
from digest_md.render import render_html

TEMPLATE = Path(__file__).resolve().parent.parent / "assets" / "digest-template.html"


def verify(html: str, findings: dict) -> list:
    # 出荷前の機械検査。人の目視に頼っていた検査をここへ集約する
    # Pre-ship machine checks; the checks that used to rely on human inspection live here
    problems = []
    # 実在するプレースホルダのみ検査する（コード抜粋中の "{{" は本文由来で誤爆するため）
    # Only check the placeholders that actually exist ("{{" bare can false-positive on code excerpts)
    for token in ("{{TITLE}}", "{{DATE}}", "{{SUBTITLE}}"):
        if token in html:
            problems.append(f"未置換のプレースホルダ {token} が残っています")
    # テンプレ外枠（style/head/main本数/剥がし残り）が生き残っているかを検査する
    # Check the template shell survived (style/head/main count/leftover strip markers)
    if "<style>" not in html:
        problems.append("<style> がありません")
    if "</head>" not in html:
        problems.append("</head> がありません")
    if html.count("</main>") != 1:
        problems.append(f"</main> が {html.count('</main>')} 個あります（1個であるべき）")
    if "REPLACE_WITH_" in html:
        problems.append("REPLACE_WITH_ プレースホルダが残っています")
    if "使い方:" in html:
        problems.append("使い方コメントが残っています")
    if html.count("<script") != 1:
        problems.append(f"<script> が {html.count('<script')} 個あります（1個であるべき）")
    ids = [f["id"] for f in findings["findings"]]
    for fid in ids:
        if f'data-finding-id="{fid}"' not in html:
            problems.append(f"{fid} のカードがHTMLにありません")
    if html.count('data-finding-id="') != len(ids):
        problems.append("data-finding-id の件数がfindings件数と一致しません")
    for f in findings["findings"]:
        if f["suppressed"]:
            continue
        n = len([o for o in f["options"] if o.get("recommended")])
        if n != 1:
            problems.append(f"{f['id']}: recommended が {n} 件（1件であるべき）")
    return problems


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: digest_build.py <RUNDIR>", file=sys.stderr)
        return 1
    rundir = Path(sys.argv[1])
    md_path = rundir / "digest.md"
    if not md_path.is_file():
        print(f"digest.md がありません: {md_path}", file=sys.stderr)
        return 1

    # 外部入力（AI生成のMarkdown）の隔離のためここだけ例外を捕える
    # This is the external-input boundary (AI-authored markdown), so the exception is caught here
    try:
        doc = parse_document(md_path.read_text(encoding="utf-8"))
        refs = assign_ids(doc)
        findings = build_findings(doc)
        html = render_html(doc, TEMPLATE.read_text(encoding="utf-8"), refs)
    except DigestError as e:
        print(f"digest.md の形式エラー: {e}", file=sys.stderr)
        return 1

    problems = verify(html, findings)
    if problems:
        for p in problems:
            print(f"生成後検査に失敗: {p}", file=sys.stderr)
        return 1

    (rundir / "digest.html").write_text(html, encoding="utf-8")
    with (rundir / "findings.json").open("w", encoding="utf-8") as fp:
        json.dump(findings, fp, ensure_ascii=False, indent=2)
        fp.write("\n")
    print(f"generated: {rundir/'digest.html'} / {rundir/'findings.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

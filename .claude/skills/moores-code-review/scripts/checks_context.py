#!/usr/bin/env python3
"""4カテゴリcontextの出所ラベル検査。

「許容するトレードオフ」「目指さない（非目標）」セクションの各箇条書き行に
出所ラベル（[ユーザー裁定: ...] / [ADR: ...] / [agent前提]）を要求する。
ラベル欠落行は confirmed（context_source_label）として返す。
欠落行は免責力を持たない=自動的に [agent前提] 扱いであることをmessageに明記する。
"""
from __future__ import annotations

import re
from pathlib import Path

TARGET_SECTIONS = ("許容するトレードオフ", "目指さない")
LABEL_RE = re.compile(r"\[(ユーザー裁定:.+?|ADR:.+?|agent前提)\]")


def run(context_path: Path) -> list[dict]:
    if not context_path.is_file():
        return [{
            "check": "context_source_label",
            "file": str(context_path),
            "line": 0,
            "message": "contextファイルが存在しない（--contextで指定されたパスを確認）",
        }]
    findings: list[dict] = []
    in_target = False
    seen_sections = 0
    for lineno, raw in enumerate(
            context_path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        line = raw.strip()
        if line.startswith("#"):
            in_target = any(s in line for s in TARGET_SECTIONS)
            seen_sections += 1 if in_target else 0
            continue
        if in_target and line.startswith("- ") and not LABEL_RE.search(line):
            findings.append({
                "check": "context_source_label",
                "file": str(context_path),
                "line": lineno,
                "message": (
                    f"出所ラベル欠落: {line[:60]} — [ユーザー裁定]/[ADR]/[agent前提] の"
                    "いずれかを付与すること。欠落行は [agent前提] 扱いで免責力を持たない"
                ),
            })
    # 対象セクション見出しゼロは沈黙故障ではなくfail-closedで検出する
    # Zero target headings must fail closed, not silently pass
    if seen_sections == 0:
        findings.append({
            "check": "context_source_label",
            "file": str(context_path),
            "line": 0,
            "message": (
                "「許容するトレードオフ」「目指さない」の見出し（#〜###）が見つからない — "
                "4カテゴリcontextは必ず `##` 見出しで書くこと（太字箇条書きは検査対象外になる）"
            ),
        })
    return findings

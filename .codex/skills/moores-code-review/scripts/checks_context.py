#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""4カテゴリcontextの出所ラベル検査（confirmedスキーマはchecks_staticと共通）。

「許容するトレードオフ」「目指さない（非目標）」セクションの実質行（箇条書き・散文とも）に
出所ラベルを要求し、ラベル自体の構造も検証する:
- [agent前提] — 常に受理（免責力は持たない）
- [ユーザー裁定: ...] — 発言引用（括弧書き）または AskUserQuestion＋日付(YYYY-MM-DD) を要求
- [ADR: <doc名>#<項目>] — docs/superpowers/{specs,plans} の判断台帳を実解決し、
  項目が実在しない・参照先行が agent前提 なら免責不可として検出する
カテゴリ見出し（##）の欠落はカテゴリごとに fail-closed で検出する。
"""
from __future__ import annotations

import re
from pathlib import Path

# 「目指さない」と「非目標」は同一カテゴリの表記揺れとして扱う
# "目指さない" and "非目標" are spelling variants of the same category
CATEGORIES = {
    "許容するトレードオフ": ("許容するトレードオフ",),
    "目指さない（非目標）": ("目指さない", "非目標"),
}
LABEL_RE = re.compile(r"\[(ユーザー裁定:[^\]]+|ADR:[^\]]+|agent前提)\]")
QUOTE_RE = re.compile(r"[「『\"“].+?[」』\"”]")
DATE_RE = re.compile(r"\d{4}-\d{2}-\d{2}")
LEDGER_HEADING_RE = re.compile(r"^##\s*(判断記録（ADR）|判断台帳)")


def run(context_path: Path, repo_root: Path) -> list[dict]:
    if not context_path.is_file():
        return [_finding(str(context_path), 0,
                         "contextファイルが存在しない（--contextで指定されたパスを確認）")]
    findings: list[dict] = []
    matched_categories: set[str] = set()
    current: str | None = None
    in_fence = False
    for lineno, raw in enumerate(
            context_path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        line = raw.strip()
        if line.startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence or not line:
            continue
        if line.startswith("#"):
            current = None
            for cat, aliases in CATEGORIES.items():
                if any(a in line for a in aliases):
                    current = cat
                    matched_categories.add(cat)
            continue
        if current is None:
            continue
        # 対象カテゴリ内は箇条書きに限らず実質行すべてを検査する（散文回避の封鎖）
        # Inside target categories every substantive line is checked, prose included
        label = LABEL_RE.search(line)
        if not label:
            findings.append(_finding(str(context_path), lineno,
                                     f"出所ラベル欠落: {line[:60]} — [ユーザー裁定]/[ADR]/[agent前提] "
                                     "のいずれかを付与。欠落行は [agent前提] 扱いで免責力を持たない"))
            continue
        findings.extend(_validate_label(label.group(1), str(context_path), lineno, repo_root))
    # カテゴリ見出しの欠落はカテゴリごとにfail-closedで検出する
    # Missing category headings fail closed, per category
    for cat in CATEGORIES:
        if cat not in matched_categories:
            findings.append(_finding(str(context_path), 0,
                                     f"「{cat}」の見出し（##）が見つからない — 4カテゴリcontextは必ず "
                                     "`##` 見出しで書くこと（太字箇条書きは検査対象外になる）"))
    return findings


def _validate_label(label: str, file: str, lineno: int, repo_root: Path) -> list[dict]:
    if label == "agent前提":
        return []
    if label.startswith("ユーザー裁定:"):
        body = label[len("ユーザー裁定:"):]
        if QUOTE_RE.search(body) or ("AskUserQuestion" in body and DATE_RE.search(body)):
            return []
        return [_finding(file, lineno,
                         f"[ユーザー裁定] の構造不備: {body.strip()[:40]} — 発言引用（「…」等）または "
                         "AskUserQuestion＋日付(YYYY-MM-DD)が必要。満たせないなら [agent前提] へ降格")]
    # [ADR: <doc名>#<項目>] を実解決する（表面ラベルだけの免責を封鎖）
    # Resolve [ADR:] against the actual ledger; a bare label grants no exemption
    body = label[len("ADR:"):].strip()
    doc_ref, _, item = body.partition("#")
    if not item:
        return [_finding(file, lineno,
                         f"[ADR:] の構造不備: {body[:40]} — `<doc名>#<台帳項目>` 形式が必要")]
    ledger = _resolve_ledger(doc_ref.strip(), repo_root)
    if ledger is None:
        return [_finding(file, lineno,
                         f"[ADR: {doc_ref.strip()[:40]}] のdocが docs/superpowers/{{specs,plans}} に見つからない")]
    hit_lines = [l for l in ledger if item.strip() in l]
    if not hit_lines:
        return [_finding(file, lineno,
                         f"[ADR: …#{item.strip()[:30]}] が参照先の判断台帳に実在しない — "
                         "台帳掲載＋ユーザー承認済みの項目だけが [ADR:] を名乗れる")]
    if any("agent前提" in l for l in hit_lines):
        return [_finding(file, lineno,
                         f"[ADR: …#{item.strip()[:30]}] の参照先が agent前提 項目 — 免責力を持たないため "
                         "[agent前提] へ降格すること（ユーザー裁定項目のみ [ADR:] 参照可）")]
    return []


def _resolve_ledger(doc_ref: str, repo_root: Path) -> list[str] | None:
    stem = doc_ref.removesuffix(".md")
    for sub in ("docs/superpowers/specs", "docs/superpowers/plans"):
        base = repo_root / sub
        if not base.is_dir():
            continue
        for md in sorted(base.glob("*.md")):
            if stem in md.name:
                return _ledger_section(md)
    return None


def _ledger_section(doc: Path) -> list[str] | None:
    lines = doc.read_text(encoding="utf-8", errors="replace").splitlines()
    start = None
    for i, line in enumerate(lines):
        if start is None and LEDGER_HEADING_RE.match(line.strip()):
            start = i + 1
        elif start is not None and line.startswith("## "):
            return lines[start:i]
    return lines[start:] if start is not None else None


def _finding(path: str, line: int, message: str) -> dict:
    return {"rule": "context-source-label", "file": path, "line": line,
            "evidence": "", "message": message, "fix_class": "judgement"}

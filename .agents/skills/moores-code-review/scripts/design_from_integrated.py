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
"""design_from_integrated.py — design.md を integrated.md の機械転記で作り、書き換えを検出する。

AskUserQuestion の正本は design.md。apply 役（LLM）が design.md を書き直すと、統合報告の
推奨（例: 「両論併記」）が別の推奨へ黙って変わり、ユーザーはそれを承認してしまう。
そこで転記部分はこのスクリプトだけが書き、apply 役は末尾の「apply で追加した設計判断」
見出しの下へ追記だけ行う。AskUserQuestion の直前に check で転記部分の一致を確かめる。

design.md is the source for AskUserQuestion. If the apply agent rewrites it, the integrator's
recommendation silently changes. This script owns the transcribed part; agents only append
under the trailing heading, and `check` verifies the transcription right before asking.

使い方 / Usage:
  python3 design_from_integrated.py build --integrated <integrated.md> --out <design.md>
  python3 design_from_integrated.py check --integrated <integrated.md> --design <design.md>
終了コード: 0=一致/生成済み 1=転記部分が書き換わっている 2=入力不正
"""
from __future__ import annotations

import argparse
import difflib
import re
import sys
from pathlib import Path

APPEND_HEADING = "## apply で追加した設計判断"
CRITICAL_HEADING = "## 採用Critical のうち適用区分=設計判断"
DESIGN_HEADING = "## 設計判断"
EMPTY_MARK = "なし"
DESIGN_CLASS_RE = re.compile(r"\*\*適用区分\*\*\s*[:：]\s*設計判断")


def split_sections(text: str) -> dict[str, str]:
    # "## " 見出しで区切り、見出し語 → 本文 の辞書にする
    # Split on level-2 headings into {heading text: body}
    sections: dict[str, str] = {}
    name = None
    buf: list[str] = []
    for line in text.splitlines():
        if line.startswith("## "):
            if name is not None:
                sections[name] = "\n".join(buf).strip("\n")
            name, buf = line[3:].strip(), []
        elif name is not None:
            buf.append(line)
    if name is not None:
        sections[name] = "\n".join(buf).strip("\n")
    return sections


def design_class_criticals(body: str) -> list[str]:
    # 採用Critical の "### " ブロックのうち、適用区分が設計判断のものだけを原文のまま返す
    # Return verbatim "### " blocks of adopted Criticals whose apply class is a design judgement
    blocks: list[list[str]] = []
    for line in body.splitlines():
        if line.startswith("### "):
            blocks.append([line])
        elif blocks:
            blocks[-1].append(line)
    picked = ["\n".join(b).strip("\n") for b in blocks]
    return [b for b in picked if DESIGN_CLASS_RE.search(b)]


def transcribed_part(integrated_text: str) -> str:
    sections = split_sections(integrated_text)
    if "採用Critical" not in sections and "設計判断" not in sections:
        raise ValueError("integrated.md に「## 採用Critical」「## 設計判断」のどちらも無い")
    criticals = design_class_criticals(sections.get("採用Critical", ""))
    design = sections.get("設計判断", "").strip() or EMPTY_MARK
    lines = [
        "# design.md",
        "",
        "<!-- design_from_integrated.py が integrated.md から転記した。末尾の「apply で追加した設計判断」見出しより上は"
        "書き換え禁止（推奨・選択肢・帰結の文面を変えない。check が検出する）。 -->",
        "",
        CRITICAL_HEADING,
        "",
        "\n\n".join(criticals) if criticals else EMPTY_MARK,
        "",
        DESIGN_HEADING,
        "",
        design,
        "",
    ]
    return "\n".join(lines)


def build(integrated: Path, out: Path) -> int:
    part = transcribed_part(integrated.read_text(encoding="utf-8"))
    out.write_text(f"{part}\n{APPEND_HEADING}\n\n{EMPTY_MARK}\n", encoding="utf-8")
    print(f"design.md を生成: {out}")
    return 0


def check(integrated: Path, design: Path) -> int:
    expected = transcribed_part(integrated.read_text(encoding="utf-8"))
    actual_text = design.read_text(encoding="utf-8")
    # 見出しは行頭一致で探す（本文中の言及で切らない）
    # Match the heading at line start only
    parts = re.split(rf"^{re.escape(APPEND_HEADING)}\s*$", actual_text, maxsplit=1, flags=re.M)
    if len(parts) < 2:
        print(f"NG: design.md に「{APPEND_HEADING}」見出しが無い（build で作り直す）", file=sys.stderr)
        return 1
    actual = parts[0]
    if actual.rstrip("\n") == expected.rstrip("\n"):
        print("OK: design.md の転記部分は integrated.md と一致")
        return 0
    diff = difflib.unified_diff(expected.splitlines(), actual.splitlines(),
                                "integrated.md からの転記", "design.md", lineterm="", n=1)
    print("NG: design.md の転記部分が integrated.md と食い違う（推奨の書き換えの疑い）。"
          "AskUserQuestion を出さず、build で作り直してから追記分を戻す", file=sys.stderr)
    for line in list(diff)[:40]:
        print(line, file=sys.stderr)
    return 1


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build")
    b.add_argument("--integrated", required=True)
    b.add_argument("--out", required=True)
    c = sub.add_parser("check")
    c.add_argument("--integrated", required=True)
    c.add_argument("--design", required=True)
    args = ap.parse_args()
    integrated = Path(args.integrated)
    if not integrated.is_file():
        print(f"integrated.md が無い: {integrated}", file=sys.stderr)
        return 2
    try:
        if args.cmd == "build":
            return build(integrated, Path(args.out))
        design = Path(args.design)
        if not design.is_file():
            print(f"design.md が無い: {design}", file=sys.stderr)
            return 2
        return check(integrated, design)
    except ValueError as e:
        print(f"入力不正: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())

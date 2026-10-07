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
"""user-intent reviewer の §5（裁定引用の含意チェック）を「形」だけ機械検査する関所。

節の有無しか見ていなかったため、PR #1457 では対象10決定中7件が「読み1／読み2／両立判定」を
欠いたまま統合側で「回収済み」とまとめられた（moorestech-fjr19.2）。本スクリプトは決定ごとに
読み1・読み2が揃い、各読みに「→ 両立／不両立」の判定が付いているかだけを見る。中身の妥当性は
判定しない（ユーザー裁定 2026-10-07: 中身を判定する追加エージェント案は棄却）。
逐語引用が無い決定の「含意検査不能」行は様式どおりなので通す。

使い方:
  s5_shape_gate.py --report <agents/rev-core-any-user-intent-fulfillment.md>   … reviewer 報告だけ検査
  s5_shape_gate.py <RUNDIR>   … 報告に加え、形の欠けた §5 を integrated.md が「回収」扱いしていないか検査
終了コード: 0=形が揃っている / 1=差し戻し（理由は stderr と stdout JSON）/ 2=引数誤り

Shape-only gate for §5 of the user-intent reviewer: every decision needs reading 1 and reading 2,
each with a compatibility verdict. Content is not judged (user ruling 2026-10-07).
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPORT_NAME = "rev-core-any-user-intent-fulfillment.md"
SYSTEM_KEY = "user-intent-fulfillment"
S5_HEADING_RE = re.compile(r"^##\s*§5\s*裁定引用の含意チェック")
RECOVERY_HEADING_RE = re.compile(r"^##\s*系統別回収状況")
TARGET_COUNT_RE = re.compile(r"対象\s*[:：]\s*あり[（(]\s*決定\s*(\d+)\s*件")
READING_RE = re.compile(r"読み\s*(\d+)\s*[:：](.*?)(?=\s/\s*(?:読み\s*\d|棄却案)|$)")
VERDICT_RE = re.compile(r"→.*?(?:不両立|両立)")
UNVERIFIABLE_MARK = "含意検査不能"
REJECT_MARK_RE = re.compile(r"差し戻し|欠員(?!\s*なし)")
NOTE_PREFIXES = ("備考", "注記")


def section(text: str, heading_re: re.Pattern[str]) -> list[str] | None:
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if heading_re.match(line.strip()):
            body = []
            for rest in lines[i + 1:]:
                if rest.startswith("## "):
                    break
                body.append(rest)
            return body
    return None


def decision_bullets(body: list[str]) -> list[str]:
    # 字下げの続き行（入れ子の「- 読み1:」等）は「 / 」区切りで直前の決定へ連結する。備考行は決定に数えない
    # Indented continuation lines join the previous decision with " / "; 備考 bullets are not decisions
    bullets: list[str] = []
    for line in body:
        if line.startswith("- "):
            bullets.append(line[2:].strip())
        elif bullets and line.startswith((" ", "\t")) and line.strip():
            bullets[-1] += " / " + re.sub(r"^[-*]\s+", "", line.strip())
    return [b for b in bullets if not b.startswith(NOTE_PREFIXES)]


def decision_problem(bullet: str) -> str | None:
    if UNVERIFIABLE_MARK in bullet:
        return None
    readings = {int(n): seg for n, seg in READING_RE.findall(bullet)}
    absent = [f"読み{n}" for n in (1, 2) if n not in readings]
    if absent:
        return "・".join(absent) + "が無い"
    unjudged = [f"読み{n}" for n in sorted(readings) if not VERDICT_RE.search(readings[n])]
    if unjudged:
        return "・".join(unjudged) + "に「→ 両立／不両立」の判定が無い"
    return None


def report_violations(text: str) -> list[str]:
    body = section(text, S5_HEADING_RE)
    if body is None:
        return ["`## §5 裁定引用の含意チェック` 節が無い"]
    joined = "\n".join(body)
    bullets = decision_bullets(body)
    count = TARGET_COUNT_RE.search(joined)
    if count is None:
        if re.search(r"対象\s*[:：]\s*なし", joined) and not bullets:
            return []
        return ["`対象: なし` か `対象: あり（決定 N 件）` の行が無い"]
    problems = []
    if int(count.group(1)) != len(bullets):
        problems.append(f"対象 {count.group(1)} 件と書いたが決定行は {len(bullets)} 件")
    for i, bullet in enumerate(bullets, 1):
        reason = decision_problem(bullet)
        if reason:
            problems.append(f"決定{i}「{bullet[:40]}…」: {reason}")
    return problems


def integrated_violations(text: str) -> list[str]:
    # 形の欠けた §5 を統合側が「回収」とまとめていないか / The integrator must not mark it recovered
    body = section(text, RECOVERY_HEADING_RE) or []
    rows = [line for line in body if SYSTEM_KEY in line]
    if not rows:
        return ["integrated.md の系統別回収状況に user-intent 系統の行が無い"]
    bad = [row.strip() for row in rows if not REJECT_MARK_RE.search(row)]
    return [f"integrated.md が §5 形式欠落の系統を差し戻し／欠員と書いていない: {row[:80]}" for row in bad]


def check_run_dir(run_dir: Path) -> dict:
    report = run_dir / "agents" / REPORT_NAME
    if report.is_file():
        rep = report_violations(report.read_text(encoding="utf-8", errors="replace"))
    else:
        rep = [f"{report} が無い"]
    integrated = run_dir / "integrated.md"
    integ = []
    if rep and integrated.is_file():
        integ = integrated_violations(integrated.read_text(encoding="utf-8", errors="replace"))
    return {"report": str(report), "report_violations": rep, "integrated_violations": integ}


def main(argv: list[str]) -> int:
    if len(argv) == 2 and argv[0] == "--report":
        result = {"report": argv[1], "integrated_violations": [],
                  "report_violations": report_violations(Path(argv[1]).read_text(encoding="utf-8", errors="replace"))}
    elif len(argv) == 1 and Path(argv[0]).is_dir():
        result = check_run_dir(Path(argv[0]))
    else:
        print("usage: s5_shape_gate.py <RUNDIR> | --report <report.md>", file=sys.stderr)
        return 2
    result["ok"] = not result["report_violations"] and not result["integrated_violations"]
    print(json.dumps(result, ensure_ascii=False, indent=1))
    if result["ok"]:
        return 0
    print(f"s5-shape-gate: §5 の形が欠けているため差し戻し（{result['report']}）。"
          "各決定に「読み1: … → 両立|不両立 / 読み2: … → 両立|不両立」を書き直させ、統合では回収済みにしない:",
          file=sys.stderr)
    for problem in result["report_violations"] + result["integrated_violations"]:
        print(f"  - {problem}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

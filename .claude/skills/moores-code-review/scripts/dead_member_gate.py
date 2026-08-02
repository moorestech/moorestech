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
"""dead_member_gate.py — DeadMemberAudit(IL解析)の結果をpatchスコープへ絞る配線層。

moores-code-review Step 2.5 から呼ばれる。tools/DeadMemberAudit を実行し、
「参照0」「非production参照のみ」のメンバーのうち宣言場所がpatchの変更ファイルに
含まれるものだけを candidates.dead_member として出力する（裁定はdead-member-verifier）。

ScriptAssemblies が無い環境（素のレビューworktree等）では status: skipped を返す。
変更.csがDLLより新しい場合は status: stale を返す（先に uloop compile が必要）。

Wires DeadMemberAudit (IL analysis) into the review flow, scoped to the patch.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from patch_util import parse_patch  # noqa: E402

ASSEMBLIES_REL = "moorestech_client/Library/ScriptAssemblies"
TOOL_REL = "tools/DeadMemberAudit"
# 宣言場所セル `path:line` を取り出す / Extract the `path:line` declaration cell
LOCATION_RE = re.compile(r"`([^`]+\.cs):(\d+)`")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patch")
    ap.add_argument("--repo-root", required=True)
    ap.add_argument("--skip-run", action="store_true",
                    help="既存のreport.mdを再利用（テスト用） / Reuse existing report.md (for tests)")
    args = ap.parse_args()

    root = Path(args.repo_root).resolve()
    assemblies = root / ASSEMBLIES_REL
    report_path = root / TOOL_REL / "report.md"

    # patchの変更.csファイル集合を作る / Collect changed .cs files from the patch
    patch_text = Path(args.patch).read_text(encoding="utf-8", errors="replace")
    changed = {f.path for f in parse_patch(patch_text) if f.path.endswith(".cs")}
    if not changed:
        return emit("skipped", "patchに.cs変更なし", [])

    # DLLが無い環境ではゲート自体を縮退させる / Degrade gracefully when DLLs are absent
    if not assemblies.is_dir():
        return emit("skipped", f"{ASSEMBLIES_REL} が存在しない（Unity未コンパイル環境）", [])

    # 変更.csがDLLより新しければ結果は信用できない / Stale DLLs make results untrustworthy
    if not args.skip_run:
        cs_mtimes = [(root / p).stat().st_mtime for p in changed if (root / p).is_file()]
        dll_mtimes = [d.stat().st_mtime for d in assemblies.glob("*.dll")]
        if cs_mtimes and dll_mtimes and max(cs_mtimes) > max(dll_mtimes):
            return emit("stale", "変更.csがDLLより新しい。uloop compile 後に再実行すること", [])
        run = subprocess.run(
            ["dotnet", "run", "--project", str(root / TOOL_REL), "--", str(assemblies)],
            capture_output=True, text=True, cwd=str(root), timeout=600)
        if run.returncode != 0:
            return emit("error", f"DeadMemberAudit失敗: {run.stderr.strip()[:300]}", [])

    if not report_path.is_file():
        return emit("error", "report.md が生成されていない", [])
    candidates = collect_candidates(report_path.read_text(encoding="utf-8"), changed)
    return emit("ok", f"patch内.cs {len(changed)}件と突き合わせ", candidates)


def collect_candidates(report: str, changed: set) -> list:
    # リスト1/2のテーブル行のうち宣言場所がpatch変更ファイルのものを拾う
    # Keep list-1/2 rows whose declaration file is in the patch
    candidates = []
    section = None
    for line in report.splitlines():
        if line.startswith("## リスト1"):
            section = "dead-member-unused"
        elif line.startswith("## リスト2"):
            section = "dead-member-nonproduction"
        elif line.startswith("## "):
            section = None
        if section is None or not line.startswith("|"):
            continue
        m = LOCATION_RE.search(line)
        if not m or m.group(1) not in changed:
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        candidates.append({
            "rule": section,
            "file": m.group(1),
            "line": int(m.group(2)),
            "member": cells[1].strip("`") if len(cells) > 1 else "",
            "referencers": cells[4] if section == "dead-member-nonproduction" and len(cells) > 4 else "",
        })
    return candidates


def emit(status: str, note: str, candidates: list) -> int:
    print(json.dumps({"status": status, "note": note,
                      "candidates": candidates}, ensure_ascii=False, indent=1))
    # skipped/staleは失敗ではない（オーケストレータが縮退記録する） / Degradations are not failures
    return 0 if status in ("ok", "skipped", "stale") else 1


if __name__ == "__main__":
    sys.exit(main())

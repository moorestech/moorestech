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
"""ts_dead_code_gate.py — knip(静的解析)によるwebui死コード検知をpatchスコープへ絞る配線層。

moores-code-review Step 2.6 から呼ばれる（check_all.py が同時実行）。
moorestech_web/webui で knip を2モード実行し、patchが触った .ts/.tsx のものだけを
candidates.ts_dead_code として出力する（裁定は ts-dead-code-verifier）:
  - 通常モード      → どこからも参照されないファイル/export（rule: ts-dead-file / ts-dead-export）
  - --production    → テスト・e2e・開発コードからしか参照されないもの
                      （通常モードとの差分。rule: ts-nonproduction-file / ts-nonproduction-export。
                       C#側 DeadMemberAudit の dead-member-nonproduction と対称）

webuiのts/tsx変更が無いpatchでは status: skipped（knip自体を実行しない・0秒）。
knip未インストール環境も status: skipped で縮退を明示する。

Wires knip static analysis into the review flow, scoped to the patch.
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from patch_util import parse_patch  # noqa: E402

WEBUI_REL = "moorestech_web/webui"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patch")
    ap.add_argument("--repo-root", required=True)
    args = ap.parse_args()

    root = Path(args.repo_root)
    changed = webui_ts_files(args.patch)
    if not changed:
        emit({"status": "skipped", "note": "webuiのts/tsx変更なし", "candidates": []})
        return 0
    if not (root / WEBUI_REL / "node_modules/.bin/knip").exists():
        emit({"status": "skipped", "note": "knip未インストール（webuiでpnpm installが必要）",
              "candidates": []})
        return 0

    default_issues = run_knip(root, production=False)
    prod_issues = run_knip(root, production=True)
    if default_issues is None or prod_issues is None:
        emit({"status": "error", "note": "knip実行失敗（webuiでpnpm exec knipを直接確認）",
              "candidates": []})
        return 0

    # 通常モード=完全な死コード、productionモードとの差分=テスト専用参照
    # Default mode = fully dead; production-minus-default = referenced only by tests/dev code
    dead = collect(default_issues, "ts-dead")
    nonprod = [c for c in collect(prod_issues, "ts-nonproduction")
               if key_of(c) not in {key_of(d) for d in dead}]

    candidates = [c for c in dead + nonprod if c["file"] in changed]
    for c in candidates:
        c["file"] = f"{WEBUI_REL}/{c['file']}"
    emit({"status": "ok",
          "note": f"patch内webui ts/tsx {len(changed)}件と突き合わせ（knip全体: 死{len(dead)}・非production{len(nonprod)}）",
          "candidates": candidates})
    return 0


#region Internal


def webui_ts_files(patch_path: str) -> set[str]:
    prefix = WEBUI_REL + "/"
    files = set()
    for f in parse_patch(Path(patch_path).read_text(encoding="utf-8", errors="replace")):
        p = f.path
        if p.startswith(prefix) and p.endswith((".ts", ".tsx")):
            files.add(p[len(prefix):])
    return files


def run_knip(root: Path, production: bool) -> list | None:
    cmd = ["pnpm", "exec", "knip", "--reporter", "json"]
    if production:
        cmd.append("--production")
    run = subprocess.run(cmd, cwd=root / WEBUI_REL,
                         capture_output=True, text=True, timeout=600)
    # knipは指摘が1件でもあるとexit 1を返すため、returncodeでなく出力で判定する
    # knip exits 1 whenever issues exist, so judge by output shape instead of return code
    out = run.stdout.strip()
    if not out.startswith("{"):
        return None
    return json.loads(out).get("issues", [])


def collect(issues: list, rule_prefix: str) -> list[dict]:
    rows: list[dict] = []
    for issue in issues:
        path = issue.get("file", "")
        if not path.endswith((".ts", ".tsx")):
            continue
        if issue.get("files"):
            rows.append({"rule": f"{rule_prefix}-file", "file": path, "line": 1, "name": path})
        for kind in ("exports", "types", "enumMembers", "namespaceMembers"):
            for e in issue.get(kind) or []:
                rows.append({"rule": f"{rule_prefix}-export", "file": path,
                             "line": e.get("line", 1), "name": e.get("name", "?")})
    return rows


def key_of(c: dict) -> tuple:
    return (c["file"], c["line"], c["name"])


def emit(obj: dict) -> None:
    print(json.dumps(obj, ensure_ascii=False, indent=1))


#endregion


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""「独立レビュー&対応完了」ラベルの付与条件を verdict ごとに機械判定し、通った場合だけ gh pr edit を実行する。
Decide per verdict whether the "review & response done" label may be applied, and run gh pr edit only when it passes.

使い方 / Usage:
    label_gate.py --pr <番号> --rundir <$RUNDIR> [--prwt <PR worktree>] [--fixed-commit <sha> ...]
                  [--head-oid <sha>] [--repo moorestech/moorestech] [--dry-run]

verdict 別の条件（SKILL.md Step 10）/ Conditions per verdict:
  自動マージ可       … 無条件で付けてよい
  Critical差し戻し   … --fixed-commit を1つ以上受け取り、全てが現在の PR head の祖先（push済み）であること
  新形につき裁定行き … $RUNDIR/adjudications.json が completed:true であること（裁定が出ていること）
  未測定（スタブ）   … 付けない
exit 0  … 付与した（--dry-run では付与せず判定だけ）
exit 30 … Critical差し戻しで修正コミット未指定 / push 未確認
exit 31 … 裁定行きで裁定が未完了
exit 32 … スタブ verdict
exit 33 … findings.json が読めない / verdict 不明
exit 34 … PR head の取得に失敗（gh 不在・未認証）
--head-oid は gh を経由せず head を与える手段（テスト・オフライン用）。
"""
import argparse
import json
import os
import subprocess
import sys

LABEL_DONE = "独立レビュー&対応完了"
LABEL_WAITING = "独立レビュー待ち"
VERDICT_AUTO = "自動マージ可"
VERDICT_REJECT = "Critical差し戻し"
VERDICT_RULING = "新形につき裁定行き"
VERDICT_STUB = "未測定（スタブ）"


def run(cmd: list[str]) -> subprocess.CompletedProcess:
    # 外部境界（gh / git プロセス起動）/ External boundary: spawning gh / git
    return subprocess.run(cmd, capture_output=True, text=True)


def fail(code: int, message: str) -> int:
    sys.stderr.write(f"label_gate: {message}\n")
    return code


def load_json(path: str) -> dict | None:
    # 外部境界（他プロセスが書いた JSON の読み取り）/ External boundary: JSON written by another process
    try:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError):
        return None
    return data if isinstance(data, dict) else None


def current_head(args: argparse.Namespace) -> str | None:
    if args.head_oid:
        return args.head_oid
    view = run(["gh", "pr", "view", str(args.pr), "--repo", args.repo, "--json", "headRefOid", "--jq", ".headRefOid"])
    if view.returncode != 0 or not view.stdout.strip():
        return None
    return view.stdout.strip()


def fixes_pushed(args: argparse.Namespace, head: str) -> bool:
    # 修正コミットが全て PR head の祖先＝push 済み / Every fix commit must be an ancestor of the PR head
    if not args.prwt:
        return False
    run(["git", "-C", args.prwt, "fetch", "origin", f"pull/{args.pr}/head"])
    for sha in args.fixed_commit:
        check = run(["git", "-C", args.prwt, "merge-base", "--is-ancestor", sha, head])
        if check.returncode != 0:
            return False
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--pr", required=True, type=int)
    ap.add_argument("--rundir", required=True)
    ap.add_argument("--prwt", default="")
    ap.add_argument("--fixed-commit", action="append", default=[])
    ap.add_argument("--head-oid", default="")
    ap.add_argument("--repo", default="moorestech/moorestech")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    findings = load_json(os.path.join(args.rundir, "findings.json"))
    verdict = findings.get("verdict") if findings else None
    if verdict not in (VERDICT_AUTO, VERDICT_REJECT, VERDICT_RULING, VERDICT_STUB):
        return fail(33, f"findings.json の verdict が読めない: {verdict!r}")

    # verdict 別の付与条件 / Per-verdict conditions
    if verdict == VERDICT_STUB:
        return fail(32, "未測定（スタブ）には完了の合図を出さない")
    if verdict == VERDICT_REJECT:
        if not args.fixed_commit:
            return fail(30, "Critical差し戻しは修正コミット（--fixed-commit）の push 確認が要る")
        head = current_head(args)
        if head is None:
            return fail(34, "PR head を取得できない（gh 不在・未認証なら --head-oid で渡す）")
        if not fixes_pushed(args, head):
            return fail(30, f"修正コミットが PR head {head} の祖先ではない（未push・ローカルのみ）")
    if verdict == VERDICT_RULING:
        adjudications = load_json(os.path.join(args.rundir, "adjudications.json"))
        if not adjudications or not adjudications.get("completed"):
            return fail(31, "新形につき裁定行きは裁定完了（adjudications.json completed:true）まで付けない")

    print(json.dumps({"pr": args.pr, "verdict": verdict, "label": LABEL_DONE, "applied": not args.dry_run}, ensure_ascii=False))
    if args.dry_run:
        return 0
    edit = run(["gh", "pr", "edit", str(args.pr), "--repo", args.repo,
                "--add-label", LABEL_DONE, "--remove-label", LABEL_WAITING])
    if edit.returncode != 0:
        return fail(34, f"gh pr edit 失敗: {edit.stderr.strip()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

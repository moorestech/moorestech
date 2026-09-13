#!/usr/bin/env python3
"""PR差分の patch を固定フラグで生成し、散文で守らせていた $PRWT / base のガードを終了コードで裁く。
Generate the PR patch with the fixed flag set and enforce the former prose guards on $PRWT / base via exit codes.

使い方 / Usage:
    make_patch.py --prwt <PR worktree> --origin <起動元repo> --pr <番号> --base-ref <BASE_REF> --out <patch.diff>

stdout に JSON: {"patch", "diff_count", "head", "base_sha", "adr_files_in_diff"}
  adr_files_in_diff … PR diff 自身が追加・変更した docs/superpowers/ 配下のファイル（Step 4 で [agent前提] へ降格する引用元）
exit 0  … 生成完了
exit 20 … $PRWT が $ORIGIN と同一（$ORIGIN で gh pr checkout した・共用worktreeで走らせた経路）
exit 21 … $PRWT のディレクトリ名が pr-<番号> ではない（PRごとの専用worktreeを使っていない）
exit 22 … BASE_REF が解決できない
exit 23 … merge-base(BASE_REF, HEAD) が HEAD 自身（base取り違え。MERGED PR で origin/<base> を使った典型）
exit 24 … patch が空（`^diff` が0行。git diff は空でも exit 0 なのでここが唯一の検知点）
"""
import argparse
import json
import os
import subprocess
import sys

EXCLUDES = [
    ":(exclude)*.meta", ":(exclude)*.prefab", ":(exclude)*.asset", ":(exclude)*.unity",
    ":(exclude)*.png", ":(exclude)*.jpg", ":(exclude)*.controller", ":(exclude)*.mat", ":(exclude)*.fbx",
    ":(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs",
]
# ユーザー側git設定（quotepath/color/ext-diff/textconv/バイナリ判定/rename圧縮）に patch を痩せさせないための固定フラグ
# Fixed flags so user git config (quotepath/color/ext-diff/textconv/binary/rename) cannot silently thin the patch
DIFF_FLAGS = ["-c", "core.quotepath=false", "diff", "--no-color", "--no-ext-diff", "--no-textconv", "--text", "--no-renames"]
ADR_DIR = "docs/superpowers/"


def git(repo: str, *args: str) -> subprocess.CompletedProcess:
    # 外部境界（git プロセス起動）/ External boundary: spawning git
    return subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True)


def fail(code: int, message: str) -> int:
    sys.stderr.write(f"make_patch: {message}\n")
    return code


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--prwt", required=True)
    ap.add_argument("--origin", required=True)
    ap.add_argument("--pr", required=True, type=int)
    ap.add_argument("--base-ref", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    prwt = os.path.realpath(args.prwt)
    origin = os.path.realpath(args.origin)

    # tree ガード: $PRWT は $ORIGIN と別で、名前が pr-<番号> の専用worktree
    # Tree guards: $PRWT must differ from $ORIGIN and be the dedicated worktree named pr-<number>
    if prwt == origin:
        return fail(20, f"$PRWT が $ORIGIN と同一: {prwt}（$ORIGIN で gh pr checkout していないか）")
    if os.path.basename(prwt) != f"pr-{args.pr}":
        return fail(21, f"$PRWT のディレクトリ名が pr-{args.pr} ではない: {prwt}")

    # base ガード: 解決できること・HEAD の祖先で merge-base が HEAD 自身にならないこと
    # Base guards: BASE_REF resolves and merge-base is not HEAD itself
    base = git(prwt, "rev-parse", "--verify", f"{args.base_ref}^{{commit}}")
    if base.returncode != 0:
        return fail(22, f"BASE_REF が解決できない: {args.base_ref}")
    base_sha = base.stdout.strip()
    head_sha = git(prwt, "rev-parse", "HEAD").stdout.strip()
    merge_base = git(prwt, "merge-base", base_sha, "HEAD").stdout.strip()
    if merge_base == head_sha:
        return fail(23, f"merge-base が HEAD 自身: BASE_REF={args.base_ref}（MERGED PR で origin/<base> を使っていないか）")

    # patch 生成と非空ガード / Generate the patch and require it to be non-empty
    diff = git(prwt, *DIFF_FLAGS, f"{base_sha}...HEAD", "--", ".", *EXCLUDES)
    if diff.returncode != 0:
        return fail(22, f"git diff 失敗: {diff.stderr.strip()}")
    diff_count = sum(1 for line in diff.stdout.splitlines() if line.startswith("diff "))
    if diff_count == 0:
        return fail(24, "patch が空（base指定ミスまたはpatch取得失敗）。空patchのまま先へ進まない")
    os.makedirs(os.path.dirname(os.path.realpath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(diff.stdout)

    # PR 内で新設・変更された ADR の引用元一覧 / ADR sources the PR itself added or changed
    names = git(prwt, "diff", f"{base_sha}...HEAD", "--name-only", "--", ADR_DIR).stdout.split()
    print(json.dumps({
        "patch": os.path.realpath(args.out),
        "diff_count": diff_count,
        "head": head_sha,
        "base_sha": base_sha,
        "adr_files_in_diff": names,
    }, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())

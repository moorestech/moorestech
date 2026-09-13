#!/usr/bin/env python3
"""masterコンフリクトの事前検知。`git merge-tree --write-tree` で衝突有無だけを判定し、解消はしない。
Pre-check for master conflicts: decide conflict/no-conflict via `git merge-tree --write-tree`; never resolves.

無衝突なら subagent を起動せずに済むための関所（ユーザー裁定 2026-09-13）。
Gate so that a conflict-free PR skips the resolution subagent entirely (user ruling 2026-09-13).

exit 0 = 衝突なし / 1 = 衝突あり / 2 = git 失敗。stdout は常に JSON。
exit 0 = no conflict / 1 = conflict / 2 = git failure. stdout is always JSON.
"""
import argparse
import fnmatch
import json
import subprocess
import sys

# 機械的に解消するファイル（conflict-preflight-agent.md の表と同じ集合）
# Files resolved mechanically (same set as the table in conflict-preflight-agent.md)
MECHANICAL_PATTERNS = (
    ".moorestech-external-revisions.json",
    "moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs",
    "moorestech_client/.uloop/tools.json",
    ".superpowers/*",
    "docs/superpowers/*",
)


def run_git(repo: str, *args: str) -> subprocess.CompletedProcess:
    # 外部境界（git プロセス起動）のため例外を握らず CompletedProcess で返す
    # External boundary (git process); return CompletedProcess without raising
    return subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True)


def is_mechanical(path: str) -> bool:
    return any(fnmatch.fnmatch(path, p) or path == p for p in MECHANICAL_PATTERNS)


def refspec_for(base: str) -> str:
    # `origin/<name>` を `+refs/heads/<name>:refs/remotes/origin/<name>` へ組む
    # Build the explicit refspec for `origin/<name>`
    name = base.split("/", 1)[1] if base.startswith("origin/") else base
    return f"+refs/heads/{name}:refs/remotes/origin/{name}"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--repo", required=True)
    ap.add_argument("--base", default="origin/master")
    ap.add_argument("--fetch", dest="fetch", action="store_true", default=True)
    ap.add_argument("--no-fetch", dest="fetch", action="store_false")
    args = ap.parse_args()

    if args.fetch:
        fetched = run_git(args.repo, "fetch", "origin", refspec_for(args.base))
        if fetched.returncode != 0:
            sys.stderr.write(fetched.stderr)
            return 2

    head = run_git(args.repo, "rev-parse", "HEAD")
    base = run_git(args.repo, "rev-parse", f"{args.base}^{{commit}}")
    if head.returncode != 0 or base.returncode != 0:
        sys.stderr.write(head.stderr + base.stderr)
        return 2

    # merge-tree は 0=clean / 1=conflict / それ以外=error。-z 出力は tree, NUL, ファイル名群, NUL, メッセージ群
    # merge-tree exits 0=clean / 1=conflict / other=error; -z output is tree NUL names NUL messages
    merged = run_git(args.repo, "merge-tree", "--write-tree", "--name-only", "-z", "HEAD", args.base)
    if merged.returncode not in (0, 1):
        sys.stderr.write(merged.stderr)
        return 2

    files: list = []
    if merged.returncode == 1:
        sections = merged.stdout.split("\0\0")
        names = sections[0].split("\0")[1:] if sections else []
        files = sorted({n for n in names if n})

    result = {
        "conflict": merged.returncode == 1,
        "files": files,
        "mechanical_only": bool(files) and all(is_mechanical(f) for f in files),
        "head": head.stdout.strip(),
        "base": base.stdout.strip(),
    }
    print(json.dumps(result, ensure_ascii=False))
    return 1 if result["conflict"] else 0


if __name__ == "__main__":
    sys.exit(main())

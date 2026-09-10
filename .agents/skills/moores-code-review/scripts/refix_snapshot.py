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
"""refix_snapshot.py — 反映 diff（修正適用の前後差分）を作業ツリーに触れずに作る。

所有する状態: なし。入力は git 作業ツリーと `$RUNDIR/refix/`、出力は同ディレクトリのファイルと stdout の JSON。

なぜ必要か: レビューの出力を反映した diff は、どの工程の入力にもならず誰にも再レビューされない
（2026-09-08 cmux-connector c9baa79: 裁定「案 B」の反映が判定式の評価時点を誤り 2 日間の機能停止）。
再レビューの対象は「反映で変わった行だけ」であり、これは patch.diff と final.diff の差（interdiff）で、
未コミット状態同士の差分を git の通常コマンドでは一発で作れない。そこで一時 index で作業ツリー全体
（未追跡ファイル含む・ignored 除く）を commit object 化して snapshot とし、snapshot 間の `git diff` で作る。
HEAD・index・作業ツリーは一切変更しない（`git stash create` は intent-to-add や未追跡ファイルの扱いが
git 版で揺れるため、2026-09-08 の暫定手順から置き換えた）。

Why: a diff that applies review output is never an input to any stage and thus never re-reviewed
(2026-09-08 cmux-connector c9baa79). The target is the interdiff between two dirty worktree states, which
plain git cannot produce in one step, so we commit the whole worktree (untracked included, ignored excluded)
through a temporary index and diff the snapshots. HEAD, index and worktree stay untouched.

使い方 / usage:
  refix_snapshot.py snapshot --repo-root R --run-dir D --name s0        # D/refix/s0.sha へ commit SHA
  refix_snapshot.py diff --repo-root R --run-dir D --from s0 --to s1 --out D/refix/round1.diff
      → stdout に JSON {scope, diff, files, source_files, changed_lines, from, to}
      scope: source（再レビュー必須）/ non-source（doc・テスト・コメントのみ）/ none（差分なし）。
      判定は fail-closed: 分類できない変更は source に倒す。

終了コード: 0 成功 / 2 引数不正 / 3 git 失敗。
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path

# テスト・doc の判定（path 由来）。source 側へ倒すのが既定なので、ここは「確実に非ソース」だけを列挙する
# Test/doc detection by path. Default falls to source, so list only what is certainly non-source
TEST_SEGMENTS = {"test", "tests", "__tests__", "spec", "specs", "e2e", "fixtures"}
TEST_NAME_RE = re.compile(r"(^test_.*\.py$|\.test\.|\.spec\.|_test\.|Tests?\.cs$)")
DOC_SUFFIXES = {".md", ".markdown", ".rst", ".adoc", ".txt"}
COMMENT_PREFIXES = ("//", "#", "/*", "*/", "*", "///", "--", "<!--", "-->", "'''", '"""')


def git(repo: Path, args: list[str], env: dict | None = None) -> str:
    run = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True, env=env)
    if run.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed ({run.returncode}): {run.stderr.strip()[:400]}")
    return run.stdout.strip()


def snapshot(repo: Path, run_dir: Path, name: str) -> str:
    refix_dir = run_dir / "refix"
    refix_dir.mkdir(parents=True, exist_ok=True)
    index = refix_dir / f"{name}.index"
    if index.exists():
        index.unlink()
    env = dict(os.environ, GIT_INDEX_FILE=str(index))
    head = git(repo, ["rev-parse", "HEAD"])
    # 一時 index を HEAD で初期化してから作業ツリー全体を add する（未追跡を含め、.gitignore は尊重される）
    # Seed the temporary index from HEAD, then add the whole worktree (untracked included, .gitignore honored)
    git(repo, ["read-tree", head], env)
    git(repo, ["add", "-A", "--", "."], env)
    tree = git(repo, ["write-tree"], env)
    sha = git(repo, ["commit-tree", tree, "-p", head, "-m", f"moores-code-review refix snapshot {name}"], env)
    (refix_dir / f"{name}.sha").write_text(sha + "\n", encoding="utf-8")
    index.unlink(missing_ok=True)
    return sha


def read_sha(run_dir: Path, name: str) -> str:
    path = run_dir / "refix" / f"{name}.sha"
    if not path.is_file():
        raise RuntimeError(f"snapshot が無い: {path}（先に `snapshot --name {name}` を実行する）")
    return path.read_text(encoding="utf-8").strip()


def is_non_source_path(path: str) -> bool:
    # moorestech は `Tests/` `Client.Tests/` のように大文字始まりのテストディレクトリなので小文字化して比較する
    # moorestech test directories are capitalized (`Tests/`, `Client.Tests/`), so compare lower-cased segments
    parts = Path(path).parts
    if any(seg.lower() in TEST_SEGMENTS or seg.lower().endswith(".tests") for seg in parts[:-1]):
        return True
    if TEST_NAME_RE.search(parts[-1]):
        return True
    if Path(path).suffix.lower() in DOC_SUFFIXES:
        return True
    return "docs" in parts[:-1]


def is_comment_or_blank(line: str) -> bool:
    body = line[1:].strip()
    return body == "" or body.startswith(COMMENT_PREFIXES)


def classify(diff_text: str) -> dict:
    files: list[str] = []
    source_files: list[str] = []
    changed = 0
    current = None
    current_is_source_path = False
    current_has_code = False

    def close():
        nonlocal current, current_has_code
        if current is not None and current_is_source_path and current_has_code and current not in source_files:
            source_files.append(current)

    for line in diff_text.splitlines():
        m = re.match(r"^diff --git a/(.*) b/(.*)$", line)
        if m:
            close()
            current = m.group(2)
            files.append(current)
            current_is_source_path = not is_non_source_path(current)
            current_has_code = False
            continue
        if line.startswith(("+++", "---")):
            continue
        if line.startswith(("+", "-")):
            changed += 1
            if not is_comment_or_blank(line):
                current_has_code = True
        elif line.startswith("Binary files"):
            # バイナリは行が読めないので fail-closed で source 扱い / binary: unreadable, fail closed to source
            current_has_code = True
    close()
    scope = "none" if not files else ("source" if source_files else "non-source")
    return {"scope": scope, "files": files, "source_files": source_files, "changed_lines": changed}


def diff(repo: Path, run_dir: Path, src: str, dst: str, out: Path) -> dict:
    a, b = read_sha(run_dir, src), read_sha(run_dir, dst)
    text = git(repo, ["diff", "--no-color", "--no-ext-diff", a, b])
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text + ("\n" if text else ""), encoding="utf-8")
    result = classify(text)
    result.update({"diff": str(out), "from": a, "to": b})
    return result


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("snapshot")
    s.add_argument("--repo-root", required=True)
    s.add_argument("--run-dir", required=True)
    s.add_argument("--name", required=True)
    d = sub.add_parser("diff")
    d.add_argument("--repo-root", required=True)
    d.add_argument("--run-dir", required=True)
    d.add_argument("--from", dest="src", required=True)
    d.add_argument("--to", dest="dst", required=True)
    d.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    if not re.fullmatch(r"[A-Za-z0-9_.-]+", getattr(args, "name", "x") or "x"):
        print("--name は英数字・_ . - のみ", file=sys.stderr)
        return 2
    repo, run_dir = Path(args.repo_root).resolve(), Path(args.run_dir).resolve()
    try:
        if args.cmd == "snapshot":
            sha = snapshot(repo, run_dir, args.name)
            print(json.dumps({"name": args.name, "sha": sha, "path": str(run_dir / "refix" / f"{args.name}.sha")}))
        else:
            print(json.dumps(diff(repo, run_dir, args.src, args.dst, Path(args.out).resolve()), ensure_ascii=False))
    except RuntimeError as e:
        print(str(e), file=sys.stderr)
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())

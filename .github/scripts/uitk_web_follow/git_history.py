"""Thin git wrappers the UITK Web follow gate reads history through."""

import subprocess
from dataclasses import dataclass
from pathlib import Path

WEB_ROOT = "moorestech_web/"


@dataclass(frozen=True)
class ChangedFile:
    status: str
    path: str


@dataclass(frozen=True)
class CommitFiles:
    sha: str
    files: tuple[str, ...]


def _git(root: Path, *args: str) -> str:
    # git は外部プロセス。失敗は CalledProcessError のまま上げて CI を赤にする
    # git is an external process; failures surface as CalledProcessError and turn CI red
    return subprocess.run(["git", "-c", "core.quotePath=false", *args], cwd=root, check=True, capture_output=True, text=True).stdout


def _succeeds(root: Path, *args: str) -> bool:
    return subprocess.run(["git", *args], cwd=root, capture_output=True).returncode == 0


def merge_base(root: Path, base: str, head: str) -> str:
    return _git(root, "merge-base", base, head).strip()


def resolve_commit(root: Path, rev: str) -> str | None:
    if not _succeeds(root, "rev-parse", "--verify", "--quiet", f"{rev}^{{commit}}"):
        return None
    return _git(root, "rev-parse", f"{rev}^{{commit}}").strip()


def is_ancestor(root: Path, ancestor: str, descendant: str) -> bool:
    # 終了コード 0=祖先・1=祖先でない。それ以外は git の失敗として落とす
    # Exit 0 means ancestor and 1 means not; anything else is a git failure and raises
    result = subprocess.run(["git", "merge-base", "--is-ancestor", ancestor, descendant], cwd=root, capture_output=True, text=True)
    if result.returncode not in (0, 1):
        raise subprocess.CalledProcessError(result.returncode, result.args, result.stdout, result.stderr)
    return result.returncode == 0


def read_file_at(root: Path, rev: str, path: str) -> str | None:
    # rev は解決済みの前提。ファイルが無いときだけ None
    # The revision is already resolved; None means only that the file is absent
    if not _succeeds(root, "cat-file", "-e", f"{rev}:{path}"):
        return None
    return _git(root, "show", f"{rev}:{path}")


def changed_files(root: Path, base: str, head: str) -> tuple[ChangedFile, ...]:
    # rename は削除と追加に分けて読む（追加側だけが検査に回る）
    # Renames split into delete plus add so only the added side is checked
    output = _git(root, "diff", "--name-status", "--no-renames", base, head, "--", WEB_ROOT)
    return tuple(ChangedFile(*line.split("\t", 1)) for line in output.splitlines() if line)


def web_commits(root: Path, start: str, head: str) -> tuple[CommitFiles, ...]:
    # start より後で Web を触った非マージコミットを、触ったファイルごと返す
    # Return non-merge commits after start that touched Web, with the files each touched
    output = _git(root, "log", "--full-history", "--no-merges", "--no-renames", "--format=@%H", "--name-only", f"{start}..{head}", "--", WEB_ROOT)
    commits: list[CommitFiles] = []
    sha: str | None = None
    files: list[str] = []
    for line in output.splitlines():
        if line.startswith("@"):
            if sha is not None:
                commits.append(CommitFiles(sha, tuple(files)))
            sha, files = line[1:], []
        elif line:
            files.append(line)
    if sha is not None:
        commits.append(CommitFiles(sha, tuple(files)))
    return tuple(commits)


def tracked_files(root: Path, rev: str, prefix: str) -> tuple[str, ...]:
    return tuple(line for line in _git(root, "ls-tree", "-r", "--name-only", rev, "--", prefix).splitlines() if line)

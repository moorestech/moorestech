# 一時gitリポジトリ（bare origin + 作業clone）でconflict_precheck.pyの判定を検証する
# Verify conflict_precheck.py against a throwaway bare origin + working clone
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "conflict_precheck.py"


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True, text=True).stdout


def _commit(repo: Path, path: str, text: str, msg: str) -> None:
    target = repo / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text)
    _git(repo, "add", "-A")
    _git(repo, "commit", "-q", "-m", msg)


@pytest.fixture()
def clone(tmp_path: Path) -> Path:
    # origin(bare) に master を1コミット置き、clone 側で PR ブランチを detached で切る
    # Seed bare origin with one master commit; clone checks out a detached PR head
    origin = tmp_path / "origin.git"
    subprocess.run(["git", "init", "-q", "--bare", "-b", "master", str(origin)], check=True)
    seed = tmp_path / "seed"
    subprocess.run(["git", "clone", "-q", str(origin), str(seed)], check=True)
    _git(seed, "config", "user.email", "t@t")
    _git(seed, "config", "user.name", "t")
    _git(seed, "checkout", "-q", "-b", "master")
    _commit(seed, "a.txt", "base\n", "base")
    _commit(seed, ".moorestech-external-revisions.json", '{"pin":"base"}\n', "pin")
    _git(seed, "push", "-q", "origin", "master")
    c = tmp_path / "clone"
    subprocess.run(["git", "clone", "-q", str(origin), str(c)], check=True)
    _git(c, "config", "user.email", "t@t")
    _git(c, "config", "user.name", "t")
    _git(c, "checkout", "-q", "--detach", "origin/master")
    return c


def _advance_master(clone: Path, path: str, text: str) -> None:
    # 別作業ツリーから origin/master を進める（PR head とは独立に）
    # Advance origin/master from a separate worktree, independent of the PR head
    other = clone.parent / "other"
    if not other.exists():
        subprocess.run(["git", "clone", "-q", str(clone.parent / "origin.git"), str(other)], check=True)
        _git(other, "config", "user.email", "t@t")
        _git(other, "config", "user.name", "t")
        _git(other, "checkout", "-q", "master")
    _commit(other, path, text, "master advance")
    _git(other, "push", "-q", "origin", "master")


def _run(clone: Path) -> tuple[int, dict]:
    proc = subprocess.run([sys.executable, str(SCRIPT), "--repo", str(clone)], capture_output=True, text=True)
    return proc.returncode, json.loads(proc.stdout)


def test_no_conflict_when_different_files(clone: Path) -> None:
    _commit(clone, "b.txt", "pr\n", "pr")
    _advance_master(clone, "c.txt", "master\n")
    code, out = _run(clone)
    assert code == 0
    assert out["conflict"] is False and out["files"] == [] and out["mechanical_only"] is False


def test_conflict_on_same_line(clone: Path) -> None:
    _commit(clone, "a.txt", "pr side\n", "pr")
    _advance_master(clone, "a.txt", "master side\n")
    code, out = _run(clone)
    assert code == 1
    assert out["conflict"] is True and out["files"] == ["a.txt"] and out["mechanical_only"] is False


def test_mechanical_only_conflict(clone: Path) -> None:
    _commit(clone, ".moorestech-external-revisions.json", '{"pin":"pr"}\n', "pr pin")
    _advance_master(clone, ".moorestech-external-revisions.json", '{"pin":"master"}\n')
    code, out = _run(clone)
    assert code == 1
    assert out["files"] == [".moorestech-external-revisions.json"] and out["mechanical_only"] is True


def test_mixed_conflict_is_not_mechanical_only(clone: Path) -> None:
    _commit(clone, "a.txt", "pr side\n", "pr")
    _commit(clone, "docs/superpowers/plan.md", "pr\n", "pr doc")
    _advance_master(clone, "a.txt", "master side\n")
    _advance_master(clone, "docs/superpowers/plan.md", "master\n")
    code, out = _run(clone)
    assert code == 1
    assert out["files"] == ["a.txt", "docs/superpowers/plan.md"] and out["mechanical_only"] is False


def test_git_failure_exit_2(tmp_path: Path) -> None:
    proc = subprocess.run([sys.executable, str(SCRIPT), "--repo", str(tmp_path), "--no-fetch"], capture_output=True, text=True)
    assert proc.returncode == 2

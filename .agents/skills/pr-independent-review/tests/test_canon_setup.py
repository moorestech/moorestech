# 一時 git リポジトリ（bare origin ＋ 作業クローン）で canon_setup.py のガードと終了コードを検証する
# Verify canon_setup.py guards and exit codes on a throwaway git repo (bare origin + working clone)
import json
import os
import subprocess
import sys
import time
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "canon_setup.py"
SKILL_MD = Path(".agents/skills/pr-independent-review/SKILL.md")
NOVELTY = Path(".agents/skills/pr-independent-review/scripts/novelty_gate.py")


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True, text=True).stdout


def _run(origin: Path, parent: Path, *extra: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "--origin", str(origin), "--parent", str(parent), *extra],
        capture_output=True, text=True,
    )


@pytest.fixture()
def origin(tmp_path: Path) -> Path:
    bare = tmp_path / "remote.git"
    _git(tmp_path, "init", "--bare", "-b", "master", str(bare))
    work = tmp_path / "origin"
    _git(tmp_path, "clone", "-q", str(bare), str(work))
    _git(work, "config", "user.email", "t@t")
    _git(work, "config", "user.name", "t")
    (work / SKILL_MD).parent.mkdir(parents=True)
    (work / SKILL_MD).write_text("# skill\n")
    (work / NOVELTY).parent.mkdir(parents=True)
    (work / NOVELTY).write_text("print('gate')\n")
    _git(work, "add", "-A")
    _git(work, "commit", "-q", "-m", "base")
    _git(work, "push", "-q", "origin", "master")
    return work


def test_creates_pin_and_reports(origin: Path, tmp_path: Path):
    parent = tmp_path / "wts"
    res = _run(origin, parent)
    assert res.returncode == 0, res.stderr
    out = json.loads(res.stdout)
    sha8 = _git(origin, "rev-parse", "--short=8", "origin/master").strip()
    assert out["canon"] == str(parent / f"skills-canon-{sha8}")
    assert out["skew"] is False and out["cleaned"] == []
    assert (Path(out["canon"]) / ".last-used").exists()
    assert (Path(out["canon"]) / NOVELTY).exists()


def test_reuses_existing_pin_without_touching_it(origin: Path, tmp_path: Path):
    parent = tmp_path / "wts"
    first = json.loads(_run(origin, parent).stdout)
    marker = Path(first["canon"]) / "marker.txt"
    marker.write_text("kept")
    second = _run(origin, parent)
    assert second.returncode == 0, second.stderr
    assert json.loads(second.stdout)["canon"] == first["canon"]
    assert marker.read_text() == "kept"


def test_skew_is_exit_13_unless_allowed(origin: Path, tmp_path: Path):
    parent = tmp_path / "wts"
    assert _run(origin, parent).returncode == 0
    (origin / SKILL_MD).write_text("# skill (unmerged local edit)\n")
    res = _run(origin, parent)
    assert res.returncode == 13
    assert json.loads(res.stdout)["skew"] is True
    allowed = _run(origin, parent, "--allow-skew")
    assert allowed.returncode == 0
    assert json.loads(allowed.stdout)["skew"] is True


def test_stale_pin_is_cleaned_and_fresh_pin_kept(origin: Path, tmp_path: Path):
    parent = tmp_path / "wts"
    parent.mkdir()
    stale = parent / "skills-canon-deadbeef"
    _git(origin, "worktree", "add", "-q", "--detach", str(stale), "HEAD")
    old = time.time() - 48 * 3600
    (stale / ".last-used").write_text("")
    os.utime(stale / ".last-used", (old, old))
    fresh = parent / "skills-canon-cafebabe"
    _git(origin, "worktree", "add", "-q", "--detach", str(fresh), "HEAD")
    (fresh / ".last-used").write_text("")
    res = _run(origin, parent)
    assert res.returncode == 0, res.stderr
    out = json.loads(res.stdout)
    assert out["cleaned"] == [str(stale)]
    assert not stale.exists() and fresh.exists()


def test_origin_must_be_a_worktree_root(origin: Path, tmp_path: Path):
    res = _run(origin / ".agents", tmp_path / "wts")
    assert res.returncode == 14


def test_missing_novelty_gate_is_exit_12(origin: Path, tmp_path: Path):
    _git(origin, "rm", "-q", str(NOVELTY))
    _git(origin, "commit", "-q", "-m", "drop gate")
    _git(origin, "push", "-q", "origin", "master")
    res = _run(origin, tmp_path / "wts")
    assert res.returncode == 12

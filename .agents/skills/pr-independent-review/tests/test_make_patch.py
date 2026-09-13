# 一時 git リポジトリで make_patch.py の tree / base / 非空ガードと patch 生成を検証する
# Verify make_patch.py tree / base / non-empty guards and patch generation on a throwaway git repo
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "make_patch.py"


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True, text=True).stdout


def _run(prwt: Path, origin: Path, pr: int, base_ref: str, out: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "--prwt", str(prwt), "--origin", str(origin),
         "--pr", str(pr), "--base-ref", base_ref, "--out", str(out)],
        capture_output=True, text=True,
    )


@pytest.fixture()
def repos(tmp_path: Path) -> tuple[Path, Path]:
    origin = tmp_path / "origin"
    origin.mkdir()
    _git(origin, "init", "-q", "-b", "master")
    _git(origin, "config", "user.email", "t@t")
    _git(origin, "config", "user.name", "t")
    (origin / "a.cs").write_text("class A {}\n")
    (origin / "img.png").write_bytes(b"\x89PNG\r\n")
    _git(origin, "add", "-A")
    _git(origin, "commit", "-q", "-m", "base")
    _git(origin, "tag", "basetag")
    prwt = tmp_path / "pr-42"
    _git(origin, "worktree", "add", "-q", "-b", "feature", str(prwt), "master")
    (prwt / "a.cs").write_text("class A { int x; }\n")
    (prwt / "img.png").write_bytes(b"\x89PNG\r\n\x00")
    adr = prwt / "docs" / "superpowers" / "specs" / "2026-01-01-x.md"
    adr.parent.mkdir(parents=True)
    adr.write_text("# adr\n")
    _git(prwt, "add", "-A")
    _git(prwt, "commit", "-q", "-m", "feature")
    return origin, prwt


def test_generates_patch_and_lists_adr_files(repos, tmp_path: Path):
    origin, prwt = repos
    out = tmp_path / "run" / "patch.diff"
    res = _run(prwt, origin, 42, "basetag", out)
    assert res.returncode == 0, res.stderr
    info = json.loads(res.stdout)
    text = out.read_text()
    assert info["diff_count"] == 2 and "a.cs" in text and "img.png" not in text
    assert info["adr_files_in_diff"] == ["docs/superpowers/specs/2026-01-01-x.md"]
    assert info["base_sha"] == _git(origin, "rev-parse", "basetag").strip()


def test_prwt_equal_to_origin_is_exit_20(repos, tmp_path: Path):
    origin, _ = repos
    assert _run(origin, origin, 42, "basetag", tmp_path / "p.diff").returncode == 20


def test_prwt_name_must_match_pr_number(repos, tmp_path: Path):
    origin, prwt = repos
    assert _run(prwt, origin, 43, "basetag", tmp_path / "p.diff").returncode == 21


def test_unresolvable_base_is_exit_22(repos, tmp_path: Path):
    origin, prwt = repos
    assert _run(prwt, origin, 42, "no-such-ref", tmp_path / "p.diff").returncode == 22


def test_base_equal_to_head_is_exit_23(repos, tmp_path: Path):
    origin, prwt = repos
    # マージ済みPRで origin/<base> を使った取り違えの再現（HEAD が base の祖先）
    # Reproduce the merged-PR mistake: HEAD is an ancestor of the chosen base
    _git(origin, "merge", "-q", "--no-ff", "-m", "merge", "feature")
    assert _run(prwt, origin, 42, "master", tmp_path / "p.diff").returncode == 23


def test_empty_patch_is_exit_24(repos, tmp_path: Path):
    origin, prwt = repos
    # 除外対象（png）だけを変えたコミットは patch が空になる / A commit touching only excluded files yields an empty patch
    (prwt / "only.png").write_bytes(b"\x89PNG")
    _git(prwt, "add", "-A")
    _git(prwt, "commit", "-q", "-m", "png only")
    head = _git(prwt, "rev-parse", "HEAD~1").strip()
    res = _run(prwt, origin, 42, head, tmp_path / "p.diff")
    assert res.returncode == 24
    assert not (tmp_path / "p.diff").exists()

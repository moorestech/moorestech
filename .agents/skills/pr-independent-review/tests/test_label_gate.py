# findings.json の verdict ごとにラベル付与の可否と終了コードを検証する（gh は --dry-run と --head-oid で迂回）
# Verify label_gate.py decisions and exit codes per verdict (gh bypassed via --dry-run and --head-oid)
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "label_gate.py"


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True, text=True).stdout


def _run(rundir: Path, *extra: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "--pr", "7", "--rundir", str(rundir), "--dry-run", *extra],
        capture_output=True, text=True,
    )


def _findings(rundir: Path, verdict: str) -> None:
    rundir.mkdir(parents=True, exist_ok=True)
    (rundir / "findings.json").write_text(json.dumps({"pr": 7, "verdict": verdict, "findings": []}))


@pytest.fixture()
def prwt(tmp_path: Path) -> Path:
    r = tmp_path / "pr-7"
    r.mkdir()
    _git(r, "init", "-q", "-b", "feature")
    _git(r, "config", "user.email", "t@t")
    _git(r, "config", "user.name", "t")
    (r / "a.txt").write_text("1\n")
    _git(r, "add", "-A")
    _git(r, "commit", "-q", "-m", "review head")
    (r / "a.txt").write_text("2\n")
    _git(r, "commit", "-q", "-am", "fix critical")
    return r


def test_auto_merge_verdict_passes(tmp_path: Path):
    _findings(tmp_path / "run", "自動マージ可")
    res = _run(tmp_path / "run")
    assert res.returncode == 0, res.stderr
    assert json.loads(res.stdout)["applied"] is False


def test_stub_verdict_is_exit_32(tmp_path: Path):
    _findings(tmp_path / "run", "未測定（スタブ）")
    assert _run(tmp_path / "run").returncode == 32


def test_unreadable_findings_is_exit_33(tmp_path: Path):
    assert _run(tmp_path / "run").returncode == 33


def test_reject_requires_pushed_fix(tmp_path: Path, prwt: Path):
    _findings(tmp_path / "run", "Critical差し戻し")
    assert _run(tmp_path / "run").returncode == 30
    fix = _git(prwt, "rev-parse", "HEAD").strip()
    review_head = _git(prwt, "rev-parse", "HEAD~1").strip()
    # 修正が head の祖先なら通る / Passes when the fix is an ancestor of the PR head
    ok = _run(tmp_path / "run", "--prwt", str(prwt), "--fixed-commit", fix, "--head-oid", fix)
    assert ok.returncode == 0, ok.stderr
    # PR head がレビュー時点のまま（修正が未push）なら拒否 / Refused when the PR head still lacks the fix
    assert _run(tmp_path / "run", "--prwt", str(prwt), "--fixed-commit", fix, "--head-oid", review_head).returncode == 30


def test_ruling_requires_completed_adjudications(tmp_path: Path):
    _findings(tmp_path / "run", "新形につき裁定行き")
    assert _run(tmp_path / "run").returncode == 31
    (tmp_path / "run" / "adjudications.json").write_text(json.dumps({"completed": False, "items": []}))
    assert _run(tmp_path / "run").returncode == 31
    (tmp_path / "run" / "adjudications.json").write_text(json.dumps({"completed": True, "items": []}))
    assert _run(tmp_path / "run").returncode == 0

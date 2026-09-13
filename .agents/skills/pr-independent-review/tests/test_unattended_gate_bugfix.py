# bugfix ジョブの関所を、偽 transcript と一時 run ディレクトリで検証する
# Verify the bugfix gate with a fake transcript and a temp run directory
import json
import os
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "unattended-gate.py"
RUN_ID = "20260911_120000_aaaa1111"


def _run_gate(rundir_base: Path, transcript: Path, session: str, tmpdir: Path) -> subprocess.CompletedProcess:
    payload = json.dumps({"session_id": session, "transcript_path": str(transcript)})
    env = dict(os.environ, BUG_REPORT_RUNDIR_BASE=str(rundir_base), TMPDIR=str(tmpdir))
    return subprocess.run(
        [sys.executable, str(SCRIPT), "stop", "bugfix"],
        input=payload, text=True, capture_output=True, env=env,
    )


@pytest.fixture()
def transcript(tmp_path: Path) -> Path:
    path = tmp_path / "transcript.jsonl"
    launch = {"type": "user", "message": {"content": f"【無人起動】/bug-report-auto-fix {RUN_ID}"}}
    path.write_text(json.dumps(launch, ensure_ascii=False) + "\n", encoding="utf-8")
    (tmp_path / "runs" / RUN_ID).mkdir(parents=True)
    return path


def test_stop_is_blocked_without_fix_result(transcript: Path, tmp_path: Path):
    res = _run_gate(tmp_path / "runs", transcript, "s1", tmp_path / "state1")
    assert '"block"' in res.stdout, res.stdout


def test_stop_passes_once_fix_result_exists(transcript: Path, tmp_path: Path):
    (tmp_path / "runs" / RUN_ID / "fix-result.json").write_text('{"status":"fixed"}', encoding="utf-8")
    res = _run_gate(tmp_path / "runs", transcript, "s2", tmp_path / "state2")
    assert res.stdout.strip() == "", res.stdout

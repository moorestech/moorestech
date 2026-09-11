#!/usr/bin/env python3
"""bugfix ジョブの関所を、偽 transcript と一時 run ディレクトリで検証する。
Verifies the bugfix gate with a fake transcript and a temp run directory."""
import json
import os
import subprocess
import sys
import tempfile

GATE = os.path.join(os.path.dirname(__file__), "..", "..", ".agents", "skills", "pr-independent-review", "scripts", "unattended-gate.py")


def run_gate(rundir_base, transcript, session):
    payload = json.dumps({"session_id": session, "transcript_path": transcript})
    env = dict(os.environ, BUG_REPORT_RUNDIR_BASE=rundir_base, TMPDIR=tempfile.mkdtemp())
    return subprocess.run([sys.executable, GATE, "stop", "bugfix"], input=payload, text=True, capture_output=True, env=env)


def main():
    base = tempfile.mkdtemp()
    run = os.path.join(base, "20260911_120000_aaaa1111")
    os.makedirs(run)
    transcript = os.path.join(base, "t.jsonl")
    with open(transcript, "w", encoding="utf-8") as f:
        f.write(json.dumps({"type": "user", "message": {"content": "【無人起動】/bug-report-auto-fix 20260911_120000_aaaa1111"}}) + "\n")

    blocked = run_gate(base, transcript, "s1")
    assert '"block"' in blocked.stdout, f"result 無しなのに block されない: {blocked.stdout!r}"

    with open(os.path.join(run, "fix-result.json"), "w", encoding="utf-8") as f:
        f.write('{"status":"fixed"}')
    passed = run_gate(base, transcript, "s2")
    assert passed.stdout.strip() == "", f"result 有りなのに block された: {passed.stdout!r}"
    print("OK")


if __name__ == "__main__":
    main()

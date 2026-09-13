# PreToolUse(Bash) hook checkout_guard.py が `gh pr checkout` を PR専用worktree の外でだけ拒否することを検証する
# Verify the PreToolUse(Bash) hook checkout_guard.py refuses `gh pr checkout` only outside the PR-dedicated worktree
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "checkout_guard.py"


def _run(command: str) -> subprocess.CompletedProcess:
    payload = {"tool_name": "Bash", "tool_input": {"command": command}}
    return subprocess.run([sys.executable, str(SCRIPT)], input=json.dumps(payload), capture_output=True, text=True)


@pytest.mark.parametrize("command", [
    "gh pr checkout 1234",
    "cd /Users/x/moorestech && gh pr checkout 1234",
    "cd /Users/x/moorestech-worktrees/skills-canon-abcd1234 && gh pr checkout 1234",
    "gh pr checkout 1234 && cd /Users/x/moorestech-worktrees/pr-1234",
])
def test_denies_outside_prwt(command: str):
    res = _run(command)
    assert res.returncode == 2
    assert "PR専用worktree" in res.stderr


@pytest.mark.parametrize("command", [
    "cd /Users/x/moorestech-worktrees/pr-1234 && gh pr checkout 1234",
    "cd '/Users/x/moorestech-worktrees/pr-1234' && gh pr checkout 1234",
    "cd /Users/x/moorestech-worktrees/pr-1234/ && gh pr checkout 1234",
    "git -C /Users/x/moorestech fetch origin && git -C /Users/x/moorestech worktree add /tmp/pr-1 origin/feat",
    "gh pr view 1234 --json headRefOid",
])
def test_allows_inside_prwt_and_unrelated(command: str):
    assert _run(command).returncode == 0


def test_unparsable_input_fails_open():
    res = subprocess.run([sys.executable, str(SCRIPT)], input="not json", capture_output=True, text=True)
    assert res.returncode == 0

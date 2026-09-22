import subprocess
from pathlib import Path
from unittest import mock


REAL_POPEN = subprocess.Popen


def _guarded_popen(args, *positional, **options):
    command = args[0] if isinstance(args, (list, tuple)) else args
    if Path(str(command)).name == "claude":
        raise AssertionError("paid Claude CLI is forbidden in unit tests")
    return REAL_POPEN(args, *positional, **options)


def reject_paid_cli():
    return mock.patch("process_owner.subprocess.Popen", new=_guarded_popen)

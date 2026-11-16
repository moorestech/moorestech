#!/usr/bin/env python3
"""Run the UITK Web follow gate (G1): ledger coverage, switch records, and switched bundle paths."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from uitk_web_follow.cli import main  # noqa: E402

sys.exit(main(sys.argv[1:]))

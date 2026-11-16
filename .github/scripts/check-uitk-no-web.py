#!/usr/bin/env python3
"""Run the UITK no-Web gate (G4): no WebUi/Cef/webui tokens outside the allowlist."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from uitk_no_web.cli import main  # noqa: E402

sys.exit(main(sys.argv[1:]))

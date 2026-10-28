#!/usr/bin/env python3
"""Run verification in the foreground and preserve argv, exit status and raw output."""
import json
import subprocess
import sys
from pathlib import Path

name, cwd, *argv = sys.argv[1:]
result = subprocess.run(argv, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
path = Path(__file__).parent / (name + ".txt")
path.write_text(json.dumps({"cwd": cwd, "argv": argv, "exit": result.returncode}, ensure_ascii=False) + "\n" + result.stdout)
print(result.stdout)
print(f"exit={result.returncode}, evidence={path}")
sys.exit(result.returncode)

#!/usr/bin/env python3
# =====================================================================
# ⚠ scripts変更後: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
# =====================================================================
"""Atomic persistence helpers for requirement-review runtime state."""

import json
import os
import tempfile


def atomic_text(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            stream.write(text)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    except BaseException:
        try:
            os.unlink(temporary)
        except FileNotFoundError:
            pass
        raise


def atomic_json(path, value):
    atomic_text(path, json.dumps(value, ensure_ascii=False, indent=2))

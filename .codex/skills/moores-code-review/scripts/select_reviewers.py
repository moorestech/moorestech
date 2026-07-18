#!/usr/bin/env python3
"""Select reviewer subagents to fire based on a unified diff.

Reads a unified diff from stdin (or from the path given as argv[1]) and prints
`<absolute path>\t<model>` lines for reviewer markdown files whose yaml header
matches either:
  - one of the changed file extensions, OR
  - one of the keywords appearing in the added lines of the diff.

The model column comes from model_map.json next to this script (default opus).
Reviewers listed under model_map.json "replaced_by_script" are never printed:
deterministic_checks.py + a verifier agent supersede them.

Reviewer yaml header format (top of `reviewers/*.md`):

    ---
    extensions:
      - .cs
    keywords:
      - "#region Internal"
      - "UniTask"
    ---
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

REVIEWERS_DIR = Path(__file__).resolve().parent.parent / "reviewers"
MODEL_MAP_PATH = Path(__file__).resolve().parent / "model_map.json"


def load_model_map() -> tuple[str, set[str], set[str]]:
    if not MODEL_MAP_PATH.is_file():
        return "opus", set(), set()
    data = json.loads(MODEL_MAP_PATH.read_text(encoding="utf-8"))
    # disabled: 実績棚卸しで採用実績ゼロ/完全冗長と判定された reviewer(発火しない)
    # disabled: reviewers judged zero-yield/redundant in the run-history audit (never fired)
    excluded = set(data.get("replaced_by_script", [])) | set(data.get("disabled", []))
    return (
        data.get("default", "opus"),
        set(data.get("sonnet", [])),
        excluded,
    )


def parse_yaml_header(text: str) -> dict[str, list[str]]:
    if not text.startswith("---"):
        return {}
    end = text.find("\n---", 4)
    if end == -1:
        return {}
    header = text[4:end]
    result: dict[str, list[str]] = {}
    current_key: str | None = None
    for raw in header.splitlines():
        line = raw.rstrip()
        if not line.strip():
            continue
        if line.lstrip().startswith("- "):
            if current_key is None:
                continue
            value = line.lstrip()[2:].strip().strip('"').strip("'")
            result.setdefault(current_key, []).append(value)
        elif ":" in line:
            key, _, inline = line.partition(":")
            current_key = key.strip()
            inline = inline.strip()
            if inline in ("[]", "{}"):
                # inline empty list/map ("keywords: []") — not a single literal value
                result[current_key] = []
                current_key = None
            elif inline:
                result[current_key] = [inline.strip('"').strip("'")]
                current_key = None
            else:
                result[current_key] = []
    return result


def extract_changed_files_and_added(diff: str) -> tuple[list[str], str]:
    files: list[str] = []
    added: list[str] = []
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            files.append(line[6:])
        elif line.startswith("+++ "):
            path = line[4:].strip()
            if path != "/dev/null":
                files.append(path)
        elif line.startswith("+") and not line.startswith("+++"):
            added.append(line[1:])
    return files, "\n".join(added)


def matches(header: dict[str, list[str]], files: list[str], added: str) -> bool:
    # always:true short-circuits. Otherwise the extension group and the keyword
    # group are AND-combined (a reviewer fires only when BOTH its language gate
    # and its construct gate are satisfied); within each group the options are
    # OR-combined. An empty group is no constraint (treated as satisfied), so a
    # reviewer with neither extensions nor keywords nor always fires on every diff.
    always = header.get("always", [])
    if always and always[0].strip().lower() == "true":
        return True
    exts = [e for e in header.get("extensions", []) if e]
    kws = [k for k in header.get("keywords", []) if k]
    ext_ok = (not exts) or any(f.endswith(ext) for ext in exts for f in files)
    kw_ok = (not kws) or any(kw in added or any(kw in f for f in files) for kw in kws)
    return ext_ok and kw_ok


def main(argv: list[str]) -> int:
    if len(argv) > 1:
        diff = Path(argv[1]).read_text(encoding="utf-8", errors="replace")
    else:
        diff = sys.stdin.read()
    if not diff.strip():
        return 0
    files, added = extract_changed_files_and_added(diff)
    default_model, sonnet_set, replaced_set = load_model_map()
    for md in sorted(REVIEWERS_DIR.glob("*.md")):
        if md.stem in replaced_set:
            continue
        header = parse_yaml_header(md.read_text(encoding="utf-8"))
        if matches(header, files, added):
            model = "sonnet" if md.stem in sonnet_set else default_model
            print(f"{md}\t{model}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

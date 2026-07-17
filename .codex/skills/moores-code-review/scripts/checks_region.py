"""Candidate extraction for `#region Internal` placement rules (deterministic part).

Scans the post-change working tree of each changed .cs file with brace-depth
tracking: (a) `#region Internal` sitting directly inside a type body (§1),
(b) executable code after the `#endregion` of a method-level Internal region
(§2). Semantic parts of the rule (§3-§5) stay with the LLM reviewer; these
candidates are corroborating deterministic input for the integration step.
"""
from __future__ import annotations

import re
from pathlib import Path

from cs_lex import strip_source
from patch_util import FileDiff

TYPE_RE = re.compile(r"\b(?:class|struct|interface|record|enum)\b")
NAMESPACE_RE = re.compile(r"\bnamespace\b")
REGION_INTERNAL_RE = re.compile(r"^\s*#region\s+Internal\b")
ENDREGION_RE = re.compile(r"^\s*#endregion\b")


def run(files: list[FileDiff], repo_root: Path) -> list[dict]:
    candidates: list[dict] = []
    for f in files:
        if not f.path.endswith(".cs"):
            continue
        p = repo_root / f.path
        if not p.is_file():
            continue
        candidates += _scan_file(f.path, p.read_text(encoding="utf-8", errors="replace"))
    return candidates


def _scan_file(rel_path: str, text: str) -> list[dict]:
    lines = strip_source(text).splitlines()
    findings: list[dict] = []
    stack: list[str] = []  # 'namespace' | 'type' | 'block'
    decl_buf = ""
    open_region: dict | None = None  # method-level Internal region being tracked
    for lineno, line in enumerate(lines, start=1):
        if REGION_INTERNAL_RE.match(line):
            if stack and stack[-1] == "type":
                findings.append({"file": rel_path, "line": lineno, "kind": "class-level-region",
                                 "evidence": "#region Internal がクラス直下で private メソッドを囲っている疑い"})
            else:
                open_region = {"depth": len(stack), "closed_at": None}
            continue
        if ENDREGION_RE.match(line):
            if open_region is not None and open_region["depth"] == len(stack):
                open_region["closed_at"] = lineno
            continue
        for ch in line:
            if ch == "{":
                if NAMESPACE_RE.search(decl_buf):
                    stack.append("namespace")
                elif TYPE_RE.search(decl_buf):
                    stack.append("type")
                else:
                    stack.append("block")
                decl_buf = ""
            elif ch == "}":
                if stack:
                    popped_to = len(stack) - 1
                    stack.pop()
                    if open_region is not None and popped_to < open_region["depth"]:
                        open_region = None
                decl_buf = ""
            elif ch == ";":
                decl_buf = ""
            else:
                decl_buf += ch
        # #endregion 後に実行文が続いたら §2 違反 / executable code after #endregion is §2
        if open_region is not None and open_region["closed_at"] is not None:
            body = line.strip()
            if body and body not in ("{", "}") and not body.startswith("#"):
                findings.append({"file": rel_path, "line": lineno, "kind": "code-after-endregion",
                                 "evidence": body[:80]})
                open_region = None
    return findings

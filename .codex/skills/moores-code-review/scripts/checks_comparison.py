"""Candidate extraction for the `>` / `>=` direction rule (C#).

Recall-oriented lexical pass: strings/comments are blanked, then `=>` `->`
`>>` `>>=` are masked. What remains: any `>=` is a candidate; a bare `>` is a
candidate only with whitespace on both sides (project style writes generics
without spaces, comparisons with spaces). A sonnet verifier makes the final
call, so borderline generics leaking through here is acceptable.
"""
from __future__ import annotations

import re

from cs_lex import strip_line
from patch_util import FileDiff

MASK_RE = re.compile(r"=>|->|>>=|>>")
GE_RE = re.compile(r">=")
GT_RE = re.compile(r"(?<=\s)>(?=\s)")


def run(files: list[FileDiff]) -> list[dict]:
    candidates: list[dict] = []
    for f in files:
        if not f.path.endswith(".cs"):
            continue
        for lineno, text in f.added():
            code = MASK_RE.sub(lambda m: " " * len(m.group()), strip_line(text))
            ops = [m.start() for m in GE_RE.finditer(code)]
            ops += [m.start() for m in GT_RE.finditer(code) if not _inside_ge(code, m.start())]
            if ops:
                candidates.append({
                    "file": f.path, "line": lineno, "code": text.strip(),
                    "ops": sorted(code[i : i + 2].strip() for i in ops),
                })
    return candidates


def _inside_ge(code: str, i: int) -> bool:
    return code[i : i + 2] == ">=" or code[i - 1 : i + 1] == ">="

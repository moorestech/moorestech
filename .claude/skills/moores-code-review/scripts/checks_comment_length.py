"""Candidate extraction for the Japanese comment length convention.

The script does what LLMs are bad at (exact char counts) and leaves what
scripts are bad at (根拠コメント判定・短縮案) to a sonnet verifier. Only
CJK-containing added comments are measured, so the English half of a 日本語→
English pair is exempt automatically. Limits: statement/variable 20 (hard 24),
method 30 (hard 40), class 30.
"""
from __future__ import annotations

import re

from cs_lex import line_comment_body, strip_line
from patch_util import FileDiff

CJK_RE = re.compile(r"[぀-ヿ㐀-鿿！-｠]")
SKIP_RE = re.compile(
    r"@param|@returns?|@remarks|@ts-|eslint-disable|<summary>|</summary>|copyright|license", re.I
)
TYPE_DECL_RE = re.compile(r"\b(?:class|interface|struct|record|enum)\b")
METHOD_DECL_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal|static|override|async|export|function)\b.*\("
)
# ローカル関数など修飾子無しの宣言 `Vector3 Resolve(...)` もメソッド扱い / modifier-less local functions count as methods
LOCAL_FUNC_RE = re.compile(
    r"^\s*(?!return\b|throw\b|await\b|yield\b|new\b|if\b|while\b|for\b|foreach\b|switch\b|using\b|lock\b|else\b|var\b|const\b|case\b)"
    r"(?:[\w<>\[\],.?]+\s+)+\w+\s*\("
)
LIMITS = {"statement": (20, 24), "method": (30, 40), "class": (30, 40)}
SOURCE_EXTS = (".cs", ".ts", ".tsx")


def run(files: list[FileDiff]) -> list[dict]:
    candidates: list[dict] = []
    for f in files:
        if not f.path.endswith(SOURCE_EXTS):
            continue
        seq = [(m, no, t) for m, no, t in f.lines if m in ("+", " ")]
        for i, (marker, lineno, text) in enumerate(seq):
            body = _standalone_comment_body(text)
            if marker != "+" or body is None:
                continue
            if not CJK_RE.search(body) or SKIP_RE.search(body):
                continue
            candidates.append(_measure(f.path, lineno, body, _classify(seq, i)))
        candidates += _trailing_comments(f)
    return [c for c in candidates if c is not None]


def _measure(path: str, lineno: int | None, body: str, kind: str) -> dict | None:
    limit, hard = LIMITS[kind]
    count = _count(body)
    if count <= limit:
        return None
    return {"file": path, "line": lineno, "comment": body, "count": count,
            "kind": kind, "limit": limit, "hard_violation": hard < count}


def _standalone_comment_body(text: str) -> str | None:
    s = text.strip()
    if s.startswith(("//", "/*", "*")):
        return s.lstrip("/*").rstrip("*/").strip()
    return None


def _trailing_comments(f: FileDiff) -> list[dict | None]:
    out: list[dict | None] = []
    for lineno, text in f.added():
        if _standalone_comment_body(text) is not None:
            continue
        body = line_comment_body(text)
        if body is None or not CJK_RE.search(body) or SKIP_RE.search(body):
            continue
        out.append(_measure(f.path, lineno, body, "statement"))
    return out


def _classify(seq: list[tuple[str, int | None, str]], i: int) -> str:
    # 直後の非コメント行でコメント種別を決める / classify by the next non-comment line
    for _, _, text in seq[i + 1 :]:
        if _standalone_comment_body(text) is not None or not text.strip():
            continue
        code = strip_line(text)
        if TYPE_DECL_RE.search(code):
            return "class"
        if METHOD_DECL_RE.search(code) or LOCAL_FUNC_RE.match(code):
            return "method"
        return "statement"
    return "statement"


def _count(body: str) -> int:
    return len(re.sub(r"^-\s*", "", body).strip())

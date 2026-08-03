# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""Lightweight C#/TS lexing helpers: blank out strings and comments.

Blanked characters are replaced with spaces so column positions survive.
Handles "..." (with \\ escapes), @"..." ("" escapes), '...', `...`, // and /* */.
Good enough for convention checks; not a full parser (interpolation holes in
$"..." are treated as part of the string, which is the safe direction here).
"""
from __future__ import annotations


def strip_source(text: str) -> str:
    out: list[str] = []
    i, n = 0, len(text)
    mode = "code"  # code | str | vstr | char | tmpl | line | block
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if mode == "code":
            if c == '"':
                prev1 = text[i - 1] if i >= 1 else ""
                prev2 = text[i - 2] if i >= 2 else ""
                verbatim = prev1 == "@" or (prev1 == "$" and prev2 == "@")
                mode = "vstr" if verbatim else "str"
                out.append(" ")
            elif c == "'":
                mode = "char"
                out.append(" ")
            elif c == "`":
                mode = "tmpl"
                out.append(" ")
            elif c == "/" and nxt == "/":
                mode = "line"
                out.append(" ")
            elif c == "/" and nxt == "*":
                mode = "block"
                out.append(" ")
            else:
                out.append(c)
            i += 1
            continue
        if mode in ("str", "char", "tmpl"):
            closer = {"str": '"', "char": "'", "tmpl": "`"}[mode]
            if c == "\\" and i + 1 < n:
                out.append("  ")
                i += 2
                continue
            if c == closer:
                mode = "code"
            out.append(c if c == "\n" else " ")
            i += 1
            continue
        if mode == "vstr":
            if c == '"' and nxt == '"':
                out.append("  ")
                i += 2
                continue
            if c == '"':
                mode = "code"
            out.append(c if c == "\n" else " ")
            i += 1
            continue
        if mode == "line":
            if c == "\n":
                mode = "code"
            out.append(c if c == "\n" else " ")
            i += 1
            continue
        # block comment
        if c == "*" and nxt == "/":
            mode = "code"
            out.append("  ")
            i += 2
            continue
        out.append(c if c == "\n" else " ")
        i += 1
    return "".join(out)


def strip_line(line: str) -> str:
    """Blank strings/comments within a single line (block comments approximated)."""
    return strip_source(line)


def line_comment_body(line: str) -> str | None:
    """Return text after '//' (or '///'), or None when the line has no comment.

    Strings are blanked first so '//' inside a literal never counts.
    """
    blanked = strip_source(line)
    # strip_source blanks the comment too, so locate '//' in the original via
    # the first position where blanked shows spaces but original shows '//'.
    idx = -1
    for j in range(len(line) - 1):
        if line[j] == "/" and line[j + 1] == "/" and blanked[j] == " ":
            idx = j
            break
    if idx == -1:
        return None
    return line[idx:].lstrip("/").strip()

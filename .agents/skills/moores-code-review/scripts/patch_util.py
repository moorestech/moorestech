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
"""Unified diff parsing shared by deterministic checks.

FileDiff.lines holds (marker, new_lineno, text) tuples where marker is one of
' ' (context), '+' (added), '-' (removed). Removed lines have new_lineno=None.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field

HUNK_RE = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@")


@dataclass
class FileDiff:
    path: str
    is_new: bool = False
    lines: list[tuple[str, int | None, str]] = field(default_factory=list)

    def added(self) -> list[tuple[int, str]]:
        return [(no, text) for marker, no, text in self.lines if marker == "+" and no is not None]


def parse_patch(text: str) -> list[FileDiff]:
    files: list[FileDiff] = []
    current: FileDiff | None = None
    new_lineno = 0
    pending_new = False
    for raw in text.splitlines():
        if raw.startswith("diff --git"):
            current = None
            pending_new = False
        elif raw.startswith("new file mode"):
            pending_new = True
        elif raw.startswith("+++ "):
            path = raw[4:].strip()
            if path.startswith("b/"):
                path = path[2:]
            if path == "/dev/null":
                current = None
                continue
            current = FileDiff(path=path, is_new=pending_new)
            files.append(current)
            pending_new = False
        elif raw.startswith("@@"):
            m = HUNK_RE.match(raw)
            if m and current is not None:
                new_lineno = int(m.group(1))
        elif current is not None:
            if raw.startswith("+"):
                current.lines.append(("+", new_lineno, raw[1:]))
                new_lineno += 1
            elif raw.startswith("-"):
                current.lines.append(("-", None, raw[1:]))
            elif raw.startswith(" "):
                current.lines.append((" ", new_lineno, raw[1:]))
                new_lineno += 1
    return files


def files_with_ext(files: list[FileDiff], exts: tuple[str, ...]) -> list[FileDiff]:
    return [f for f in files if f.path.endswith(exts)]

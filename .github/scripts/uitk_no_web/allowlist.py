"""Allowlist of the UITK no-Web gate (G4): path entries and line entries, each with a reason."""

import re
from dataclasses import dataclass
from fnmatch import fnmatchcase


@dataclass(frozen=True)
class PathEntry:
    glob: str
    reason: str
    line_no: int


@dataclass(frozen=True)
class LineEntry:
    glob: str
    pattern: re.Pattern
    reason: str
    line_no: int


@dataclass(frozen=True)
class Allowlist:
    paths: tuple
    lines: tuple


def parse_allowlist(text: str) -> tuple:
    """Return (Allowlist, errors). Each entry is `path <glob>  # 理由` or `line <glob> <regex>  # 理由`."""
    paths, lines, errors = [], [], []
    for line_no, raw in enumerate(text.splitlines(), 1):
        stripped = raw.strip()
        if not stripped or stripped.startswith("#"):
            continue
        # 理由の無い行は誤り。黙って許すと許可の根拠が残らない
        # An entry without a reason is an error; silently accepting it leaves no rationale
        body, separator, reason = stripped.partition(" # ")
        if not separator or not reason.strip():
            errors.append(f"{line_no}: 理由（' # 理由'）が無い")
            continue
        kind, _, rest = body.strip().partition(" ")
        rest = rest.strip()
        if kind == "path" and rest and " " not in rest:
            paths.append(PathEntry(rest, reason.strip(), line_no))
        elif kind == "line":
            glob, _, regex = rest.partition(" ")
            if not glob or not regex.strip():
                errors.append(f"{line_no}: line は '<glob> <regex>' の 2 つが要る")
                continue
            # 許可リストは外から書かれる入力。正規表現の誤りは例外で落とさず行番号つきの誤りにして 2 で止める
            # The allowlist is hand-written input; a bad regex becomes a line-numbered error (exit 2) instead of a traceback
            try:
                pattern = re.compile(regex.strip())
            except re.error as error:
                errors.append(f"{line_no}: 正規表現が読めない: {error}")
                continue
            lines.append(LineEntry(glob, pattern, reason.strip(), line_no))
        else:
            errors.append(f"{line_no}: 種類は 'path <glob>' か 'line <glob> <regex>' だけ")
    return Allowlist(tuple(paths), tuple(lines)), errors


def path_allowed(entry, path: str) -> bool:
    """Glob match shared by both entry kinds (both carry `glob`)."""
    return fnmatchcase(path, entry.glob)


def line_allowed(entry: LineEntry, path: str, text: str) -> bool:
    return path_allowed(entry, path) and entry.pattern.search(text) is not None

"""Layer-1 deterministic AGENTS.md rules: findings are confirmed, no LLM needed.

Detection is exact; fixes are NOT mechanical (partial removal, file splitting
are design work), so callers route these to the report / AskUserQuestion path.
"""
from __future__ import annotations

from pathlib import Path

from cs_lex import strip_line
from patch_util import FileDiff

import re

PARTIAL_RE = re.compile(r"\bpartial\s+(?:class|struct|interface|record)\b")
CATCH_RE = re.compile(r"\bcatch\b\s*(?:$|[({])")
DEFAULT_ARG_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal)\b[^=;{]*\([^)]*=[^=]"
)
SERIALIZE_FIELD_NAME_RE = re.compile(r"[\w<>.,\[\]?\s]+?\s(_\w+|[A-Z]\w*)\s*[;=]")
SOURCE_EXTS = (".cs", ".ts", ".tsx")
MAX_FILE_LINES = 200
MAX_DIR_FILES = 10


def run(files: list[FileDiff], repo_root: Path) -> list[dict]:
    findings: list[dict] = []
    findings += _added_line_rules(files)
    findings += _file_length_rule(files, repo_root)
    findings += _dir_count_rule(files, repo_root)
    return findings


def _added_line_rules(files: list[FileDiff]) -> list[dict]:
    findings: list[dict] = []
    for f in files:
        if not f.path.endswith(".cs"):
            continue
        added = f.added()
        for idx, (lineno, text) in enumerate(added):
            code = strip_line(text)
            if PARTIAL_RE.search(code):
                findings.append(_finding("partial-forbidden", f.path, lineno, text,
                                         "partial は如何なる条件でも禁止 (AGENTS.md)"))
            if CATCH_RE.search(code):
                findings.append(_finding("try-catch-forbidden", f.path, lineno, text,
                                         "try-catch は基本禁止。条件分岐/null チェックで代替 (AGENTS.md)"))
            if DEFAULT_ARG_RE.search(code) and "=>" not in code.split("(")[0]:
                findings.append(_finding("default-argument-forbidden", f.path, lineno, text,
                                         "デフォルト引数は禁止。呼び出し側を変更する (AGENTS.md)"))
            if "[SerializeField]" in text:
                decl = code.split("[SerializeField]", 1)[-1]
                if not decl.strip() and idx + 1 < len(added) and added[idx + 1][0] == lineno + 1:
                    decl = strip_line(added[idx + 1][1])
                m = SERIALIZE_FIELD_NAME_RE.search(decl)
                if m:
                    findings.append(_finding("serializefield-naming", f.path, lineno, text.strip(),
                                             f"[SerializeField] は _ 無し小文字キャメル。`{m.group(1)}` が違反"))
    return findings


def _file_length_rule(files: list[FileDiff], repo_root: Path) -> list[dict]:
    findings = []
    for f in files:
        if not f.path.endswith(SOURCE_EXTS):
            continue
        p = repo_root / f.path
        if not p.is_file():
            continue
        count = sum(1 for _ in p.open(encoding="utf-8", errors="replace"))
        if MAX_FILE_LINES < count:
            findings.append(_finding("file-too-long", f.path, count, f"{count} 行",
                                     f"1 ファイル {MAX_FILE_LINES} 行以下。分割する (partial 禁止)"))
    return findings


def _dir_count_rule(files: list[FileDiff], repo_root: Path) -> list[dict]:
    findings = []
    seen: set[str] = set()
    for f in files:
        if not (f.is_new and f.path.endswith(SOURCE_EXTS)):
            continue
        rel_dir = str(Path(f.path).parent)
        if rel_dir in seen:
            continue
        seen.add(rel_dir)
        d = repo_root / rel_dir
        if not d.is_dir():
            continue
        count = sum(1 for c in d.iterdir() if c.is_file() and c.name.endswith(SOURCE_EXTS))
        if MAX_DIR_FILES < count:
            findings.append(_finding("dir-file-limit", rel_dir, count, f"{count} ファイル",
                                     f"1 ディレクトリ {MAX_DIR_FILES} ファイルまで。サブディレクトリへ分割"))
    return findings


def _finding(rule: str, path: str, line: int, evidence: str, message: str) -> dict:
    return {"rule": rule, "file": path, "line": line,
            "evidence": evidence.strip(), "message": message, "fix_class": "judgement"}

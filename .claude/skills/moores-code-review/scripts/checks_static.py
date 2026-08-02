"""Layer-1 deterministic AGENTS.md rules: findings are confirmed, no LLM needed.

Detection is exact; fixes are NOT mechanical (partial removal, file splitting
are design work), so callers route these to the report / AskUserQuestion path.
"""
from __future__ import annotations

from pathlib import Path

from cs_lex import line_comment_body, strip_line
from patch_util import FileDiff

import re

PARTIAL_RE = re.compile(r"\bpartial\s+(?:class|struct|interface|record)\b")
FUNC_RE = re.compile(r"\bFunc\s*<")
CATCH_RE = re.compile(r"\bcatch\b\s*(?:$|[({])")
TRY_RE = re.compile(r"\btry\b\s*(?:$|\{)")
DEFAULT_ARG_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal)\b[^=;{]*\([^)]*=[^=]"
)
SERIALIZE_FIELD_NAME_RE = re.compile(r"[\w<>.,\[\]?\s]+?\s(_\w+|[A-Z]\w*)\s*[;=]")
SOURCE_EXTS = (".cs", ".ts", ".tsx")
MAX_FILE_LINES = 200
MAX_DIR_FILES = 10

# AGENTS.md が try-catch を許すのは外部境界3種だけ。根拠コメントの「主張内容」をこの表と照合する
# AGENTS.md permits try-catch only at these 3 external boundaries; the rationale comment's claim is matched here
BOUNDARY_ALLOWLIST = {
    "external-process": ("外部プロセス", "プロセス起動", "外部コマンド", "サブプロセス",
                         "Process.Start", "ProcessStartInfo", "subprocess", "external process"),
    "network-io": ("ネットワーク", "通信", "WebSocket", "websocket", "ソケット", "Socket",
                   "socket", "HTTP", "http", "TCP", "network"),
    "external-json-parse": ("パース", "parse", "Parse", "Deserialize", "デシリアライズ",
                            "JsonConvert", "JsonSerializer", "外部入力", "外部JSON"),
}
# 根拠コメントを探す遡り幅（try の直前に置かれた2行セットコメントまで届く距離）
# Look-back window for the rationale comment (reaches the 2-line comment set placed above `try`)
RATIONALE_WINDOW = 12


# テストコードは200行/10ファイル規約の適用外（ユーザー裁定 2026-07-28）
# Test code is exempt from the 200-line / 10-file rules (user ruling 2026-07-28)
def _is_test_path(path: str) -> bool:
    parts = Path(path).parts
    if any(p in ("e2e", "tests", "__tests__") or "Tests" in p for p in parts[:-1]):
        return True
    name = parts[-1]
    stem = re.sub(r"\.(cs|ts|tsx)$", "", name)
    return name.endswith((".test.ts", ".test.tsx", ".spec.ts")) or stem.endswith(("Test", "Tests"))


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
            if FUNC_RE.search(code):
                findings.append(_finding("func-forbidden", f.path, lineno, text,
                                         "Func<> は禁止。Action/delegate への置換に逃げず、interface導入・依存関係の整理・呼び出し方向の変更で解決 (AGENTS.md)"))
            if CATCH_RE.search(code):
                comment = _rationale_comment(f, lineno)
                claims = _classify_boundary(comment) if comment else []
                if not comment:
                    findings.append(_finding("try-catch-forbidden", f.path, lineno, text,
                                             "try-catch は基本禁止。条件分岐/null チェックで代替 (AGENTS.md)。境界である根拠コメントも無い"))
                elif not claims:
                    findings.append(_finding("try-catch-forbidden", f.path, lineno, text,
                                             f"try-catch は基本禁止 (AGENTS.md)。根拠コメントはあるが許可された境界3種"
                                             f"(外部プロセス起動/ネットワーク送受信/外部入力JSONのパース)のどれも主張していない: 「{comment}」"))
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


def _try_anchor(f: FileDiff, catch_lineno: int) -> int:
    # 根拠コメントは try の直前に置かれる。try 本体が長いと catch 起点の窓では届かないので try 行を探す
    # The rationale sits above `try`; a long try body puts it out of a catch-anchored window, so find the `try`
    anchor = catch_lineno
    for _, no, text in f.lines:
        if no is None or no > catch_lineno:
            continue
        if TRY_RE.search(strip_line(text)) and (anchor == catch_lineno or anchor < no):
            anchor = no
    return anchor


def _rationale_comment(f: FileDiff, catch_lineno: int) -> str | None:
    # try 行（無ければ catch 行）から遡って窓内の // コメントを全部拾う（日本語+英語の2行セット規約に合わせて連結）
    # Collect every // comment in the window above `try` (or `catch`), joined per the JP+EN 2-line convention
    anchor = _try_anchor(f, catch_lineno)
    bodies = [
        body
        for _, no, text in f.lines
        if no is not None and 0 <= anchor - no <= RATIONALE_WINDOW
        for body in (line_comment_body(text),)
        if body
    ]
    return " / ".join(bodies) if bodies else None


def _classify_boundary(comment: str) -> list[str]:
    return [name for name, kws in BOUNDARY_ALLOWLIST.items() if any(kw in comment for kw in kws)]


def try_catch_boundary(files: list[FileDiff]) -> list[dict]:
    """境界を主張する根拠コメント付き try-catch を候補として返す（免除ではなく裁定行き）。

    コメントが存在するだけでは免除しない。許可リスト3種のどれかを主張しているものだけを
    candidate へ降ろし、verifier が「主張どおりの境界か」を実コードで裁定する。
    主張が無い / コメント自体が無いものは confirmed のまま（_added_line_rules 側）。
    """
    candidates: list[dict] = []
    for f in files:
        if not f.path.endswith(".cs"):
            continue
        for lineno, text in f.added():
            if not CATCH_RE.search(strip_line(text)):
                continue
            comment = _rationale_comment(f, lineno)
            claims = _classify_boundary(comment) if comment else []
            if not claims:
                continue
            finding = _finding(
                "try-catch-boundary", f.path, lineno, text,
                f"try-catch の根拠コメントが境界 {'/'.join(claims)} を主張している。"
                f"主張どおりの外部境界か（AGENTS.md 例外3種のどれに当たるか）を実コードで裁定する: 「{comment}」")
            finding["comment"] = comment
            finding["boundary_claim"] = claims
            candidates.append(finding)
    return candidates


def _file_length_rule(files: list[FileDiff], repo_root: Path) -> list[dict]:
    findings = []
    for f in files:
        if not f.path.endswith(SOURCE_EXTS) or _is_test_path(f.path):
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
        if not (f.is_new and f.path.endswith(SOURCE_EXTS)) or _is_test_path(f.path):
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

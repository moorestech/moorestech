"""Find G4 tokens in tracked file names and tracked text contents."""

import re
import subprocess
from dataclasses import dataclass
from pathlib import Path

# 検出する語の正本。git grep の粗い拾いと Python の判定はどちらもここから作る
# Single source of the tokens; both the coarse git grep and the Python check derive from it
TOKENS = ("WebUi", "WebUI", "Web UI", "webui", "webUi", "WEBUI", "Webui", "Cef", "CEF", "cef-unity", "Kestrel", "moorestech_web")
# CEF は英数字に挟まれないときだけ語（素材の 16 進を拾わず、和文の隣は拾う。BSD の正規表現に \b が無いので Python で見る）
# CEF counts only when not wedged in ASCII word characters (skip hex in assets, keep next to Japanese; BSD regex lacks \b)
BOUNDED_TOKENS = frozenset({"CEF"})
GREP_PATTERN = "|".join(re.escape(token) for token in TOKENS)
TOKEN_PATTERN = re.compile("|".join(
    rf"(?<![A-Za-z0-9_]){re.escape(token)}(?![A-Za-z0-9_])" if token in BOUNDED_TOKENS else re.escape(token)
    for token in TOKENS))


class GitFailure(Exception):
    """git が失敗した・出力の形が崩れた。終了コードは cli.main だけが決める / git failed or its output was malformed; only cli.main decides the exit code."""


@dataclass(frozen=True)
class Hit:
    path: str
    line_no: int
    token: str
    text: str


def tracked_files(root: Path) -> list:
    result = _git(root, ["ls-files", "-z"], (0,))
    return [path for path in result.decode("utf-8", errors="replace").split("\0") if path]


def path_hits(files: list) -> list:
    hits = []
    for path in files:
        match = TOKEN_PATTERN.search(path)
        if match is not None:
            hits.append(Hit(path, 0, match.group(0), path))
    return hits


def content_hits(root: Path) -> list:
    # -I でバイナリを飛ばす。終了コード 1 は「当たり無し」
    # -I skips binaries; exit code 1 means "no match"
    output = _git(root, ["grep", "-I", "-n", "-z", "-E", GREP_PATTERN, "--", "."], (0, 1))
    hits = []
    # レコードは改行だけで区切る（splitlines は \x0c 等でも割り、行の後半を落とすため）
    # Records end only at \n (splitlines also breaks at \x0c and friends and would lose the tail of a line)
    for record in output.decode("utf-8", errors="replace").split("\n"):
        if not record:
            continue
        parts = record.split("\0", 2)
        if len(parts) != 3 or not parts[1].isdigit():
            raise GitFailure(f"git grep の出力の形が崩れた: {record[:160]!r}")
        path, line_no, text = parts
        match = TOKEN_PATTERN.search(text)
        # 英数字に挟まれた CEF（素材の 16 進など）は語ではないので落とす
        # A CEF wedged between ASCII word characters (hex in assets etc.) is not the word, so it is dropped
        if match is None:
            continue
        hits.append(Hit(path, int(line_no), match.group(0), text.strip()))
    return hits


def _git(root: Path, args: list, ok_codes: tuple) -> bytes:
    completed = subprocess.run(["git", "-C", str(root), *args], capture_output=True)
    if completed.returncode not in ok_codes:
        raise GitFailure(f"git {' '.join(args[:2])} が失敗: {completed.stderr.decode('utf-8', errors='replace').strip()}")
    return completed.stdout

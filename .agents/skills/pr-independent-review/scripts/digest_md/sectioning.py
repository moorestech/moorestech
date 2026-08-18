# digest.mdをコードフェンス・見出しレベルで切り分ける
# Split digest.md by code fences and heading levels
from __future__ import annotations

from .errors import DigestError


def read_fence(lines: list, i: int) -> tuple:
    # フェンスの中身と、閉じフェンスの次の行番号を返す
    # Return the fenced body and the line index just after the closing fence
    body = []
    i += 1
    while i < len(lines) and not lines[i].startswith("```"):
        body.append(lines[i])
        i += 1
    if i >= len(lines):
        raise DigestError("閉じられていないコードフェンスがあります")
    return "\n".join(body), i + 1


def split_blocks(text: str) -> tuple:
    # 見出しレベル1/2でブロックへ割る。フェンス内の見出しは無視する
    # Split by level-1/2 headings, ignoring headings that live inside fences
    lines = text.splitlines()
    blocks, title = [], ""
    cur_level, cur_title, buf = "", "", []
    i, in_fence = 0, False
    while i < len(lines):
        line = lines[i]
        if line.startswith("```"):
            in_fence = not in_fence
        # 先頭の `# ` は文書タイトル、以降の `# `/`## ` は新ブロックの開始
        # The first `# ` is the document title; later `# `/`## ` start a new block
        if not in_fence and line.startswith("# ") and not title and not cur_level:
            title = line[2:].strip()
            i += 1
            continue
        if not in_fence and (line.startswith("# ") or line.startswith("## ")):
            if cur_level:
                blocks.append((cur_level, cur_title, "\n".join(buf).strip()))
            cur_level = "1" if line.startswith("# ") else "2"
            cur_title = line.lstrip("#").strip()
            buf = []
            i += 1
            continue
        buf.append(line)
        i += 1
    if cur_level:
        blocks.append((cur_level, cur_title, "\n".join(buf).strip()))
    return blocks, title

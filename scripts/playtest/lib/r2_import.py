#!/usr/bin/env python3
"""zip 取り込みで箱を R2 へ上げるための宣言づくりと、受け口の応答の読み取り（標準ライブラリのみ）。

使い方:
  r2_import.py declare <箱>   … 箱の全ファイルを {"files":[{path,bytes}]} の JSON で出す（受け口の import 本文）
  r2_import.py uploads <応答> … 署名付き URL を1行1件（path US url）で出す。長さ違いで上書きできない物（conflicts）があれば
                                 理由を stderr へ出して exit 4（R2 の正本と手元の箱が食い違うので取り込まない）

Builds the declaration for uploading a zip-imported box to R2 and reads the receiver's reply (stdlib only).
"declare" prints every file of the box as {"files":[{path,bytes}]}, the receiver's import body;
"uploads" prints one presigned URL per line (path US url) and exits 4 with reasons on stderr when conflicts exist,
because R2 (the source of truth) and the local box disagree, so the box must not be imported.
"""
from __future__ import annotations

import json
import os
import sys


def declare(box: str) -> int:
    files = []
    for directory, _dirs, names in os.walk(box):
        for name in names:
            full = os.path.join(directory, name)
            files.append({"path": os.path.relpath(full, box).replace(os.sep, "/"), "bytes": os.path.getsize(full)})
    print(json.dumps({"files": sorted(files, key=lambda entry: entry["path"])}, ensure_ascii=False))
    return 0


def uploads(reply_path: str) -> int:
    with open(reply_path, encoding="utf-8") as source:
        reply = json.load(source)
    conflicts = reply.get("conflicts") or []
    for conflict in conflicts:
        print(f"[r2-import] ERROR: R2 に長さの違う同名ファイルがある: {conflict!r}", file=sys.stderr)
    if conflicts:
        return 4
    for upload in reply.get("uploads") or []:
        path, url = upload.get("path"), upload.get("url")
        if not isinstance(path, str) or not isinstance(url, str) or "\x1f" in path or "\n" in path + url:
            print(f"[r2-import] ERROR: 受け口の応答に不正な upload がある: {upload!r}", file=sys.stderr)
            return 4
        print(f"{path}\x1f{url}")
    return 0


def main(argv: list[str]) -> int:
    if len(argv) == 2 and argv[0] == "declare":
        return declare(argv[1])
    if len(argv) == 2 and argv[0] == "uploads":
        return uploads(argv[1])
    print("usage: r2_import.py declare <box> | uploads <reply.json>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

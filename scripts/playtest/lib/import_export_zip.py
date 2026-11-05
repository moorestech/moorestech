#!/usr/bin/env python3
"""テスターが書き出した未送信報告 zip の展開と、箱ごとの整形（標準ライブラリのみ）。

使い方:
  import_export_zip.py unpack <zip> <dir>
    … エントリ名を検証してから展開し、箱を1行1件（kind US id US exportedAt）で出す。kind は report|progress
  import_export_zip.py stage <unpacked-box> <kind> <partial>
    … 印を除いて箱を <partial> へコピーし、印は <partial>/export-markers/ へ置き、受け口と同じ形の READY（files[] は退避した印も含む全ファイル）を書き、steamId を出す

Unpacks the zip a tester exported and shapes each box (stdlib only).
"unpack" validates entry names before extracting and prints one US-separated row per box;
"stage" copies a box minus marker files, parks markers in export-markers/, writes a receiver-shaped READY and prints the steamId.
"""
from __future__ import annotations

import json
import os
import shutil
import sys
import zipfile

MARKER_NAMES = ("READY", "UPLOADED", "SHIPPED", "UPLOAD_FAILED", "UPLOAD_ATTEMPTS",
                "UPLOAD_SKIPPED", "UPLOAD_DECLARED", "UPLOAD_LOCK")
KIND_FROM_EXPORT = {"reports": "report", "progress": "progress"}
IDENTITY_FILE = {"report": "manifest.json", "progress": "record.json"}


def fail(message: str) -> int:
    print(f"[import-export-zip] ERROR: {message}", file=sys.stderr)
    return 1


def is_unsafe_entry(name: str) -> bool:
    if not name or name.startswith(("/", "\\")) or ":" in name or "\\" in name:
        return True
    return any(part == ".." or any(c < " " or c == "\x7f" for c in part) for part in name.split("/"))


def unpack(zip_path: str, target: str) -> int:
    with zipfile.ZipFile(zip_path) as archive:
        for name in archive.namelist():
            if is_unsafe_entry(name):
                return fail(f"zip に不正なエントリ名がある（何も展開しない）: {name!r}")
        # 大文字小文字だけ違う同名エントリは APFS で上書きし合う（ready が READY を潰す等）ので展開前に拒否する
        # Entries differing only by case overwrite each other on APFS (ready clobbering READY), so reject before extracting
        folded = [name.casefold() for name in archive.namelist()]
        if len(set(folded)) != len(folded):
            return fail("zip に大文字小文字だけが違う同名エントリがある（何も展開しない）")
        archive.extractall(target)
    export_path = os.path.join(target, "export.json")
    if not os.path.isfile(export_path):
        return fail("zip に export.json が無い（書き出しの zip ではない）")
    with open(export_path, encoding="utf-8") as source:
        export = json.load(source)
    for entry in export.get("missing") or []:
        print(f"[import-export-zip] [WARN] 書き出し時に欠落した項目: {entry.get('item')}（{entry.get('reason')}）", file=sys.stderr)
    for box in export.get("boxes") or []:
        kind = KIND_FROM_EXPORT.get(box.get("kind"))
        box_id = box.get("id")
        if kind is None or not isinstance(box_id, str):
            print(f"[import-export-zip] ERROR: export.json の箱を読めない: {box!r}", file=sys.stderr)
            return 1
        row = [kind, box_id, str(export.get("exportedAt") or "")]
        if any(c < " " or c == "\x7f" for c in "".join(row)):
            return fail(f"export.json の箱に制御文字がある: {row!r}")
        print("\x1f".join(row))
    return 0


def stage(box_dir: str, kind: str, partial: str) -> int:
    identity_path = os.path.join(box_dir, IDENTITY_FILE[kind])
    steam_id = None
    manifest_text = None
    if os.path.isfile(identity_path):
        with open(identity_path, encoding="utf-8") as source:
            manifest_text = source.read()
        steam_id = json.loads(manifest_text).get("steamId")
    if not steam_id:
        return fail(f"steamId が無い（開発者の箱）: {identity_path}")
    files, markers = [], []
    for root, _dirs, names in os.walk(box_dir):
        for name in sorted(names):
            relative = os.path.relpath(os.path.join(root, name), box_dir).replace(os.sep, "/")
            (markers if relative in MARKER_NAMES else files).append(relative)
    for relative, base in [(f, partial) for f in files] + [(m, os.path.join(partial, "export-markers")) for m in markers]:
        destination = os.path.join(base, relative)
        os.makedirs(os.path.dirname(destination), exist_ok=True)
        shutil.copyfile(os.path.join(box_dir, relative), destination)
    # R2 へ上げる物は全部 READY の files[] に載せる（fetch-report.sh が READY から取るので、印の退避先も含める）
    # Everything uploaded to R2 goes into READY's files[] (fetch-report.sh reads READY, so parked markers are included)
    listed = sorted(files + [f"export-markers/{m}" for m in markers])
    ready = {"kind": kind, "id": os.path.basename(box_dir), "fileCount": len(listed), "files": listed,
             "skipped": [], "manifest": manifest_text if kind == "report" else None, "importedFrom": "export-zip"}
    with open(os.path.join(partial, "READY"), "w", encoding="utf-8") as target:
        json.dump(ready, target, ensure_ascii=False)
    print(steam_id)
    return 0


def main(argv: list[str]) -> int:
    # 外部入力（受け取った zip・JSON）の解析境界。壊れていれば理由を出して失敗にする
    # Parsing boundary for received input (zip, JSON): a broken file fails with its reason
    try:
        if len(argv) == 3 and argv[0] == "unpack":
            return unpack(argv[1], argv[2])
        if len(argv) == 4 and argv[0] == "stage" and argv[2] in IDENTITY_FILE:
            return stage(argv[1], argv[2], argv[3])
    except (zipfile.BadZipFile, json.JSONDecodeError, AttributeError, OSError) as error:
        return fail(f"入力を読めない: {type(error).__name__}: {error}")
    print("usage: import_export_zip.py unpack <zip> <dir> | stage <box> <report|progress> <partial>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

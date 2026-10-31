#!/usr/bin/env python3
"""logs 側の箱から生データ側の所在と状態を引く（標準ライブラリのみ）。読む側（ダッシュボード・find-reports・enqueue）が共有する。

状態: none（raw.json が無い＝箱が全部を持つ旧い箱か、生データの無い箱）／ok（全ファイルあり）／
expired（期限を過ぎて消えた）／missing（期限前なのに欠けている＝事故）／broken（raw.json を読めない）。
none と ok 以外は、呼び出し側が止まるか利用者に見える形で欠落を示す。

使い方: raw_reader.py state <box> … 状態を1行（state<TAB>expiresAt<TAB>理由）で出す。ok/none で 0、それ以外 1
        raw_reader.py materialize <box> <dest> … 生データを <dest> の同じ相対パスへ複写する。ok/none 以外は理由を出して 1

Resolves a logs-side box's raw-side location and state (stdlib only); shared by the dashboard, find-reports and enqueue.
States: none (no raw.json: an older box holding everything, or one with no raw data), ok (every file present),
expired (deleted after expiry), missing (absent before expiry: an incident) and broken (raw.json unreadable).
Anything but none/ok makes the caller stop or show the gap to the user.
"""
from __future__ import annotations

import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import raw_layout as layout  # noqa: E402

READABLE_STATES = ("none", "ok")


def raw_state(box: Path, root: Path) -> dict:
    ref_path = box / layout.REF_NAME
    if not ref_path.is_file():
        return {"state": "none", "expiresAt": "", "reason": "", "rawDir": None, "files": []}
    ref, reason = layout.read_ref(ref_path)
    if ref is None:
        return {"state": "broken", "expiresAt": "", "reason": reason, "rawDir": None, "files": []}
    raw_dir = root / layout.box_relative(box)
    files = [entry["path"] for entry in ref["files"]]
    absent = [name for name in files if not (raw_dir / name).is_file()]
    state = {"expiresAt": ref["expiresAt"], "rawDir": raw_dir, "files": files}
    if not absent:
        return dict(state, state="ok", reason="")
    if datetime.now(timezone.utc) >= layout.parse_utc(ref["expiresAt"]):
        return dict(state, state="expired", reason=f"生データは期限 {ref['expiresAt']} を過ぎて削除済み（{raw_dir}）")
    return dict(state, state="missing",
                reason=f"期限 {ref['expiresAt']} 前なのに生データが {len(absent)}/{len(files)} 件欠けている（{raw_dir}・例: {absent[0]}）")


def resolve_file(box: Path, relative: str, root: Path) -> Path | None:
    """箱内の相対パスを、logs 側→生データ側の順で実在するファイルへ解決する。無ければ None
    Resolves a box-relative path to an existing file, logs side first then raw side; None when neither has it"""
    local = box / relative
    if local.is_file():
        return local
    state = raw_state(box, root)
    if state["state"] == "ok" and relative in state["files"]:
        return state["rawDir"] / relative
    return None


def materialize(box: Path, dest: Path, root: Path) -> int:
    state = raw_state(box, root)
    if state["state"] not in READABLE_STATES:
        print(f"[raw-store] ERROR: {state['state']}: {state['reason']}", file=sys.stderr)
        return 1
    # ディスクIO（別ボリュームの生データ置き場からの複写）の境界。失敗は理由を出して呼び出し側を止める
    # Disk IO boundary (copying from a raw store that may sit on another volume); failure stops the caller with a reason
    try:
        for relative in state["files"]:
            target = dest / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(state["rawDir"] / relative, target)
    except OSError as error:
        print(f"[raw-store] ERROR: 生データを複写できない: {error}", file=sys.stderr)
        return 1
    print(len(state["files"]))
    return 0


def main(argv: list[str]) -> int:
    root = layout.default_root()
    if len(argv) == 2 and argv[0] == "state":
        state = raw_state(Path(argv[1]), root)
        print(f"{state['state']}\t{state['expiresAt']}\t{state['reason']}")
        return 0 if state["state"] in READABLE_STATES else 1
    if len(argv) == 3 and argv[0] == "materialize":
        return materialize(Path(argv[1]), Path(argv[2]), root)
    print("usage: raw_reader.py state <box> | materialize <box> <dest>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

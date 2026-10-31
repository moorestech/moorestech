#!/usr/bin/env python3
"""logs repo に既に入っている生データを生データ置き場へ移す一回きりの移行（標準ライブラリのみ）。migrate.sh から呼ぶ。

使い方: migrate.py [-n]
  -n は移す箱・件数・容量の一覧だけ出す。実行すると箱ごとに生データ置き場へ複写し、全ファイルを sha256 で突き合わせてから
  raw.json を置き、logs 側の git 管理のファイルは git rm、git 管理外（.gitignore で除外の動画等）は削除する。最後に
  harness/playtest だけを pathspec 指定で commit する（push はしない）。途中で止まっても再実行すれば続きから進む。
  期限は ingest.json の ingestedAt から数えるので、古い箱は移した直後の prune で消える。git の過去の履歴は書き換えない。

One-off migration moving raw data already inside the logs repo into the raw store (stdlib only); run via migrate.sh.
Usage: migrate.py [-n]. -n lists boxes, counts and sizes only. A real run copies each box, verifies every file by sha256,
places raw.json, then git-rms tracked files and deletes untracked ones (gitignored videos etc.) on the logs side, and finally
commits harness/playtest by pathspec (no push). An interrupted run resumes when re-run. Expiry counts from ingest.json's
ingestedAt, so old boxes go at the next prune. Past git history is not rewritten.
"""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import raw_layout as layout  # noqa: E402


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def raw_files(box: Path) -> list[dict]:
    files = []
    for relative in layout.walk_files(box):
        size = (box / relative).stat().st_size
        if not layout.keeps_in_logs(relative, size):
            files.append({"path": relative, "bytes": size})
    return files


def copy_verified(box: Path, files: list[dict], raw_dir: Path) -> None:
    """生データ側へ複写し（既にあれば複写を飛ばし）、全ファイルの sha256 が元と一致することを確かめる
    Copies into the raw side (skipping complete files already there) and checks every sha256 against the source"""
    for entry in files:
        source, target = box / entry["path"], raw_dir / entry["path"]
        # 前回の中断で途中までしか書けていない複写は大きさで見分けて取り直す
        # A copy left truncated by an interrupted run is told apart by size and redone
        if not target.is_file() or target.stat().st_size != entry["bytes"]:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
        if sha256(source) != sha256(target):
            raise ValueError(f"複写の中身が元と一致しない: {target}")


def migrate_box(logs: Path, box: Path, root: Path, days: int) -> list[str]:
    """1箱を移し、git rm すべき logs repo 相対パスを返す
    Migrates one box and returns the logs-repo-relative paths to git rm"""
    rel = layout.box_relative(box)
    files = raw_files(box)
    ref_path = box / layout.REF_NAME
    if ref_path.is_file():
        ref, reason = layout.read_ref(ref_path)
        if ref is None:
            raise ValueError(reason)
        listed = {entry["path"] for entry in ref["files"]}
        stray = [entry["path"] for entry in files if entry["path"] not in listed]
        if stray:
            raise ValueError(f"移行済みの raw.json に無い生データが箱に残っている（手で確かめる）: {stray[:3]}")
    else:
        ingested_at = json.loads((box / "ingest.json").read_text(encoding="utf-8"))["ingestedAt"]
        ref = layout.build_ref(rel, root, ingested_at, files, days)
    raw_dir = root / rel
    copy_verified(box, files, raw_dir)
    layout.write_ref(raw_dir / layout.REF_NAME, ref)
    layout.write_ref(ref_path, ref)
    tracked = set(git(logs, "ls-files", "-z", "--", str(box.relative_to(logs))).split("\0")) - {""}
    to_remove = []
    for entry in files:
        logs_rel = str((box / entry["path"]).relative_to(logs))
        if logs_rel in tracked:
            to_remove.append(logs_rel)
        else:
            os.remove(box / entry["path"])
    return to_remove


def git(logs: Path, *args: str, stdin: str | None = None) -> str:
    # ファイル名の * や [ を glob として読ませない（別のファイルを git rm しない）
    # Never let * or [ in file names act as globs (so git rm never hits another file)
    done = subprocess.run(["git", "-C", str(logs), "--literal-pathspecs", *args], input=stdin, capture_output=True, text=True)
    if done.returncode != 0:
        raise RuntimeError(f"git {args[0]} が失敗: {done.stderr.strip()}")
    return done.stdout


def boxes(logs: Path) -> tuple[list[Path], list[str]]:
    playtest = logs / "harness" / "playtest"
    found, not_boxes = [], []
    for kind in layout.KINDS:
        for box in sorted(p for p in (playtest / kind).glob("*/*") if p.is_dir() and not p.name.endswith(".partial")):
            (found if (box / "ingest.json").is_file() else not_boxes).append(box)
    skipped = sorted({str(p.parent.relative_to(playtest)) for p in not_boxes})
    return found, skipped


def run(logs: Path, root: Path, dry_run: bool) -> int:
    days = layout.retention_days()
    found, skipped = boxes(logs)
    for name in skipped:
        print(f"- 箱でない（ingest.json が無い）ので触らない: harness/playtest/{name}")
    moved = failed = total = 0
    for box in found:
        files = raw_files(box)
        if not files:
            continue  # silent-ok: 生データの無い箱・移行済みの箱は移す物が無い / 合計行の件数に出ないことで分かる
        size = sum(entry["bytes"] for entry in files)
        total += size
        print(f"- {'移す予定' if dry_run else '移す'}: {layout.box_relative(box)}（{len(files)}件・{size / 1e6:.1f}MB）")
        if dry_run:
            continue
        # ディスクIO・git・箱の JSON の境界。失敗はその箱の理由として出し、他の箱は続ける（失敗箱は logs 側に残る）
        # Disk IO, git and box-JSON boundary; a failure is reported for that box and the rest continue (it stays in logs)
        try:
            to_remove = migrate_box(logs, box, root, days)
            if to_remove:
                git(logs, "rm", "-q", "-f", "--pathspec-from-file=-", "--pathspec-file-nul", stdin="\0".join(to_remove))
            layout.remove_empty_dirs(box)
            moved += 1
        except (OSError, ValueError, KeyError, RuntimeError) as error:
            failed += 1
            print(f"  ! 移せなかった: {type(error).__name__}: {error}")
    print(f"合計 {total / 1e6:.1f}MB / 移した箱 {moved} / 失敗 {failed}{'（判定のみ）' if dry_run else ''}")
    if moved:
        git(logs, "add", "-A", "--", "harness/playtest")
        git(logs, "commit", "-qm", f"playtest: 生データ {moved} 箱を logs repo の外（{root}）へ移す", "--", "harness/playtest")
        print("logs repo へ commit した（push はしていない）")
    return 1 if failed else 0


def main(argv: list[str]) -> int:
    if argv not in ([], ["-n"]):
        print("usage: migrate.py [-n]", file=sys.stderr)
        return 2
    logs = layout.default_logs()
    root = layout.default_root()
    if not (logs / ".git").exists() or not root.is_dir():
        print(f"[raw-store] ERROR: logs repo（{logs}）か生データ置き場（{root}）が無い。何もしない", file=sys.stderr)
        return 1
    return run(logs, root, argv == ["-n"])


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

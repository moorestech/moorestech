#!/usr/bin/env python3
"""PRの報告ページ（画像・動画・検証結果）を組み立て、review.moores.tech/pr/<N>/report/ へ公開する。

使い方: publish_pr_report.py <PR番号> <manifest.json>
manifest の書式は .agents/skills/pr-create/references/pr-report.md が正本。
公開先は review.moores.tech サイトの報告ルート（mac-mini ローカル）。同じPRへ再実行すると丸ごと差し替える。

Builds a PR report page (images, videos, verification) and publishes it at review.moores.tech/pr/<N>/report/.
Usage: publish_pr_report.py <PR number> <manifest.json>. Rerunning for the same PR replaces it wholesale.
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path

from report_html import render

REPORT_ROOT = Path(os.environ.get(
    "PR_REPORT_ROOT", "/Users/sakastudio/hermes-agent/data/services/pr-review/reports"))
LOCAL_SITE = "http://127.0.0.1:8931"
PUBLIC_SITE = "https://review.moores.tech"
MEDIA_SUFFIXES = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".mp4", ".webm"}


def main(argv: list[str]) -> int:
    if len(argv) != 2 or not argv[0].isdigit():
        print("usage: publish_pr_report.py <PR番号> <manifest.json>", file=sys.stderr)
        return 2
    pr_number, manifest_path = argv[0], Path(argv[1]).resolve()
    if not REPORT_ROOT.is_dir():
        print(f"報告ルートが無い: {REPORT_ROOT}（mac-mini で実行する）", file=sys.stderr)
        return 1
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    errors = validate(manifest, manifest_path.parent)
    if errors:
        print("manifest が不正:\n- " + "\n- ".join(errors), file=sys.stderr)
        return 1
    pr = fetch_pr(pr_number)
    staging = REPORT_ROOT / f".pr-{pr_number}.staging"
    shutil.rmtree(staging, ignore_errors=True)
    staging.mkdir()
    media = copy_media(manifest, manifest_path.parent, staging)
    (staging / "index.html").write_text(render(pr, manifest, media), encoding="utf-8")
    swap_in(staging, REPORT_ROOT / f"pr-{pr_number}")
    missing = verify(pr_number, ["index.html"] + [m["name"] for m in media])
    if missing:
        print("公開後の取得に失敗: " + ", ".join(missing), file=sys.stderr)
        return 1
    print(f"{PUBLIC_SITE}/pr/{pr_number}/report/")
    print(f"画像 {sum(m['suffix'] not in {'.mp4', '.webm'} for m in media)} 枚 / "
          f"動画 {sum(m['suffix'] in {'.mp4', '.webm'} for m in media)} 本", file=sys.stderr)
    return 0


def validate(manifest: dict, base: Path) -> list[str]:
    errors = []
    for key in ("summary", "verification"):
        value = manifest.get(key)
        if not isinstance(value, list) or not value or not all(isinstance(v, str) and v for v in value):
            errors.append(f"{key} は空でない文字列の配列")
    media = manifest.get("media", [])
    if not isinstance(media, list):
        return errors + ["media は配列"]
    if not media and not manifest.get("no_media_reason"):
        errors.append("media が空なら no_media_reason（画像・動画が無い理由）が必須")
    for i, item in enumerate(media):
        path = resolve(base, item.get("file", ""))
        if not item.get("caption"):
            errors.append(f"media[{i}] に caption が無い")
        if path.suffix.lower() not in MEDIA_SUFFIXES:
            errors.append(f"media[{i}] の拡張子は {sorted(MEDIA_SUFFIXES)} のどれか: {path}")
        elif not path.is_file() or path.stat().st_size == 0:
            errors.append(f"media[{i}] が無いか 0 byte: {path}")
    return errors


def resolve(base: Path, file: str) -> Path:
    path = Path(file).expanduser()
    return path if path.is_absolute() else base / path


def fetch_pr(number: str) -> dict:
    out = subprocess.run(["gh", "pr", "view", number, "--repo", "moorestech/moorestech",
                          "--json", "number,title,url"], check=True, capture_output=True, text=True)
    return json.loads(out.stdout)


def copy_media(manifest: dict, base: Path, staging: Path) -> list[dict]:
    media = []
    for i, item in enumerate(manifest.get("media", []), start=1):
        source = resolve(base, item["file"])
        # 連番を前置して名前の衝突と並び順の揺れを無くす
        # A sequence prefix removes name collisions and ordering drift
        stem = re.sub(r"[^A-Za-z0-9._-]", "-", source.stem).strip(".-")[:80] or "media"
        name = f"{i:02d}-{stem}{source.suffix.lower()}"
        shutil.copy2(source, staging / name)
        media.append({"name": name, "suffix": source.suffix.lower(),
                      "caption": item["caption"], "group": item.get("group", "")})
    return media


def swap_in(staging: Path, target: Path) -> None:
    old = target.with_name(target.name + ".old")
    shutil.rmtree(old, ignore_errors=True)
    if target.exists():
        target.rename(old)
    staging.rename(target)
    shutil.rmtree(old, ignore_errors=True)


def verify(number: str, names: list[str]) -> list[str]:
    missing = []
    for name in names:
        url = f"{LOCAL_SITE}/pr/{number}/report/{'' if name == 'index.html' else name}"
        try:
            # review.moores.tech サイトは HEAD を持たないので先頭1バイトの Range GET で確かめる
            # The review site has no HEAD handler, so probe with a one-byte Range GET
            request = urllib.request.Request(url, headers={"Range": "bytes=0-0"})
            with urllib.request.urlopen(request, timeout=10) as res:
                if res.status not in (200, 206):
                    missing.append(f"{name}({res.status})")
        except (urllib.error.URLError, OSError) as error:
            missing.append(f"{name}({error})")
    return missing


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

#!/usr/bin/env python3
"""Capture manifestの全グリップcomponentをTSVへ展開する。"""

import argparse
import csv
import hashlib
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPOSITORY_ROOT = Path(__file__).resolve().parents[4]
# 比較器はworktree位置から解決して環境固有パスを排除する
# Resolve the comparator from the worktree to avoid machine-specific paths
sys.path.insert(0, str(REPOSITORY_ROOT / "moorestech_web/webui/e2e/craft-chrome"))
from compare import detect_panel
from shape_profiles import GRIP_MIN_DIMENSION, PIXEL_CONNECTIVITY_RADIUS, components, grip_zone, touches_frame


def component_detail(mask: np.ndarray, frame: np.ndarray, component: tuple[int, int, int, int, int], x0: int, y0: int) -> tuple[str, bool, int]:
    left, top, right, bottom, count = component
    touches = touches_frame(mask, frame, left, top, right, bottom, x0, y0)
    minimum = right - left + 1 >= GRIP_MIN_DIMENSION and bottom - top + 1 >= GRIP_MIN_DIMENSION
    detail = f"({left},{top})-({right},{bottom})/{count}/touch={int(touches)}/min={int(minimum)}"
    return detail, not touches and minimum, count


def analyze_capture(capture_file: Path) -> tuple[str, str, str]:
    # 色maskの全成分と選択結果をcapture実体から再計算する
    # Recompute every color-mask component and the selected result from the capture itself
    image = np.asarray(Image.open(capture_file).convert("RGB"))
    _, _, panel_right, panel_bottom = detect_panel(image)
    _, mask, frame, x0, y0 = grip_zone(image, (0, 0, panel_right, panel_bottom))
    raw_components = components(mask, x0, y0, radius=PIXEL_CONNECTIVITY_RADIUS)
    details = [component_detail(mask, frame, component, x0, y0) for component in raw_components]
    selected_index = max((index for index, (_, eligible, _) in enumerate(details) if eligible), key=lambda index: details[index][2])
    left, top, right, bottom = raw_components[selected_index][:4]
    inventory = ";".join(f"{detail}/selected={int(index == selected_index)}" for index, (detail, _, _) in enumerate(details))
    return inventory, f"({left},{top})-({right},{bottom})", f"{panel_right - right - 1},{panel_bottom - bottom - 1}"


def verify_capture_hash(row: dict[str, str]) -> None:
    capture_file = Path(row["capture_file"])
    # raw manifestと画像実体のSHA256を先に照合する
    # Verify the raw manifest SHA256 against the capture before analysis
    actual = hashlib.sha256(capture_file.read_bytes()).hexdigest()
    if actual != row["sha256"]:
        raise ValueError(f"SHA256 mismatch for {capture_file}: manifest={row['sha256']} actual={actual}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--offset", type=int, default=0)
    parser.add_argument("--limit", type=int)
    parser.add_argument("--append", action="store_true")
    arguments = parser.parse_args()
    with arguments.manifest.open(newline="") as source:
        rows = list(csv.DictReader(source, delimiter="\t"))
    # 長い監査をchunk出力できるよう入力範囲を限定する
    # Limit the input range so long audits can be emitted in chunks
    rows = rows[arguments.offset:None if arguments.limit is None else arguments.offset + arguments.limit]
    # 解析出力より先に入力画像の完全性を検証する
    # Validate every input capture before writing analysis output
    for row in rows:
        verify_capture_hash(row)
    fields = list(rows[0])
    for field in ("raw_components", "post_bbox", "gaps"):
        if field not in fields:
            fields.append(field)
    mode = "a" if arguments.append else "w"
    with arguments.output.open(mode, newline="") as target:
        writer = csv.DictWriter(target, fieldnames=fields, delimiter="\t", lineterminator="\n")
        if not arguments.append:
            writer.writeheader()
        for row in rows:
            inventory, post_bbox, gaps = analyze_capture(Path(row["capture_file"]))
            writer.writerow(row | {"raw_components": inventory, "post_bbox": post_bbox, "gaps": gaps})


if __name__ == "__main__":
    main()

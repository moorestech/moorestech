#!/usr/bin/env python3
"""Normalized visual comparator for the craft tab and corner grip."""

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

EXPECTED_SIZE = (3270, 1844)
TAB_SIZE = (166, 70)
TAB_LEFT_DELTA = 0
TAB_BOTTOM_GAP = (0, 3)
HAMMER_BOX = (44, -52, 99, 0)
GRIP_SIZE = (22, 22)
GRIP_RIGHT_GAP = 19
GRIP_BOTTOM_GAP = 19
GEOMETRY_TOLERANCE = 1
HAMMER_TOLERANCE = 2
COLOR_TOLERANCE = 15


def detect_panel(image: np.ndarray) -> tuple[int, int, int, int]:
    dark = image.max(axis=2) < 110
    zone = dark[250:1550, 1150:2150]
    columns = np.where(zone.mean(axis=0) > 0.5)[0]
    rows = np.where(zone.mean(axis=1) > 0.5)[0]
    if not len(columns) or not len(rows):
        raise ValueError("craft panel was not detected")
    return (int(columns[0] + 1150), int(rows[0] + 250), int(columns[-1] + 1150), int(rows[-1] + 250))


def components(mask: np.ndarray, x0: int, y0: int, radius: int = 1) -> list[tuple[int, int, int, int, int]]:
    seen = np.zeros(mask.shape, dtype=bool)
    found = []
    for y, x in np.argwhere(mask):
        if seen[y, x]:
            continue
        stack, seen[y, x] = [(int(y), int(x))], True
        count, min_x, min_y, max_x, max_y = 0, x, y, x, y
        while stack:
            cy, cx = stack.pop()
            count, min_x, min_y = count + 1, min(min_x, cx), min(min_y, cy)
            max_x, max_y = max(max_x, cx), max(max_y, cy)
            for ny in range(cy - radius, cy + radius + 1):
                for nx in range(cx - radius, cx + radius + 1):
                    if 0 <= ny < mask.shape[0] and 0 <= nx < mask.shape[1] and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        found.append((int(min_x + x0), int(min_y + y0), int(max_x + x0), int(max_y + y0), count))
    return found


def detect_tab(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    left, top, _, _ = panel
    x0, y0, x1, y1 = left - 8, top - 100, left + 191, top - 2
    mask = image[y0:y1, x0:x1].max(axis=2) < 120
    candidates = components(mask, x0, y0)
    return max(candidates, key=lambda item: item[4])[:4]


def detect_grip(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    _, _, right, bottom = panel
    x0, y0, x1, y1 = right - 80, bottom - 80, right + 1, bottom + 1
    zone = image[y0:y1, x0:x1]
    mask = (zone.max(axis=2) - zone.min(axis=2) < 35) & (zone.mean(axis=2) >= 70) & (zone.mean(axis=2) <= 190)
    candidates = []
    for left, top, edge_right, edge_bottom, count in components(mask, x0, y0):
        if edge_right >= right - 10 or edge_bottom >= bottom - 10:
            continue
        if edge_right - left + 1 >= 5 and edge_bottom - top + 1 >= 5:
            candidates.append((left, top, edge_right, edge_bottom, count))
    if not candidates:
        raise ValueError("corner grip was not detected")
    return max(candidates, key=lambda item: item[4])[:4]


def detect_hammer(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    left, top, _, _ = panel
    x0, y0, x1, y1 = left - 8, top - 100, left + 191, top
    zone = image[y0:y1, x0:x1]
    mask = (zone.max(axis=2) - zone.min(axis=2) < 15) & (zone.mean(axis=2) >= 68) & (zone.mean(axis=2) <= 100)
    candidates = [item for item in components(mask, x0, y0, 3) if left + 35 <= (item[0] + item[2]) / 2 <= left + 110]
    if not candidates:
        raise ValueError("tab hammer was not detected")
    return max(candidates, key=lambda item: item[4])[:4]


def relative(box: tuple[int, int, int, int], panel: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    return (box[0] - panel[0], box[1] - panel[1], box[2] - panel[0], box[3] - panel[1])


def median_color(image: np.ndarray, x: int, y: int) -> tuple[int, int, int]:
    return tuple(int(value) for value in np.median(image[y - 2:y + 3, x - 2:x + 3], axis=(0, 1)))


def save_artifacts(ref: np.ndarray, cur: np.ndarray, ref_panel: tuple[int, int, int, int], cur_panel: tuple[int, int, int, int], output: Path) -> None:
    # 比較しやすい同寸の正本・現状・合成・差分を出力する
    # Emit equal-sized reference, current, blend, and diff images for inspection
    output.mkdir(parents=True, exist_ok=True)
    for name, ref_box, cur_box in (("tab", (-20, -110, 210, 30), (-20, -110, 210, 30)), ("grip", (-120, -120, 20, 20), (-120, -120, 20, 20))):
        ref_crop = crop(ref, ref_panel, ref_box, name == "grip")
        cur_crop = crop(cur, cur_panel, cur_box, name == "grip")
        Image.fromarray(ref_crop).save(output / f"{name}-ref.png")
        Image.fromarray(cur_crop).save(output / f"{name}-cur.png")
        Image.fromarray(((ref_crop.astype(np.uint16) + cur_crop.astype(np.uint16)) // 2).astype(np.uint8)).save(output / f"{name}-blend.png")
        Image.fromarray(np.abs(ref_crop.astype(np.int16) - cur_crop.astype(np.int16)).astype(np.uint8)).save(output / f"{name}-diff.png")


def crop(image: np.ndarray, panel: tuple[int, int, int, int], box: tuple[int, int, int, int], from_right: bool) -> np.ndarray:
    anchor_x, anchor_y = (panel[2], panel[3]) if from_right else (panel[0], panel[1])
    return image[anchor_y + box[1]:anchor_y + box[3], anchor_x + box[0]:anchor_x + box[2]]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ref", required=True)
    parser.add_argument("--cur", required=True)
    parser.add_argument("--out")
    args = parser.parse_args()
    ref, cur = (np.asarray(Image.open(path).convert("RGB")) for path in (args.ref, args.cur))
    if any((image.shape[1], image.shape[0]) != EXPECTED_SIZE for image in (ref, cur)):
        print(f"error: both images must be {EXPECTED_SIZE[0]}x{EXPECTED_SIZE[1]}", file=sys.stderr)
        return 2
    # パネルと装飾を正規化座標で検出する
    # Detect the panel and decorations in normalized coordinates
    try:
        ref_panel, cur_panel = detect_panel(ref), detect_panel(cur)
        ref_tab, cur_tab = detect_tab(ref, ref_panel), detect_tab(cur, cur_panel)
        ref_hammer, cur_hammer = detect_hammer(ref, ref_panel), detect_hammer(cur, cur_panel)
        ref_grip, cur_grip = detect_grip(ref, ref_panel), detect_grip(cur, cur_panel)
    except ValueError as error:
        print(f"error: {error}", file=sys.stderr)
        return 2
    results = []
    def check(name: str, ok: bool, detail: str) -> None:
        results.append(ok)
        print(f"[{'PASS' if ok else 'FAIL'}] {name}: {detail}")
    def dimensions(box: tuple[int, int, int, int]) -> tuple[int, int]: return box[2] - box[0] + 1, box[3] - box[1] + 1
    def delta(box: tuple[int, int, int, int], expected: tuple[int, int, int, int]) -> int: return max(abs(value - target) for value, target in zip(box, expected))
    # 幾何値と色を正本の目標値に照合する
    # Check geometry and colors against the reference-derived targets
    panel_delta = max(abs(a - b) for a, b in zip(dimensions(cur_panel), dimensions(ref_panel)))
    check("panel-size", panel_delta <= GEOMETRY_TOLERANCE, f"ref={ref_panel} cur={cur_panel} maxΔ={panel_delta}")
    tab_size, tab_left, tab_bottom_gap = dimensions(cur_tab), cur_tab[0] - cur_panel[0], cur_panel[1] - cur_tab[3] - 1
    check("tab-size", max(abs(a - b) for a, b in zip(tab_size, TAB_SIZE)) <= GEOMETRY_TOLERANCE, f"bbox={cur_tab} size={tab_size} maxΔ={max(abs(a - b) for a, b in zip(tab_size, TAB_SIZE))}")
    check("tab-left", abs(tab_left - TAB_LEFT_DELTA) <= GEOMETRY_TOLERANCE, f"bbox={cur_tab} got={tab_left} maxΔ={abs(tab_left - TAB_LEFT_DELTA)}")
    check("tab-bottom-gap", TAB_BOTTOM_GAP[0] <= tab_bottom_gap <= TAB_BOTTOM_GAP[1], f"bbox={cur_tab} got={tab_bottom_gap} range={TAB_BOTTOM_GAP}")
    hammer_delta = delta(relative(cur_hammer, cur_panel), HAMMER_BOX)
    check("hammer-box", hammer_delta <= HAMMER_TOLERANCE, f"bbox={cur_hammer} relative={relative(cur_hammer, cur_panel)} maxΔ={hammer_delta}")
    grip_size = dimensions(cur_grip)
    check("grip-size", max(abs(a - b) for a, b in zip(grip_size, GRIP_SIZE)) <= GEOMETRY_TOLERANCE, f"bbox={cur_grip} size={grip_size} maxΔ={max(abs(a - b) for a, b in zip(grip_size, GRIP_SIZE))}")
    for name, got, target in (("grip-right-gap", cur_panel[2] - cur_grip[2] - 1, GRIP_RIGHT_GAP), ("grip-bottom-gap", cur_panel[3] - cur_grip[3] - 1, GRIP_BOTTOM_GAP)):
        check(name, abs(got - target) <= GEOMETRY_TOLERANCE, f"bbox={cur_grip} got={got} maxΔ={abs(got - target)}")
    for name, dx, dy in (("tab-front", 105, -45), ("tab-back", 8, -60), ("tab-right-slope", 140, -20), ("hammer", 70, -30), ("grip", -27, -27)):
        ref_color, cur_color = median_color(ref, ref_panel[0] + dx, ref_panel[1] + dy), median_color(cur, cur_panel[0] + dx, cur_panel[1] + dy)
        color_delta = max(abs(a - b) for a, b in zip(ref_color, cur_color))
        check(f"color:{name}", color_delta <= COLOR_TOLERANCE, f"ref={ref_color} cur={cur_color} maxΔ={color_delta}")
    if args.out:
        save_artifacts(ref, cur, ref_panel, cur_panel, Path(args.out))
    print(f"\n== {sum(results)}/{len(results)} checks passed ==")
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Normalized visual comparator for the craft tab and corner grip."""

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

from shape_profiles import components, detect_grip, detect_hammer, hammer_mask, row_endpoint_delta, row_endpoints, side_components

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
SHAPE_AREA_TOLERANCE = 0.05
COLOR_TOLERANCE = 15


def detect_panel(image: np.ndarray) -> tuple[int, int, int, int]:
    dark = image.max(axis=2) < 110
    zone = dark[250:1550, 1150:2150]
    columns = np.where(zone.mean(axis=0) > 0.5)[0]
    rows = np.where(zone.mean(axis=1) > 0.5)[0]
    if not len(columns) or not len(rows):
        raise ValueError("craft panel was not detected")
    return int(columns[0] + 1150), int(rows[0] + 250), int(columns[-1] + 1150), int(rows[-1] + 250)


def detect_tab(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    left, top, _, _ = panel
    x0, y0, x1, y1 = left - 8, top - 100, left + 191, top - 2
    mask = image[y0:y1, x0:x1].max(axis=2) < 120
    candidates = components(mask, x0, y0, radius=1)
    return max(candidates, key=lambda item: item[4])[:4]


def relative(box: tuple[int, int, int, int], panel: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    return box[0] - panel[0], box[1] - panel[1], box[2] - panel[0], box[3] - panel[1]


def median_color(image: np.ndarray, x: int, y: int) -> tuple[int, int, int]:
    return tuple(int(value) for value in np.median(image[y - 2:y + 3, x - 2:x + 3], axis=(0, 1)))


def crop(image: np.ndarray, panel: tuple[int, int, int, int], box: tuple[int, int, int, int], from_right: bool) -> np.ndarray:
    anchor_x, anchor_y = (panel[2], panel[3]) if from_right else (panel[0], panel[1])
    return image[anchor_y + box[1]:anchor_y + box[3], anchor_x + box[0]:anchor_x + box[2]]


def save_artifacts(ref: np.ndarray, cur: np.ndarray, ref_panel: tuple[int, int, int, int], cur_panel: tuple[int, int, int, int], output: Path) -> None:
    # 比較しやすい同寸の正本・現状・合成・差分を出力する
    # Emit equal-sized reference, current, blend, and diff images for inspection
    output.mkdir(parents=True, exist_ok=True)
    for name, box, from_right in (("tab", (-20, -110, 210, 30), False), ("grip", (-120, -120, 20, 20), True)):
        ref_crop, cur_crop = crop(ref, ref_panel, box, from_right), crop(cur, cur_panel, box, from_right)
        Image.fromarray(ref_crop).save(output / f"{name}-ref.png")
        Image.fromarray(cur_crop).save(output / f"{name}-cur.png")
        Image.fromarray(((ref_crop.astype(np.uint16) + cur_crop.astype(np.uint16)) // 2).astype(np.uint8)).save(output / f"{name}-blend.png")
        Image.fromarray(np.abs(ref_crop.astype(np.int16) - cur_crop.astype(np.int16)).astype(np.uint8)).save(output / f"{name}-diff.png")


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
        ref_side_left, ref_side_right, ref_side_mask, ref_side_x, ref_side_y = side_components(ref, ref_panel)
        cur_side_left, cur_side_right, cur_side_mask, cur_side_x, cur_side_y = side_components(cur, cur_panel)
    except ValueError as error:
        print(f"error: {error}", file=sys.stderr)
        return 2
    results = []
    def check(name: str, ok: bool, detail: str) -> None:
        results.append(ok)
        print(f"[{'PASS' if ok else 'FAIL'}] {name}: {detail}")
    def dimensions(box: tuple[int, int, int, int]) -> tuple[int, int]: return box[2] - box[0] + 1, box[3] - box[1] + 1
    def delta(box: tuple[int, int, int, int], expected: tuple[int, int, int, int]) -> int: return max(abs(value - target) for value, target in zip(box, expected))
    def area_ok(current: int, reference: int) -> bool: return abs(current - reference) <= round(reference * SHAPE_AREA_TOLERANCE)
    def profile_check(name: str, reference: dict[int, tuple[int, int]], current: dict[int, tuple[int, int]]) -> None:
        endpoint_delta = row_endpoint_delta(reference, current)
        check(name, endpoint_delta <= GEOMETRY_TOLERANCE, f"max endpoint Δ={endpoint_delta}")
    # 幾何値と色を正本の目標値に照合する
    # Check geometry and colors against the reference-derived targets
    panel_delta = max(abs(a - b) for a, b in zip(dimensions(cur_panel), dimensions(ref_panel)))
    check("panel-size", panel_delta <= GEOMETRY_TOLERANCE, f"ref={ref_panel} cur={cur_panel} maxΔ={panel_delta}")
    tab_size, tab_left, tab_bottom_gap = dimensions(cur_tab), cur_tab[0] - cur_panel[0], cur_panel[1] - cur_tab[3] - 1
    check("tab-size", max(abs(a - b) for a, b in zip(tab_size, TAB_SIZE)) <= GEOMETRY_TOLERANCE, f"bbox={cur_tab} size={tab_size}")
    check("tab-left", abs(tab_left - TAB_LEFT_DELTA) <= GEOMETRY_TOLERANCE, f"bbox={cur_tab} got={tab_left}")
    check("tab-bottom-gap", TAB_BOTTOM_GAP[0] <= tab_bottom_gap <= TAB_BOTTOM_GAP[1], f"bbox={cur_tab} got={tab_bottom_gap} range={TAB_BOTTOM_GAP}")
    hammer_delta = delta(relative(cur_hammer[:4], cur_panel), HAMMER_BOX)
    check("hammer-box", hammer_delta <= HAMMER_TOLERANCE, f"relative={relative(cur_hammer[:4], cur_panel)} maxΔ={hammer_delta}")
    check("hammer-area", area_ok(cur_hammer[4], ref_hammer[4]), f"ref={ref_hammer[4]} cur={cur_hammer[4]} tolerance={round(ref_hammer[4] * SHAPE_AREA_TOLERANCE)}")
    ref_hammer_mask, ref_hammer_x, ref_hammer_y = hammer_mask(ref, ref_panel)
    cur_hammer_mask, cur_hammer_x, cur_hammer_y = hammer_mask(cur, cur_panel)
    profile_check("hammer-row-endpoints", row_endpoints(ref_hammer_mask, ref_hammer, ref_hammer_x, ref_hammer_y, ref_panel), row_endpoints(cur_hammer_mask, cur_hammer, cur_hammer_x, cur_hammer_y, cur_panel))
    for name, reference, current, ref_mask, cur_mask, ref_x, ref_y, cur_x, cur_y in (("side-left", ref_side_left, cur_side_left, ref_side_mask, cur_side_mask, ref_side_x, ref_side_y, cur_side_x, cur_side_y), ("side-right", ref_side_right, cur_side_right, ref_side_mask, cur_side_mask, ref_side_x, ref_side_y, cur_side_x, cur_side_y)):
        check(f"{name}-area", area_ok(current[4], reference[4]), f"ref={reference[4]} cur={current[4]} tolerance={round(reference[4] * SHAPE_AREA_TOLERANCE)}")
        box_delta = delta(relative(current[:4], cur_panel), relative(reference[:4], ref_panel))
        check(f"{name}-box", box_delta <= GEOMETRY_TOLERANCE, f"ref={relative(reference[:4], ref_panel)} cur={relative(current[:4], cur_panel)} maxΔ={box_delta}")
        profile_check(f"{name}-row-endpoints", row_endpoints(ref_mask, reference, ref_x, ref_y, ref_panel), row_endpoints(cur_mask, current, cur_x, cur_y, cur_panel))
    grip_size = dimensions(cur_grip)
    check("grip-size", max(abs(a - b) for a, b in zip(grip_size, GRIP_SIZE)) <= GEOMETRY_TOLERANCE, f"bbox={cur_grip} size={grip_size}")
    for name, got, target in (("grip-right-gap", cur_panel[2] - cur_grip[2] - 1, GRIP_RIGHT_GAP), ("grip-bottom-gap", cur_panel[3] - cur_grip[3] - 1, GRIP_BOTTOM_GAP)):
        check(name, abs(got - target) <= GEOMETRY_TOLERANCE, f"bbox={cur_grip} got={got} maxΔ={abs(got - target)}")
    for name, dx, dy, from_right in (("tab-front", 105, -45, False), ("tab-back", 8, -60, False), ("tab-right-slope", 140, -20, False), ("hammer", 70, -30, False), ("grip", -27, -27, True)):
        ref_anchor, cur_anchor = (ref_panel[2], ref_panel[3]) if from_right else (ref_panel[0], ref_panel[1]), (cur_panel[2], cur_panel[3]) if from_right else (cur_panel[0], cur_panel[1])
        ref_color, cur_color = median_color(ref, ref_anchor[0] + dx, ref_anchor[1] + dy), median_color(cur, cur_anchor[0] + dx, cur_anchor[1] + dy)
        color_delta = max(abs(a - b) for a, b in zip(ref_color, cur_color))
        check(f"color:{name}", color_delta <= COLOR_TOLERANCE, f"ref={ref_color} cur={cur_color} maxΔ={color_delta}")
    if args.out:
        save_artifacts(ref, cur, ref_panel, cur_panel, Path(args.out))
    print(f"\n== {sum(results)}/{len(results)} checks passed ==")
    return 0 if all(results) else 1
if __name__ == "__main__":
    sys.exit(main())

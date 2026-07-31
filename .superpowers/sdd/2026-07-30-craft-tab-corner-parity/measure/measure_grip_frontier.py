#!/usr/bin/env python3
"""Task 6 round-2 frontier captures をTSVで再集計する。"""

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, "/Users/sakastudio/orca/workspaces/moorestech/web-ui-craft/moorestech_web/webui/e2e/craft-chrome")
from compare import components, detect_frame, detect_panel, touches_frame

SIZES = ("8.70", "8.71", "8.72", "8.74", "8.75", "8.77", "8.79", "8.80")
INSETS = ("7.00", "6.99", "6.69", "6.68", "6.62", "6.50")
PANEL_RECT = (604.2838745117188, 166.71148681640625, 1034.9881591796875, 719.7614135742188)


def computed_size(size: str) -> float:
    return int(float(size) * 64) / 64


def component_detail(mask: np.ndarray, frame: np.ndarray, component: tuple[int, int, int, int, int], x0: int, y0: int) -> tuple[str, bool, int]:
    left, top, right, bottom, count = component
    touches = touches_frame(mask, frame, left, top, right, bottom, x0, y0)
    minimum = int(right - left + 1 >= 5 and bottom - top + 1 >= 5)
    detail = f"({left},{top})-({right},{bottom})/{count}/touch={int(touches)}/min={minimum}"
    return detail, not touches and bool(minimum), count


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--captures", type=Path, default=Path("/tmp"))
    arguments = parser.parse_args()
    print("size_token\tcomputed_width\tinset_token\tdom_box\traw_components\tselected\tpost_bbox\tgaps")
    for size in SIZES:
        for inset in INSETS:
            path = arguments.captures / f"webui-craft-round2-settled-frontier-s{size}-i{inset}.png"
            image = np.asarray(Image.open(path).convert("RGB"))
            _, _, panel_right, panel_bottom = detect_panel(image)
            x0, y0 = panel_right - 80, panel_bottom - 80
            zone = image[y0:panel_bottom + 1, x0:panel_right + 1]
            mask = (zone.max(axis=2) - zone.min(axis=2) < 35) & (zone.mean(axis=2) >= 70) & (zone.mean(axis=2) <= 190)
            frame = detect_frame(zone.max(axis=2) < 70)
            raw_components = components(mask, x0, y0, radius=1)
            details = [component_detail(mask, frame, component, x0, y0) for component in raw_components]
            selected_index = max((index for index, (_, eligible, _) in enumerate(details) if eligible), key=lambda index: details[index][2])
            selected = details[selected_index]
            left, top, right, bottom = raw_components[selected_index][:4]
            width = computed_size(size)
            inset_value = float(inset)
            dom_box = (PANEL_RECT[2] - inset_value - width, PANEL_RECT[3] - inset_value - width, PANEL_RECT[2] - inset_value, PANEL_RECT[3] - inset_value)
            component_tsv = ";".join(f"{detail}/selected={int(index == selected_index)}" for index, (detail, _, _) in enumerate(details))
            post_bbox = f"({left},{top})-({right},{bottom})"
            gaps = f"{panel_right - right - 1},{panel_bottom - bottom - 1}"
            print(f"{size}\t{width:.5f}\t{inset}\t{dom_box}\t{component_tsv}\t{selected[0]}\t{post_bbox}\t{gaps}")


if __name__ == "__main__":
    main()

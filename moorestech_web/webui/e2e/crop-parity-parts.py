#!/usr/bin/env python3
# 正本uGUIスクショと現状web UIスクショを同一座標でパーツ別にクロップする採点補助ツール
# Scoring helper that crops the uGUI reference and the current web UI shot at identical coordinates
#
# 座標は正本 docs/webui-parity/reference-player-inventory-3270x1844.png 上の画素実測bboxに
# マージンを付けたもの。両画像は3270x1844で同寸であること（capture-eval.ts CAPTURE_VIEWPORT_W=1635 H=922）。
# Boxes are pixel-measured bboxes on the 3270x1844 reference plus margins. Both inputs must be 3270x1844
# (capture-eval.ts with CAPTURE_VIEWPORT_W=1635 CAPTURE_VIEWPORT_H=922).

import argparse
import sys
from pathlib import Path

from PIL import Image, ImageChops

# パーツ名 → (left, top, right, bottom)。実測bbox+約60pxマージン（下端は画像端でクランプ）
# Part name -> (left, top, right, bottom). Measured bbox + ~60px margin (bottom clamped at image edge)
CROP_BOXES = {
    "inventory": (100, 200, 1170, 1530),
    "craft": (1150, 170, 2130, 1470),
    "recipe": (2110, 210, 3180, 1530),
    "hotbar": (930, 1600, 2350, 1844),
}

# 細部サブクロップ。基準は docs/webui-parity/parts-eval-criteria.md §2.4
# Fine-grained sub-crops; the authority is docs/webui-parity/parts-eval-criteria.md §2.4
SUBCROP_BOXES = {
    "inv-header": (170, 260, 1100, 420),
    "inv-slots-top": (180, 390, 1090, 730),
    "inv-slots-empty": (180, 690, 940, 1040),
    "inv-bottom-deco": (170, 1210, 1100, 1510),
    "craft-tab-title": (1140, 190, 2100, 470),
    "craft-selection": (1190, 430, 2080, 730),
    "craft-body": (1250, 700, 2030, 1230),
    "craft-button": (1390, 1220, 2100, 1440),
    "recipe-header": (2160, 260, 3130, 420),
    "recipe-grid-top": (2170, 390, 2790, 1050),
    "recipe-scrollbar": (2860, 370, 3160, 1430),
    "recipe-bottom-deco": (2160, 1220, 3140, 1510),
    "hotbar-full": (950, 1600, 2320, 1844),
    "hotbar-selected": (950, 1630, 1170, 1844),
    "hotbar-mid": (1320, 1640, 1770, 1844),
    "hotbar-right": (1770, 1640, 2320, 1844),
    "sort-button": (2980, 0, 3270, 150),
    "key-hints": (0, 1600, 1050, 1844),
}

EXPECTED_SIZE = (3270, 1844)


def main() -> int:
    parser = argparse.ArgumentParser(description="Crop parity parts from reference/current screenshots")
    parser.add_argument("--ref", required=True, help="uGUI reference screenshot (3270x1844)")
    parser.add_argument("--cur", required=True, help="current web UI screenshot (3270x1844)")
    parser.add_argument("--out", required=True, help="output directory")
    parser.add_argument("--parts", nargs="*", default=list(CROP_BOXES), help="subset of parts to crop")
    parser.add_argument("--overlay", action="store_true", help="also write 50%% blend and difference images")
    parser.add_argument("--subcrops", action="store_true", help="also write fine-grained sub-crops (criteria §2.4)")
    args = parser.parse_args()

    ref = Image.open(args.ref).convert("RGB")
    cur = Image.open(args.cur).convert("RGB")

    # 同寸でなければクロップ座標が意味を失うため即失敗させる
    # Fail fast on size mismatch; identical crop coordinates would be meaningless
    for name, im in (("ref", ref), ("cur", cur)):
        if im.size != EXPECTED_SIZE:
            print(f"error: {name} size {im.size} != expected {EXPECTED_SIZE}", file=sys.stderr)
            return 1

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    for part in args.parts:
        box = CROP_BOXES[part]
        ref_crop = ref.crop(box)
        cur_crop = cur.crop(box)
        ref_crop.save(out / f"{part}-ref.png")
        cur_crop.save(out / f"{part}-cur.png")
        if args.overlay:
            # 50%ブレンドは位置ズレの目視用、differenceは変化画素の機械検出用
            # The 50% blend visualizes misalignment; difference isolates changed pixels mechanically
            Image.blend(ref_crop, cur_crop, 0.5).save(out / f"{part}-blend.png")
            ImageChops.difference(ref_crop, cur_crop).save(out / f"{part}-diff.png")
        print(f"{part}: box={box} -> {part}-ref.png / {part}-cur.png")

    if args.subcrops:
        sub_dir = out / "subcrops"
        sub_dir.mkdir(exist_ok=True)
        for name, box in SUBCROP_BOXES.items():
            ref.crop(box).save(sub_dir / f"{name}-ref.png")
            cur.crop(box).save(sub_dir / f"{name}-cur.png")
        print(f"subcrops: {len(SUBCROP_BOXES)} pairs -> {sub_dir}/")

    return 0


if __name__ == "__main__":
    sys.exit(main())

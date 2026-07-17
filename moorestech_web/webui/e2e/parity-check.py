#!/usr/bin/env python3
# parts-eval-criteria.md の[実測]数値軸を機械判定するチェッカー。正本目標値は基準ドキュメントが正
# Mechanical checker for the [実測] numeric axes; the criteria doc is the authority for targets
# 使い方 / Usage: python3 e2e/parity-check.py --cur out/current.png

import argparse
import sys

import numpy as np
from PIL import Image

# 色ピック表（基準§2.2）: 名前, x, y, 正本RGB / Color-pick table (criteria §2.2)
COLOR_POINTS = [
    ("bg-top", 1635, 100, (143, 120, 96)), ("bg-left", 40, 900, (139, 95, 49)),
    ("bg-bottom", 1600, 1550, (129, 74, 35)), ("bg-hints", 500, 1700, (127, 69, 32)),
    ("inv-header", 600, 330, (67, 54, 44)), ("inv-bottom", 600, 1350, (63, 38, 29)),
    ("inv-empty", 700, 790, (58, 48, 51)), ("inv-white", 238, 450, (254, 254, 254)),
    ("craft-top", 1350, 455, (66, 51, 37)), ("craft-mid", 1650, 700, (66, 47, 32)),
    ("craft-low1", 1650, 900, (65, 44, 27)), ("craft-low2", 1650, 1250, (63, 38, 25)),
    ("rec-header", 2600, 330, (67, 54, 44)), ("rec-bottom", 2600, 1450, (63, 37, 29)),
    ("rec-gray", 2232, 452, (126, 126, 126)), ("rec-white", 2820, 740, (255, 255, 255)),
    ("hb-white", 1450, 1740, (253, 253, 253)), ("hb-bg", 1450, 1625, (128, 71, 33)),
]

# bbox目標（基準1章・[実測]値）: 名前, (l,t,r,b), 許容px / bbox targets from criteria ch.1
BBOX_TARGETS = {
    "inv-panel": ((160, 278, 1113, 1473), 6),
    "craft-panel": ((1210, 300, 2071, 1405), 3),
    "recipe-panel": ((2168, 280, 3121, 1473), 6),
    "selection-frame": ((1250, 492, 2015, 651), 3),
    "tree-button": ((1502, 422, 1773, 469), 3),
    "craft-button": ((1502, 1302, 1775, 1351), 4),
    "craft-tab": ((1210, 228, 1375, 297), 4),
    "sort-button": ((3028, 32, 3249, 105), 3),
    "key-hints": ((20, 1656, 993, 1811), 3),
    "hotbar-ring": ((994, 1704, 1125, 1835), 3),
    "scroll-knob": ((3078, 434, 3087, 1103), 4),
}

results = []


def check(name, ok, detail):
    results.append((name, ok, detail))
    print(f'[{"PASS" if ok else "FAIL"}] {name}: {detail}')


def bbox_of(mask):
    ys, xs = np.where(mask)
    return (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())) if len(xs) else None


def check_bbox(name, got):
    target, tol = BBOX_TARGETS[name]
    if got is None:
        check(name, False, f"not detected (target={target})")
        return
    d = max(abs(a - b) for a, b in zip(got, target))
    check(name, d <= tol, f"target={target} got={got} maxΔ={d} tol={tol}")


def runs_of(profile, minlen, thr):
    out, inrun, start = [], False, 0
    for i, v in enumerate(profile > thr):
        if v and not inrun: start, inrun = i, True
        elif not v and inrun:
            if i - start >= minlen: out.append((start, i))
            inrun = False
    if inrun and len(profile) - start >= minlen: out.append((start, len(profile)))
    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cur", required=True, help="current 3270x1844 screenshot")
    args = parser.parse_args()
    im = np.asarray(Image.open(args.cur).convert("RGB")).astype(int)
    if im.shape[:2] != (1844, 3270):
        print(f"error: size {im.shape[1]}x{im.shape[0]} != 3270x1844", file=sys.stderr)
        return 2
    R, G, B = im[:, :, 0], im[:, :, 1], im[:, :, 2]

    # 色19点: 5x5中央値でRGB各±15 / 19 color points, 5x5 median, ±15 per channel
    for name, x, y, target in COLOR_POINTS:
        p = im[y - 2:y + 3, x - 2:x + 3].reshape(-1, 3)
        got = tuple(int(v) for v in np.median(p, axis=0))
        d = max(abs(a - b) for a, b in zip(got, target))
        check(f"color:{name}", d <= 15, f"target={target} got={got} maxΔ={d}")

    # パネル外周: 暗色50%規則 / Panel edges via the dark-50% rule
    dark = im.max(axis=2) < 110
    for name, (xr, yr) in [("inv-panel", ((100, 1180), (240, 1520))),
                           ("craft-panel", ((1180, 2120), (270, 1450))),
                           ("recipe-panel", ((2130, 3180), (240, 1520)))]:
        zone = dark[yr[0]:yr[1], xr[0]:xr[1]]
        cols = np.where(zone.mean(axis=0) > 0.5)[0]
        rows = np.where(zone.mean(axis=1) > 0.5)[0]
        got = (cols[0] + xr[0], rows[0] + yr[0], cols[-1] + xr[0], rows[-1] + yr[0]) if len(cols) and len(rows) else None
        check_bbox(name, got)

    # 持ち物1行目: 白スロット列で起点とピッチ / Inventory row-1 slot origin+pitch via white columns
    grid = (im[430:1270, 150:1120].min(axis=2) > 200).mean(axis=0)
    cols = runs_of(grid, 60, 0.10)
    if len(cols) >= 4:
        starts = [a + 150 for a, _ in cols]
        pitch = (starts[-1] - starts[0]) / (len(starts) - 1)
        check("inv-grid-x0", abs(starts[0] - 232) <= 4, f"target≈232 got={starts[0]} (slot face left)")
        check("inv-grid-pitch", abs(pitch - 140) <= 2, f"target=140 got={pitch:.1f} n={len(starts)}")
    else:
        check("inv-grid-pitch", False, f"columns not detected ({len(cols)} runs)")

    # ホットバー: 非オレンジマスクのランで幅・ピッチ・起点 / Hotbar width/pitch/origin via non-orange runs
    nb = ~((R > G + 25) & (G > B + 10) & (R > 90))
    hot = nb[1704:1836, 600:2600].mean(axis=0)
    hcols = [(a + 600, b + 600) for a, b in runs_of(hot, 40, 0.4)]
    if len(hcols) >= 3:
        widths = [b - a for a, b in hcols]
        pitch = (hcols[-1][0] - hcols[0][0]) / (len(hcols) - 1)
        check("hotbar-x0", abs(hcols[0][0] - 994) <= 3, f"target=994 got={hcols[0][0]}")
        check("hotbar-width", abs(np.mean(widths) - 132) <= 2, f"target=132 got={np.mean(widths):.1f}")
        check("hotbar-pitch", abs(pitch - 145) <= 2, f"target=145 got={pitch:.1f} n={len(hcols)}")
    else:
        check("hotbar-pitch", False, f"white slots not detected ({len(hcols)} runs)")

    # ヘッダー罫線: 上線と下線のy / Header rules: top/bottom line y positions
    for label, xr in [("inv", (210, 1090)), ("recipe", (2220, 3060))]:
        for rule, (y0, y1), ty in [("top", (300, 335), 316), ("bottom", (365, 400), 382)]:
            band = im[y0:y1, xr[0]:xr[1]].mean(axis=(1, 2))
            peak_y = int(np.argmax(band)) + y0
            base = float(np.median(band))
            strong = band.max() - base >= 5
            ok = strong and abs(peak_y - ty) <= 3
            check(f"rule:{label}-{rule}", ok, f"target y={ty} got y={peak_y} strength={band.max() - base:.1f}(>=5)")

    # シアン系・紺系・白系の要素bbox / Cyan/navy/white element bboxes
    def zone_mask(cond, x0, y0, x1, y1):
        z = np.zeros(im.shape[:2], bool); z[y0:y1, x0:x1] = True
        return cond & z

    check_bbox("selection-frame", bbox_of(zone_mask((B > 150) & (B > R + 30), 1200, 480, 2100, 700)))
    check_bbox("tree-button", bbox_of(zone_mask((B > 140) & (B > R + 30), 1400, 400, 1900, 480)))
    check_bbox("craft-button", bbox_of(zone_mask((B > 140) & (B > R + 30), 1400, 1280, 1900, 1380)))
    check_bbox("craft-tab", bbox_of(zone_mask(im.max(axis=2) < 120, 1150, 215, 1700, 298)))
    check_bbox("sort-button", bbox_of(zone_mask((R < 90) & (G < 90) & (B < 120) & (B >= R), 2900, 10, 3270, 130)))
    check_bbox("key-hints", bbox_of(zone_mask(im.min(axis=2) > 170, 0, 1620, 1000, 1830)))
    check_bbox("hotbar-ring", bbox_of(zone_mask((B > 150) & (G > 120) & (B > R + 30), 950, 1680, 1180, 1844)))
    check_bbox("scroll-knob", bbox_of(zone_mask(im.min(axis=2) > 150, 3060, 400, 3140, 1400)))

    ok_count = sum(1 for _, ok, _ in results if ok)
    print(f"\n== {ok_count}/{len(results)} checks passed ==")
    return 0 if ok_count == len(results) else 1


if __name__ == "__main__":
    sys.exit(main())

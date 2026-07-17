#!/usr/bin/env python3
# parts-eval-criteria.md の[実測]数値軸を機械判定するチェッカー。正本目標値は基準ドキュメントが正
# Mechanical checker for the [実測] numeric axes; the criteria doc is the authority for targets
# 使い方 / Usage: python3 e2e/parity-check.py --cur out/current.png

import argparse
import sys

import numpy as np
from PIL import Image

from parity_targets import BBOX_TARGETS, COLOR_POINTS

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

    # 持ち物3段目(空行)の枠線ピークで面幅と間隔 / Inventory face width + gap via empty-row border peaks
    prof = im[750:850, 180:1100].mean(axis=(0, 2))
    base = float(np.median(prof))
    peaks = [i + 180 for i in range(1, len(prof) - 1) if prof[i] > base + 6 and prof[i] >= prof[i - 1] and prof[i] >= prof[i + 1]]
    merged = []
    for p in peaks:
        if merged and p - merged[-1][-1] <= 6: merged[-1].append(p)
        else: merged.append([p])
    centers = [int(np.mean(g)) for g in merged]
    diffs = [centers[i + 1] - centers[i] for i in range(len(centers) - 1)]
    faces = [d for d in diffs if 100 < d < 135]
    gaps = [d for d in diffs if 8 < d < 40]
    if faces and gaps:
        fw, gp = float(np.median(faces)), float(np.median(gaps))
        check("inv-slot-face", abs(fw - 123) <= 2, f"target=123 got={fw:.0f} (border-to-border)")
        check("inv-slot-gap", abs(gp - 16) <= 2, f"target=16 got={gp:.0f}")
    else:
        check("inv-slot-face", False, f"borders not detected (diffs={diffs})")

    # 持ち物格子の上端: 1行目白面の最初の行 / Inventory grid top via first white-face row
    toprow = (im[390:520, 240:1050].min(axis=2) > 200).mean(axis=1)
    trows = [i + 390 for i, v in enumerate(toprow) if v > 0.10]
    check("inv-grid-top", bool(trows) and abs(trows[0] - 438) <= 4,
          f"target≈438 got={trows[0] if trows else None} (row-1 face top)")

    # レシピ格子: 白面ラン列で列2起点とピッチ / Recipe grid col-2 origin + pitch via bright-face runs
    rgrid = (im[440:1390, 2150:3120].min(axis=2) > 180).mean(axis=0)
    rcols = [(a + 2150, b + 2150) for a, b in runs_of(rgrid, 60, 0.10)]
    rstarts = [a for a, _ in rcols]
    near_col2 = [s for s in rstarts if abs(s - 2372) <= 60]
    check("recipe-grid-col2", bool(near_col2) and abs(near_col2[0] - 2372) <= 3,
          f"target=2372 got={near_col2[0] if near_col2 else rstarts[:3]}")
    if len(rstarts) > 1:
        gapsr = [rstarts[i + 1] - rstarts[i] for i in range(len(rstarts) - 1)]
        pitchr = min(g % 140 if g % 140 <= 70 else 140 - g % 140 for g in gapsr)
        check("recipe-grid-pitch", pitchr <= 2, f"pitch mod140 dev={pitchr} starts={rstarts}")

    # レシピ段数: セル面(白or中間灰)の行帯が7つあるか / Recipe row count via cell-face row bands
    face = (im.min(axis=2) > 180) | ((abs(R - G) < 18) & (abs(G - B) < 18) & (im.max(axis=2) > 90) & (im.max(axis=2) < 150))
    rprof = face[400:1450, 2225:3050].mean(axis=1)
    rowbands = runs_of(rprof, 60, 0.10)
    first_top = rowbands[0][0] + 400 if rowbands else None
    check("recipe-rows", len(rowbands) == 7, f"target=7 rows got={len(rowbands)} bands={[(a + 400, b + 400) for a, b in rowbands]}")
    check("recipe-grid-top", first_top is not None and abs(first_top - 438) <= 4,
          f"target≈438 got={first_top} (row-1 face top)")

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
    check_bbox("craft-arrow-time", bbox_of(zone_mask(im.min(axis=2) > 190, 1520, 510, 1790, 660)))

    ok_count = sum(1 for _, ok, _ in results if ok)
    print(f"\n== {ok_count}/{len(results)} checks passed ==")
    return 0 if ok_count == len(results) else 1


if __name__ == "__main__":
    sys.exit(main())

# parity-check.py の目標値テーブル。値の正本は docs/webui-parity/parts-eval-criteria.md
# Target tables for parity-check.py; docs/webui-parity/parts-eval-criteria.md is the authority

# 色ピック表（基準§2.2）: 名前, x, y, 期待RGB / Color-pick table (criteria §2.2)
# UI面の期待値は2026-07-18にuGUI原画像スポイト値(原色×実アルファのブラウザ合成)へ更新。
# 旧値は正本スクショの橙世界+URPポストプロセス込みの合成色で、UIパーツ原色と色相がズレていた
# UI-face targets were updated on 2026-07-18 to the uGUI-source eyedropped values (source color × real
# alpha, browser-blended). The old values were the reference screenshot's blend including the orange world
# and URP post-processing, whose hue drifted from the actual UI part colors
COLOR_POINTS = [
    ("bg-top", 1635, 100, (143, 120, 96)), ("bg-left", 40, 900, (139, 95, 49)),
    ("bg-bottom", 1600, 1550, (129, 74, 35)), ("bg-hints", 500, 1700, (127, 69, 32)),
    ("inv-header", 600, 330, (36, 34, 38)), ("inv-bottom", 600, 1350, (46, 42, 50)),
    ("inv-empty", 700, 790, (43, 41, 50)), ("inv-white", 238, 450, (254, 254, 254)),
    ("craft-top", 1350, 455, (34, 29, 29)), ("craft-mid", 1650, 700, (34, 27, 26)),
    ("craft-low1", 1650, 900, (34, 26, 24)), ("craft-low2", 1650, 1250, (33, 24, 22)),
    ("sel-row", 1650, 500, (37, 41, 54)),
    ("rec-header", 2600, 330, (36, 34, 38)), ("rec-bottom", 2600, 1450, (57, 39, 31)),
    ("rec-gray", 2232, 452, (124, 126, 136)), ("rec-white", 2820, 740, (255, 255, 255)),
    ("hb-white", 1450, 1740, (253, 253, 253)), ("hb-bg", 1450, 1625, (128, 71, 33)),
]

# bbox目標（基準1章・[実測]値）: 名前 -> ((l,t,r,b), 許容px) / bbox targets from criteria ch.1
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
    "craft-arrow-time": ((1572, 530, 1693, 651), 5),
}

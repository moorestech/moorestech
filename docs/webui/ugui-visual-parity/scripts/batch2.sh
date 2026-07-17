#!/bin/bash
# batch2: sample(色)/fade(線端)/alpha(透過)/再計測(汚染box修正)
set -u
P="python3 /Users/katsumi/.claude/skills/run-eval-loop/scripts/visual-probe.py"
T=/Users/katsumi/moorestech-worktrees/tree3/.mso/sessions/f2277f3f-2541-4bde-8ce2-a9e31d6cede5/turns
REF="/Users/katsumi/Desktop/スクショ/inventory.png"
CUR="$T/iter0-current.png"

s() { $P sample --img "$1" ${2:+--at "$2"} ${3:+--box "$3"} > "$T/probe/sample-$4.txt" 2>&1; }
fd() { $P fade --img "$1" --box "$2" --axis "$3" > "$T/probe/fade-$4.txt" 2>&1; $P crop --img "$1" --box "$2" --margin 0.8 --draw-box "$2" --out "$T/probe/receipt-fade-$4.png" >/dev/null 2>&1; }
pf() { $P profile --img "$1" --box "$2" --axis "$3" > "$T/probe/profile-$4.txt" 2>&1; $P crop --img "$1" --box "$2" --margin 0.8 --draw-box "$2" --out "$T/probe/receipt-profile-$4.png" >/dev/null 2>&1; }
ft() { $P fit --img "$1" --box "$2" --axis row > "$T/probe/fit-$3-row.txt" 2>&1; $P fit --img "$1" --box "$2" --axis col > "$T/probe/fit-$3-col.txt" 2>&1; $P crop --img "$1" --box "$2" --margin 0.4 --draw-box "$2" --out "$T/probe/receipt-fit-$3.png" >/dev/null 2>&1; }

# ==== 再計測（box汚染の修正）
ft "$CUR" "2.5,10.5,34,71" panel-inv-cur2
ft "$CUR" "52.6,85.3,6.6,10.5" hotbar-slot-cur2
ft "$CUR" "46.8,31.2,9,5.6" arrow-cur2
ft "$REF" "32.5,88.1,1.8,2.4" hotbar-tag-ref2
ft "$CUR" "32.3,84.2,2.4,2.8" hotbar-tag-cur2
ft "$REF" "64,14,33,66" panel-recipe-ref2
pf "$REF" "35.6,45,5.2,2" col panel-center-edge-ref2
pf "$CUR" "48,74.5,4,4" row panel-center-bottom-cur2
pf "$REF" "20,15.8,10,6.2" row header-inv-ref2

# ==== fade（線の端の減衰）
fd "$REF" "8.6,16.57,23.8,0.8" col header-inv-line-above-ref
fd "$REF" "8.6,20.11,23.8,0.8" col header-inv-line-below-ref
fd "$CUR" "7.8,20.9,25.5,1.2" col header-inv-lines-cur
fd "$REF" "67.9,16.65,27,0.8" col header-recipe-line-above-ref
fd "$REF" "67.9,20.17,27,0.8" col header-recipe-line-below-ref
fd "$CUR" "69.4,20.85,28.5,1.25" col header-recipe-lines-cur
fd "$REF" "43.5,21.35,13.5,0.85" col ornament-line-ref
fd "$CUR" "38.5,25.3,25,1.3" col ornament-lines-cur
fd "$REF" "40,74.72,20,0.85" col panel-center-bottomline-ref

# ==== 端の処理エビデンス（小要素の断面）
pf "$REF" "61.7,73.7,1.8,1.4" col tri-grip-edge-ref
pf "$REF" "44.2,23.8,2.2,1" col btn-tree-edge-ref
pf "$CUR" "43.9,21.6,2.2,1" col btn-tree-edge-cur
pf "$REF" "47.4,30.8,2.2,1" col arrow-edge-ref
pf "$CUR" "48.2,33.8,2.2,1" col arrow-edge-cur
pf "$REF" "47.6,33.7,1.8,1" col timetext-edge-ref
pf "$CUR" "48.3,41.9,1.8,1" col timetext-edge-cur
pf "$REF" "38,50,3,2" col preview-edge-ref
pf "$CUR" "37.5,55,3,2" col preview-edge-cur
pf "$REF" "93.7,35,1.6,2" col scrollbar-edge-ref
pf "$REF" "32.4,89.2,2.0,0.8" col hotbar-tag-edge-ref
pf "$CUR" "32.2,85.4,2.6,0.8" col hotbar-tag-edge-cur
pf "$REF" "30.8,92.5,1.8,1.2" col hotbar-sel-edge-ref
pf "$CUR" "30.1,90,2.0,1.2" col hotbar-sel-edge-cur
pf "$REF" "0.8,89.0,3,0.9" col keyhints-edge-ref
pf "$CUR" "0.8,89.0,3,0.9" col keyhints-edge-cur
pf "$REF" "90.3,4.0,2.2,1" col btn-sort-edge-ref
pf "$CUR" "93.3,3.8,2.2,1.4" col btn-sort-edge-cur
pf "$REF" "36.3,14,2.0,1.2" col tab-top-edge-ref
pf "$CUR" "47.8,13,2.0,1.2" col tab-top-edge-cur

# ==== sample（色）
s "$REF" "" "20,17.5,3,2" panel-inv-top-ref
s "$REF" "" "20,55,3,3" panel-inv-bottom-ref
s "$REF" "" "20,10.5,3,1.5" bg-grass-ref
s "$REF" "" "2.5,55,2,2" bg-shadowbrick-ref
s "$REF" "" "20,70,3,2" bg-brick-below-ref
s "$CUR" "" "20,17.5,3,2" panel-inv-top-cur
s "$CUR" "" "20,70,3,3" panel-inv-bottom-cur
s "$CUR" "" "20,9,3,1.5" bg-grass-cur
s "$CUR" "" "20,83,3,2" bg-brick-below-cur
s "$REF" "" "44,18.5,2,1.5" panel-center-top-ref
s "$REF" "" "55,66,3,2" panel-center-bottom-ref
s "$REF" "" "50,9,4,2" bg-grass-center-ref
s "$REF" "" "50,80,4,2" bg-brick-center-ref
s "$CUR" "" "40,17,2,1.5" panel-center-top-cur
s "$CUR" "" "55,65,3,2" panel-center-bottom-cur
s "$CUR" "" "50,8.5,4,2" bg-grass-center-cur
s "$CUR" "" "50,83,4,2" bg-brick-center-cur
s "$REF" "20,16.97;25,16.97;30,16.97" "" header-inv-lineabove-ref
s "$REF" "20,20.51;25,20.51;30,20.51" "" header-inv-linebelow-ref
s "$CUR" "20,21.19;25,21.19;20,21.71;25,21.71" "" header-inv-lines-cur
s "$REF" "45,21.74;55.5,21.74" "" ornament-line-ref
s "$REF" "" "49.8,21.2,0.9,1.3" ornament-diamond-ref
s "$REF" "" "47.2,21.4,1.5,0.9" ornament-dagger-ref
s "$CUR" "45,25.68;45,26.23;58,25.68" "" ornament-lines-cur
s "$CUR" "" "50.8,25.2,1.2,1.6" ornament-diamond-cur
s "$REF" "49.6,23.2;49.6,24.0;49.6,24.9" "" btn-tree-ref
s "$CUR" "50.5,21.4;50.5,22.6;50.5,23.8" "" btn-tree-cur
s "$REF" "38.5,30;38.5,32" "" sel-frame-line-ref
s "$REF" "" "38.4,26.1,0.8,0.7" sel-frame-corner-ref
s "$REF" "38.0,30;37.8,31" "" sel-frame-glow-ref
s "$CUR" "37.84,32;37.84,34" "" sel-frame-line-cur
s "$CUR" "" "37.8,28.4,0.8,0.7" sel-frame-corner-cur
s "$CUR" "37.2,32;37.0,33" "" sel-frame-glow-cur
s "$REF" "" "49.5,30.5,1.5,1.5" arrow-body-ref
s "$CUR" "" "50.5,33.9,1.5,1.0" arrow-body-cur
s "$REF" "49.6,70.35;49.6,71.5;49.6,72.65" "" btn-craft-ref
s "$CUR" "50,69.0;50,71.3;50,73.8" "" btn-craft-cur
s "$REF" "" "62.0,74.15,0.7,0.7" tri-grip-ref
s "$REF" "7.06,49.0" "" slot-empty-border-ref
s "$REF" "" "8.9,48.5,1.5,2" slot-empty-fill-ref
s "$CUR" "6.42,65.2" "" slot-empty-border-cur
s "$CUR" "" "8.5,64.8,1.5,2" slot-empty-fill-cur
s "$REF" "" "8.7,25.5,1.5,2" slot-filled-plate-ref
s "$CUR" "" "8.4,26.3,1.5,2" slot-filled-icon-cur
s "$REF" "" "9.55,28.4,1.0,1.1" slot-count-ref
s "$CUR" "" "9.4,29.3,1.0,1.1" slot-count-cur
s "$REF" "" "69.6,25.5,1.5,2" slot-recipe-plate-ref
s "$CUR" "" "70.9,26.2,1.5,2" slot-recipe-icon-cur
s "$REF" "50.48,93.0" "" hotbar-slot-border-ref
s "$REF" "" "51.6,92.5,1.5,2" hotbar-slot-fill-ref
s "$CUR" "" "55.5,90,1.5,2" hotbar-slot-fill-cur
s "$REF" "" "33.05,89.0,0.5,0.6" hotbar-tag-bg-ref
s "$REF" "32.84,89.3;33.28,89.2" "" hotbar-tag-points-ref
s "$CUR" "" "33.3,85.5,0.6,0.7" hotbar-tag-bg-cur
s "$CUR" "32.9,85.6;33.5,85.5" "" hotbar-tag-points-cur
s "$REF" "" "32.8,92.5,1.5,2" hotbar-sel-fill-ref
s "$CUR" "30.85,90.5;30.9,92" "" hotbar-sel-ring-cur
s "$REF" "94.35,30;94.35,40" "" scrollbar-ref
s "$REF" "" "1.5,88.3,8,1.8" keyhints-ref
s "$CUR" "" "1.5,88.3,8,1.8" keyhints-cur
s "$REF" "" "93.5,3.5,2,1.5" btn-sort-ref
s "$CUR" "" "95.5,3.2,2,1.5" btn-sort-cur
s "$REF" "" "38.5,14,2,1.5" tab-top-ref
s "$CUR" "" "50.2,12.8,2,1.5" tab-top-cur
s "$REF" "" "49.3,33.5,2,1.2" timetext-ref
s "$CUR" "" "49.8,41.8,2,1.2" timetext-cur
s "$REF" "" "45,50,8,8" preview-ref
s "$CUR" "" "45,52,8,8" preview-cur

echo BATCH2_DONE; ls "$T/probe" | wc -l
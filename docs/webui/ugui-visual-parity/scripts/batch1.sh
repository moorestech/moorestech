#!/bin/bash
# batch1: fit(row/col)+receipt, edge/band profile+receipt, corner, element crop pair
# 全出力は probe/ と crops/ に保存し matrix の根拠列から引用する
set -u
P="python3 /Users/katsumi/.claude/skills/run-eval-loop/scripts/visual-probe.py"
T=/Users/katsumi/moorestech-worktrees/tree3/.mso/sessions/f2277f3f-2541-4bde-8ce2-a9e31d6cede5/turns
REF="/Users/katsumi/Desktop/スクショ/inventory.png"
CUR="$T/iter0-current.png"

img() { [ "$1" = ref ] && echo "$REF" || echo "$CUR"; }

dofit() { # side elem box
  local i; i=$(img "$1")
  $P fit --img "$i" --box "$3" --axis row > "$T/probe/fit-$2-$1-row.txt" 2>&1
  $P fit --img "$i" --box "$3" --axis col > "$T/probe/fit-$2-$1-col.txt" 2>&1
  $P crop --img "$i" --box "$3" --margin 0.4 --draw-box "$3" --out "$T/probe/receipt-fit-$2-$1.png" >/dev/null 2>&1
}
doprof() { # side elem box axis
  local i; i=$(img "$1")
  $P profile --img "$i" --box "$3" --axis "$4" > "$T/probe/profile-$2-$1.txt" 2>&1
  $P crop --img "$i" --box "$3" --margin 0.6 --draw-box "$3" --out "$T/probe/receipt-profile-$2-$1.png" >/dev/null 2>&1
}
doprof2() { # 2本目の断面 side elem box axis (別名)
  local i; i=$(img "$1")
  $P profile --img "$i" --box "$3" --axis "$4" > "$T/probe/profile-$2-$1.txt" 2>&1
  $P crop --img "$i" --box "$3" --margin 0.6 --draw-box "$3" --out "$T/probe/receipt-profile-$2-$1.png" >/dev/null 2>&1
}
docorner() { # side elem box corner
  local i; i=$(img "$1")
  $P corner --img "$i" --box "$3" --corner "$4" > "$T/probe/corner-$2-$1.txt" 2>&1
}
docrop() { # side elem box
  local i; i=$(img "$1")
  $P crop --img "$i" --box "$3" --out "$T/crops/$2-$1.png" >/dev/null 2>&1
}

# ---- 持ち物パネル
dofit ref panel-inv "5,13,30,57";      dofit cur panel-inv "4,12.5,31,67"
doprof ref panel-inv-edge "4.5,40,4.5,2" col;  doprof cur panel-inv-edge "3.5,40,4.5,2" col
docorner ref panel-inv "5.3,13.8,3.5,4.5" tl;  docorner cur panel-inv "3.7,12.7,3.4,4" tl
docrop ref panel-inv "6,15,6,6";       docrop cur panel-inv "4,13,6,6"
# ---- 持ち物見出しブロック
dofit ref header-inv "8,15,25,7";      dofit cur header-inv "7,15.5,27,8"
doprof ref header-inv "15,15.0,14,7.0" row;    doprof cur header-inv "15,15.5,14,8" row
docrop ref header-inv "8,15,26,7";     docrop cur header-inv "7,15.5,27,8"
# ---- メイン充填スロット
dofit ref slot-filled "6.7,22.3,4.6,7.6";  dofit cur slot-filled "6.2,23,4.8,7.8"
doprof ref slot-filled-edge "5.9,25.5,2.4,1.2" col;  doprof cur slot-filled-edge "5.4,26,2.4,1.4" col
docorner ref slot-filled "6.7,22.4,1.6,2.2" tl;  docorner cur slot-filled "6.2,23.2,1.8,2.4" tl
docrop ref slot-filled "6.5,22,5,8.2";  docrop cur slot-filled "6,22.7,5.2,8.4"
# ---- メイン空スロット
dofit ref slot-empty "6.7,45.3,4.6,7.8";  dofit cur slot-empty "6.2,61.5,4.8,8"
doprof ref slot-empty-edge "5.9,48.5,2.4,1.2" col;  doprof cur slot-empty-edge "5.4,64.5,2.4,1.4" col
docorner ref slot-empty "6.7,45.5,1.6,2.2" tl;  docorner cur slot-empty "6.2,61.7,1.8,2.4" tl
docrop ref slot-empty "6.5,45,5,8.4";   docrop cur slot-empty "6,61.2,5.2,8.6"
# ---- 中央パネル
dofit ref panel-center "36,12.5,29,65";  dofit cur panel-center "35,12.3,31,65"
doprof ref panel-center-edge "35.9,45,3,2" col;  doprof cur panel-center-edge "35.2,45,3,2" col
doprof2 ref panel-center-bottom "48,73.3,4,3.5" row;  doprof2 cur panel-center-bottom "48,73,4,4.5" row
docorner ref panel-center "36.1,12.6,2.8,3.2" tl;  docorner cur panel-center "35.3,12.5,2.8,3.2" tl
docrop ref panel-center "36,12.5,5,5";  docrop cur panel-center "35,12.3,5,5"
# ---- 中央パネル上部タブ
dofit ref tab-top "36.8,11.3,6.2,5.8";  dofit cur tab-top "48.4,10.5,5.2,6"
docrop ref tab-top "36.8,11.3,6.2,5.8"; docrop cur tab-top "48.4,10.5,5.2,6"
# ---- 中央見出しブロック
dofit ref header-center "43.5,16,14,9";  dofit cur header-center "46,16,12,5"
doprof ref header-center "54.6,16.8,1.7,7.6" row;  doprof cur header-center "40,15.8,2,11.5" row
docrop ref header-center "43.5,16,14,9";  docrop cur header-center "44,15.5,14,12"
# ---- 見出しオーナメント装飾
dofit ref ornament "43.5,21.5,13.5,2.8";  dofit cur ornament "38.5,24.6,25,2.6"
docrop ref ornament "43.5,21.3,14,3.2";   docrop cur ornament "38,24.2,26,3.4"
# ---- レシピツリーボタン
dofit ref btn-tree "44.8,22.3,10.4,3.9";  dofit cur btn-tree "44.6,19.9,12.4,4.4"
docorner ref btn-tree "45.3,22.6,1.4,1.8" tl;  docorner cur btn-tree "45,20.1,1.5,1.8" tl
docrop ref btn-tree "44.8,22.3,10.4,3.9"; docrop cur btn-tree "44.6,19.9,12.4,4.4"
# ---- 選択レシピ枠
dofit ref sel-frame "37.3,25.2,26,11.6";  dofit cur sel-frame "36.6,27.6,28,12.4"
doprof ref sel-frame-edge "37.4,30,2.2,1.5" col;  doprof cur sel-frame-edge "36.7,32,2.2,1.5" col
docorner ref sel-frame "37.6,25.5,2.6,3.0" tl;  docorner cur sel-frame "36.9,27.9,2.6,3.0" tl
docrop ref sel-frame "37.4,25.3,3.2,3.6";  docrop cur sel-frame "36.7,27.7,3.2,3.4"
docrop ref sel-frame-tr "60.2,25.3,3.2,3.6";  docrop cur sel-frame-tr "62,27.7,3.2,3.4"
# ---- 進行矢印
dofit ref arrow "46.8,27.3,6.4,7.4";  dofit cur arrow "47.8,32.4,6.9,3.3"
docrop ref arrow "46.8,27.3,6.4,7.4"; docrop cur arrow "47.8,32.2,7,3.6"
# ---- 所要時間テキスト
dofit ref timetext "48,32.6,4.6,3.2";  dofit cur timetext "48.7,40.8,4.2,3"
docrop ref timetext "47.5,32,5.6,4.2"; docrop cur timetext "48.2,40.2,5.2,4.2"
# ---- 中央プレビュー領域
dofit ref preview "38.5,37,24,32";  dofit cur preview "37,43.5,28,24"
docrop ref preview "45,45,10,12";   docrop cur preview "45,50,10,12"
# ---- CRAFTボタン
dofit ref btn-craft "45,69.5,10.8,4.5";  dofit cur btn-craft "36.5,68.3,28.5,6.5"
doprof ref btn-craft "49.4,69.6,1.2,4.3" row;  doprof cur btn-craft "49.4,68.5,1.2,6.2" row
docorner ref btn-craft "45.4,70.2,1.4,1.6" tl;  docorner cur btn-craft "37,68.9,1.6,2.0" tl
docrop ref btn-craft "44.8,69.3,11.2,4.9"; docrop cur btn-craft "36.3,68,29,7.2"
# ---- 三角グリップ装飾
dofit ref tri-grip "60.3,72.3,3.2,3.4"
docrop ref tri-grip "60.3,72.2,3.4,3.8"; docrop cur tri-grip "61.8,72.5,3.4,3.6"
# ---- CRAFT RECIPEパネル
dofit ref panel-recipe "65.5,15,31,63";  dofit cur panel-recipe "66.5,12.5,32.5,69.5"
doprof ref panel-recipe-edge "64.9,45,3.4,2" col;  doprof cur panel-recipe-edge "66.3,45,3.2,2" col
docorner ref panel-recipe "65.7,14.8,3,3.5" tl;  docorner cur panel-recipe "66.9,12.7,3.2,3.6" tl
docrop ref panel-recipe "66,15.5,6,6";  docrop cur panel-recipe "66.5,12.5,6,6"
# ---- CRAFT RECIPE見出しブロック
dofit ref header-recipe "67,16,29,7";  dofit cur header-recipe "68.5,16,30,7"
doprof ref header-recipe "85,16.4,6,6.0" row;  doprof cur header-recipe "88,16.2,5,6.5" row
docrop ref header-recipe "67,16,29,7";  docrop cur header-recipe "68.5,16,30,7"
# ---- レシピグリッドスロット
dofit ref slot-recipe "67.6,22.4,4.4,7.2";  dofit cur slot-recipe "69.2,23,5,7.8"
doprof ref slot-recipe-edge "66.9,25.5,2.4,1.2" col;  doprof cur slot-recipe-edge "68.6,26,2.2,1.4" col
docorner ref slot-recipe "67.7,22.5,1.6,2.2" tl;  docorner cur slot-recipe "69.3,23.2,1.8,2.4" tl
docrop ref slot-recipe "67.4,22.1,4.8,7.8";  docrop cur slot-recipe "69,22.7,5.4,8.4"
# ---- スクロールバー
dofit ref scrollbar "93.6,26,2.2,25"
docrop ref scrollbar "93.4,25.5,2.8,26";  docrop cur scrollbar "95.6,25,2.8,26"
# ---- ホットバースロット (slot6=空)
dofit ref hotbar-slot "49.9,90.2,4.6,6.0";  dofit cur hotbar-slot "53.3,86.6,5.2,8"
doprof ref hotbar-slot-edge "49.5,92.8,2.0,1.4" col;  doprof cur hotbar-slot-edge "52.9,89.5,2.2,1.5" col
docorner ref hotbar-slot "50.2,90.7,1.5,2.0" tl;  docorner cur hotbar-slot "53.5,86.9,1.8,2.2" tl
docrop ref hotbar-slot "49.7,89.9,5,6.6";  docrop cur hotbar-slot "53,86.2,5.8,8.6"
# ---- ホットバー番号タグ (tag1)
dofit ref hotbar-tag "32.2,87.9,2.6,3.2";  dofit cur hotbar-tag "32.2,83.2,2.8,3.6"
docrop ref hotbar-tag "32.2,87.7,2.6,3.6";  docrop cur hotbar-tag "32.2,83.0,2.8,4.0"
# ---- ホットバー選択表示 (slot1)
dofit ref hotbar-sel "31.2,90.3,4.0,6.0";  dofit cur hotbar-sel "30.7,86.4,5.2,8"
docrop ref hotbar-sel "31.0,89.9,4.6,6.8";  docrop cur hotbar-sel "30.4,86.0,5.8,8.8"
# ---- キーヒントテキスト
dofit ref keyhints "0.5,87,31,10.5";  dofit cur keyhints "0.5,87,28,10.5"
docrop ref keyhints "0.5,87,31,10.5"; docrop cur keyhints "0.5,87,28,10.5"
# ---- 整理ボタン
dofit ref btn-sort "90.8,1.8,9,5";  dofit cur btn-sort "94,1.8,5.5,4.8"
docrop ref btn-sort "90.8,1.8,9,5"; docrop cur btn-sort "93.8,1.5,6,5.5"

echo BATCH1_DONE; ls "$T/probe" | wc -l; ls "$T/crops" | wc -l

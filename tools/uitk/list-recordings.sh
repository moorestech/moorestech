#!/usr/bin/env bash
# 全録画の一覧を作る。切替後は描く系が UITK だけなので、scenarios 配下の全 .cs を流す
# 冒頭に「現行masterでは動かない」の印を持つシナリオは、マスタの変更で壊れたまま（bd moorestech-q3ei）なので一覧から外し、名前を標準エラーへ出す
# Lists every recording; after the cutover UITK is the only drawing system, so every .cs under scenarios runs
# Scenarios whose header carries the "broken on current master" marker stay broken by master changes (bd moorestech-q3ei), so they are left out and named on stderr
set -euo pipefail
if [ $# -ne 0 ]; then
  echo "usage: $0" >&2
  exit 2
fi
root=.agents/skills/unity-playmode-recorded-playtest/scenarios
marker='現行masterでは動かない'
find "$root" -name '*.cs' | sort | while IFS= read -r scenario; do
  if awk -v m="$marker" 'NR <= 5 && index($0, m) { found = 1 } END { exit !found }' "$scenario"; then
    echo "excluded (broken on current master, bd moorestech-q3ei): $scenario" >&2
    continue
  fi
  echo "$scenario"
done

#!/usr/bin/env bash
# 一覧のシナリオを開始行から N 本だけ winunity play で順に流し、1本1行で TSV に追記する（GUI 枠を長く握らないよう小分けにする）
# Runs N listed scenarios from a start line via winunity play and appends one TSV row each (chunked so the GUI slot is not held long)
set -u
if [ $# -ne 6 ]; then
  echo "usage: $0 <GameUi|Web> <branch> <list.txt> <out.tsv> <start-line> <count>" >&2
  exit 2
fi
system=$1; branch=$2; list=$3; out=$4; start=$5; count=$6
case "$system" in GameUi|Web) ;; *) echo "system は GameUi か Web" >&2; exit 2;; esac
logs="$(dirname "$out")/logs"
mkdir -p "$logs"
[ -f "$out" ] || printf "scenario\tsystem\texit\tseconds\trun_dir\n" > "$out"

tail -n +"$start" "$list" | head -n "$count" | while IFS= read -r scenario; do
  [ -z "$scenario" ] && continue

  # 1 本流す。系の切替と Current の確認は winunity がする。ssh が一覧を読まないよう標準入力を切る
  # Run one scenario; winunity sets the system and checks Current. Detach stdin so ssh does not eat the list
  began=$(date +%s)
  log="$logs/$(basename "$scenario" .cs)-$system.log"
  "$HOME/bin/winunity" play "$branch" "$scenario" --ui-system "$system" > "$log" 2>&1 < /dev/null
  code=$?

  # 結果の置き場を拾い、summary.json の終了コードで上書きする（summary.json は BOM 付きなので utf-8-sig で読む）
  # Pick up the result dir and take the exit code from summary.json (it carries a BOM, so read as utf-8-sig)
  dir=$(grep -o '\[mac\] results: .*' "$log" | tail -1 | sed 's/^\[mac\] results: //')
  # silent-ok: 置き場が無いときは winunity の終了コードのまま run_dir を - にする / Step 3 で TSV の - の行を log から読んで気づく
  if [ -n "$dir" ] && [ -f "$dir/summary.json" ]; then
    code=$(python3 -c 'import json,sys; d=json.load(open(sys.argv[1],encoding="utf-8-sig")); print(d["detail"]["exitCode"] if "exitCode" in (d.get("detail") or {}) else (0 if d["ok"] else 1))' "$dir/summary.json")
  fi
  printf "%s\t%s\t%s\t%s\t%s\n" "$scenario" "$system" "$code" "$(( $(date +%s) - began ))" "${dir:--}" >> "$out"
done

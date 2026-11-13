#!/usr/bin/env bash
# 系ごとの全録画の一覧を作る。wwxy の表のうち X-prerun が直す行と消えた行だけを外し、Web では UITK 専用（scenarios/uitk/）も外す
# Lists recordings per system; drops only wwxy rows owned by X-prerun (or deleted files), and UITK-only scenarios (scenarios/uitk/) for Web
set -eu
if [ $# -ne 1 ]; then
  echo "usage: $0 <GameUi|Web>" >&2
  exit 2
fi
system=$1
case "$system" in GameUi|Web) ;; *) echo "system は GameUi か Web" >&2; exit 2;; esac
root=.agents/skills/unity-playmode-recorded-playtest/scenarios
ledger=docs/uitk/TODO.md

# wwxy の節の表の行から、直すレーンが X-prerun の .cs だけを抜く（S2/S3-ladder が直した行は流す）
# From the wwxy table rows, take only .cs files whose fixing lane is X-prerun (rows fixed by S2/S3-ladder still run)
section=$(awk '/^## 壊れた録画の割り振り/{f=1;next} /^## /{f=0} f' "$ledger")
if [ -z "$section" ]; then
  echo "台帳に wwxy の節が見つからない（$ledger）" >&2
  exit 2
fi
excluded=$(printf '%s\n' "$section" | grep '^| `' | grep 'X-prerun' | grep -o '`[^`]*\.cs`' | tr -d '`' || true)

find "$root" -name '*.cs' | sort | while read -r path; do
  rel=${path#"$root"/}
  name=$(basename "$rel")
  if [ -n "$excluded" ] && printf '%s\n' "$excluded" | grep -qx -e "$rel" -e "$name"; then
    echo "除外(wwxy・X-prerun): $rel" >&2
    continue
  fi
  case "$rel" in uitk/*)
    if [ "$system" = Web ]; then echo "除外(UITK 専用): $rel" >&2; continue; fi
  esac
  echo "$path"
done

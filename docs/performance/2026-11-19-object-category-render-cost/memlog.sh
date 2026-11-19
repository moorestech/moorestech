#!/usr/bin/env bash
# Unity の RSS（MB）・スワップ使用量・空き容量を10秒おきに記録する。空きが 15GB を切ったら Unity の PlayMode を止める
OUT="$1"; PROJ="$(cd "$(dirname "$0")/../../.." && pwd)/moorestech_client"
while true; do
  rss=$(ps -axo rss,command | grep "Unity.app/Contents/MacOS/Unity" | grep -v grep | awk '{s+=$1} END {printf "%d", s/1024}')
  swap=$(sysctl -n vm.swapusage | awk '{print $6}')
  free=$(df -g /System/Volumes/Data | tail -1 | awk '{print $4}')
  echo "$(date +%H:%M:%S) unityRssMB=$rss swapUsed=$swap freeGB=$free" >> "$OUT"
  if [ "${free:-99}" -lt 15 ]; then echo "$(date +%H:%M:%S) LOW DISK: stopping play mode" >> "$OUT"; uloop control-play-mode --action Stop --project-path "$PROJ" >/dev/null 2>&1; fi
  sleep 10
done

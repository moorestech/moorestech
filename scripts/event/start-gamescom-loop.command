#!/bin/bash
# gamescom出展用: イベントモードでゲームを無限ループ起動する（終了＝リセット）
# For the gamescom booth: run the game in event mode in an endless loop (quit = reset)
set -u
cd "$(dirname "$0")"

export MOORESTECH_EVENT_MODE=1
# 無操作タイムアウト秒を変えたい場合はコメントを外す
# Uncomment to override the idle timeout seconds
# export MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS=180

# 引数で.appを指定可能。省略時は同ディレクトリのmoorestech.app
# The .app path can be given as an argument; defaults to moorestech.app beside this script
APP_PATH="${1:-./moorestech.app}"
BINARY="$(find "$APP_PATH/Contents/MacOS" -maxdepth 1 -type f 2>/dev/null | head -n 1)"
if [ -z "$BINARY" ]; then
  echo "app not found: $APP_PATH"
  exit 1
fi

while true; do
  "$BINARY"
  echo "=== game exited, relaunching... (close this window to stop the loop) ==="
  sleep 1
done

#!/usr/bin/env bash
# supervisor の periodic から worker を nohup で切り離す。periodic は同期実行なので長い処理を直接置かない
# Detaches the worker with nohup from the supervisor periodic; periodics run synchronously so long work must not sit here
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
# 既定値はスクリプト自身の位置から導出する（supervisor は HOME を差し替えるため $HOME 基準は不可）
# Defaults derive from the script's own location (supervisor swaps HOME, so a $HOME-based default breaks)
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
LOG="${PLAYTEST_INGEST_LOG:-$REPO/../../services/always-on/logs/playtest-ingest-worker.log}"
mkdir -p "$(dirname "$LOG")"
nohup /bin/bash "$REPO/scripts/playtest/ingest.sh" >>"$LOG" 2>&1 </dev/null &
echo "playtest-ingest dispatched pid=$!"

#!/usr/bin/env bash
# supervisor の periodic から worker を nohup で切り離す。periodic は同期実行なので長い処理を直接置かない
# Detaches the worker with nohup from the supervisor periodic; periodics run synchronously so long work must not sit here
set -euo pipefail
REPO="${MOORESTECH_REPO:-$HOME/hermes-agent/data/repos/moorestech}"
LOG="${PLAYTEST_INGEST_LOG:-$HOME/hermes-agent/data/services/always-on/logs/playtest-ingest-worker.log}"
mkdir -p "$(dirname "$LOG")"
nohup /bin/bash "$REPO/scripts/playtest/ingest.sh" >>"$LOG" 2>&1 </dev/null &
echo "playtest-ingest dispatched pid=$!"

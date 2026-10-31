#!/usr/bin/env bash
# ダッシュボードの起動ラッパー。env.sh を source して受け口の URL と管理キーを環境変数で server.py へ渡す（ingest-dispatch.sh と同じ方式）
# 受け口 URL の既定は receiver-api.sh の1か所だけに置き、ここで export して Python 側に既定を持たせない
# Dashboard launcher: sources env.sh and hands the receiver URL and admin key to server.py as env vars (same as ingest-dispatch.sh)
# The receiver URL default lives only in receiver-api.sh; it is exported here so Python keeps no default of its own
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[dashboard-run] $*" >&2; }
# 既定値はスクリプト自身の位置から導出する（supervisor は HOME を差し替えるため $HOME 基準は不可）
# Defaults derive from the script's own location (supervisor swaps HOME, so a $HOME-based default breaks)
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../../../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# shellcheck disable=SC1090
if [ -f "$ENV_FILE" ]; then
  . "$ENV_FILE"
else
  log "env file が無い（${ENV_FILE}）。PLAYTEST_ADMIN_KEY が環境変数に無ければ R2 中継は 503 になる"
fi
# shellcheck source=../../lib/receiver-api.sh
. "$HERE/../../lib/receiver-api.sh"
export PLAYTEST_RECEIVER_BASE="$RECEIVER_BASE"
export PLAYTEST_ADMIN_KEY="${PLAYTEST_ADMIN_KEY:-}"
exec python3 "$HERE/../server.py" "$@"

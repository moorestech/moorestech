#!/usr/bin/env bash
# 受け口とローカルの報告所在を一度に表示する
# Show receiver and local report locations together
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
if [ -f "$ENV_FILE" ]; then . "$ENV_FILE"; fi
LOGS="${LOGS:-${MOORESTECH_LOGS:-$REPO/../moorestech_logs}}"
export MOORESTECH_PLAYTEST_RAW="${MOORESTECH_PLAYTEST_RAW:-$REPO/../moorestech_playtest_raw}"
limit=10
if [ "$#" -gt 0 ]; then
  [ "$#" -eq 2 ] && [ "$1" = --limit ] && [[ "$2" =~ ^[1-9][0-9]*$ ]] \
    || { echo '使い方: find-reports.sh [--limit N]' >&2; exit 1; }
  limit="$2"
fi
. "$HERE/lib/receiver-api.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
printf '## 受け口の未取り込み\n'
cursor=''
seen=''
if [ -z "${PLAYTEST_ADMIN_KEY:-}" ]; then
  echo '取得失敗: PLAYTEST_ADMIN_KEY が未設定'
else
  # ページ単位の取得失敗でも、ローカルの一覧は続ける
  # Continue local listings even when a receiver page cannot be fetched
  while true; do
    if ! receiver_inbox_page "$cursor" "$WORK/page.json" 2>"$WORK/error"; then
      printf '取得失敗: %s\n' "$(cat "$WORK/error")"
      break
    fi
    if ! python3 "$HERE/lib/reports/find_reports.py" page "$WORK/page.json" "$WORK/cursor"; then
      echo '取得失敗: 受け口の応答が不正'
      break
    fi
    cursor="$(cat "$WORK/cursor")"
    [ -n "$cursor" ] || break
    if printf '%s\n' "$seen" | grep -Fxq -- "$cursor"; then
      echo '取得失敗: cursor が繰り返された'
      break
    fi
    seen="${seen}
${cursor}"
  done
fi
python3 "$HERE/lib/reports/find_reports.py" local "$LOGS" "$limit"

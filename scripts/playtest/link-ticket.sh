#!/usr/bin/env bash
# プレイ報告1件に関連チケット（Notion・GitHub 等の https URL）を紐付け、チケット側へ貼るビューワー URL を出す
# Links a related ticket (Notion, GitHub or any https URL) to one play report and prints the viewer URL to paste into the ticket
# 書き込みはダッシュボードのサーバー（Mac mini で常駐）経由で行う（状態ファイルの書き手を1プロセスに保つため）
# Writes go through the always-on dashboard server so the state file keeps a single writer
set -euo pipefail

if [ "$#" -ne 4 ] && [ "$#" -ne 3 ]; then
  echo "usage: link-ticket.sh <steamId> <reportId> <ticketUrl> <title>   # 紐付け" >&2
  echo "       link-ticket.sh --remove <steamId> <reportId> <ticketUrl>  # 解除" >&2
  exit 1
fi
PORT="${PLAYTEST_DASHBOARD_PORT:-8932}"
BASE="http://127.0.0.1:${PORT}/playtest"
VIEWER="https://review.moores.tech/playtest/#/report"

# 値はテスター由来の id やチケット題名を含むので、JSON への埋め込みは python の json.dumps に任せる（手で引用しない）
# Values include tester-derived ids and ticket titles, so JSON encoding is left to python's json.dumps rather than hand quoting
if [ "$1" = "--remove" ]; then
  ROUTE="links/remove"
  BODY="$(python3 -c 'import json,sys; print(json.dumps({"steamId": sys.argv[1], "id": sys.argv[2], "url": sys.argv[3]}))' "$2" "$3" "$4")"
  STEAM_ID="$2"; REPORT_ID="$3"
else
  ROUTE="links/add"
  BODY="$(python3 -c 'import json,sys; print(json.dumps({"steamId": sys.argv[1], "id": sys.argv[2], "url": sys.argv[3], "title": sys.argv[4]}))' "$1" "$2" "$3" "$4")"
  STEAM_ID="$1"; REPORT_ID="$2"
fi

RESPONSE="$(curl -sS -X POST "${BASE}/api/${ROUTE}" -H "Host: 127.0.0.1:${PORT}" -H "Content-Type: application/json" \
  -H "X-Playtest-Dashboard: 1" --data "${BODY}" -w '\n%{http_code}')" || { echo "[link-ticket] ダッシュボードに接続できない: ${BASE}" >&2; exit 1; }
STATUS="${RESPONSE##*$'\n'}"
if [ "${STATUS}" != "200" ]; then
  echo "[link-ticket] 拒否された (HTTP ${STATUS}): ${RESPONSE%$'\n'*}" >&2
  exit 1
fi
echo "${VIEWER}/${STEAM_ID}/${REPORT_ID}"

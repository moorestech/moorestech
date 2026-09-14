#!/usr/bin/env bash
# プレイテスト受け口の許可SteamIDを管理APIで操作する（ADR 0058）。全置換PUTなので必ずGET→編集→PUTの順で行う
# Manages the playtest allowlist through the admin API (ADR 0058); PUT replaces the list, so always GET, edit, then PUT
set -euo pipefail

ENV_FILE="${PLAYTEST_ENV_FILE:-$HOME/hermes-agent/data/services/playtest/env.sh}"
# shellcheck disable=SC1090
[ -f "$ENV_FILE" ] && . "$ENV_FILE"

BASE="${PLAYTEST_RECEIVER_BASE:-https://playtest.tar-atari.com}"
ADMIN_KEY="${PLAYTEST_ADMIN_KEY:?PLAYTEST_ADMIN_KEY が未設定です（$ENV_FILE に書いてください）}"
CURL_CMD="${CURL_CMD:-curl}"

log() { echo "[allowlist] $*" >&2; }

fetch_list() {
  "$CURL_CMD" -sS -f -H "X-Admin-Key: $ADMIN_KEY" "$BASE/v1/allowlist"
}

# 現在のリストへ1件足す/引く。編集はpythonのjsonに任せ、順序は入力順を保つ
# Adds or removes one entry; python's json does the editing and input order is preserved
edit_list() {
  local mode="$1" steam_id="$2" current
  current="$(fetch_list)"
  printf '%s' "$current" | python3 -c '
import json, sys
mode, steam_id = sys.argv[1], sys.argv[2]
ids = json.load(sys.stdin).get("steamIds", [])
if mode == "add":
    if steam_id not in ids:
        ids.append(steam_id)
else:
    ids = [value for value in ids if value != steam_id]
print(json.dumps({"steamIds": ids}))
' "$mode" "$steam_id"
}

put_list() {
  "$CURL_CMD" -sS -f -X PUT -H "X-Admin-Key: $ADMIN_KEY" -H "Content-Type: application/json" --data "$1" "$BASE/v1/allowlist" >/dev/null
}

require_steam_id() {
  [ "${1:-}" != "" ] || { log "steamId を指定してください: $0 $2 <steamId>"; exit 2; }
}

case "${1:-}" in
  add)
    require_steam_id "${2:-}" add
    put_list "$(edit_list add "$2")"
    log "added: $2"
    ;;
  remove)
    require_steam_id "${2:-}" remove
    put_list "$(edit_list remove "$2")"
    log "removed: $2"
    ;;
  list)
    fetch_list | python3 -c "import json,sys;[print(value) for value in json.load(sys.stdin).get('steamIds', [])]"
    ;;
  *)
    log "使い方: $0 add|remove|list [<steamId>]"
    exit 2
    ;;
esac

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

# curl呼び出しの失敗経路を無音で通さない。何を・どのURLへ呼んだかと、非2xxならstatus・bodyをstderrへ出す
# Never let a curl failure pass silently; log what was called and the URL, and on a non-2xx response its status and body
http_call() {
  local method="$1" url="$2" data="${3:-}" out status body rc=0
  if [ -n "$data" ]; then
    out="$("$CURL_CMD" -sS -w '\n%{http_code}' -X "$method" -H "X-Admin-Key: $ADMIN_KEY" -H "Content-Type: application/json" --data "$data" "$url")" || rc=$?
  else
    out="$("$CURL_CMD" -sS -w '\n%{http_code}' -H "X-Admin-Key: $ADMIN_KEY" "$url")" || rc=$?
  fi
  if [ "$rc" -ne 0 ]; then
    log "許可リストAPIへ到達できない（curl終了コード $rc）: $method $url"
    exit 1
  fi
  status="${out##*$'\n'}"
  body="${out%$'\n'*}"
  case "$status" in
    2??) ;;
    *) log "許可リストAPIが失敗を返した（ADMIN_KEY不一致の可能性を含む）: $method $url status=$status body=$body"; exit 1 ;;
  esac
  printf '%s' "$body"
}

fetch_list() {
  http_call GET "$BASE/v1/allowlist"
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

# 全置換PUT。成功時のレスポンス本文（更新後リスト）は呼び出し元で使わないため捨てる
# A full-replace PUT; the response body (the updated list) is unused by callers so it is discarded
put_list() {
  http_call PUT "$BASE/v1/allowlist" "$1" >/dev/null
}

require_steam_id() {
  [ "${1:-}" != "" ] || { log "steamId を指定してください: $0 $2 <steamId>"; exit 2; }
}

# サブコマンドの振り分け。未知のサブコマンドは使い方を出して失敗する
# Dispatches the subcommand; an unknown one prints usage and fails
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

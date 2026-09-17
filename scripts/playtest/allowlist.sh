#!/usr/bin/env bash
# プレイテスト受け口の許可SteamIDを管理APIで操作する（ADR 0058）。全置換PUTなので必ずGET→編集→PUTの順で行う
# Manages the playtest allowlist through the admin API (ADR 0058); PUT replaces the list, so always GET, edit, then PUT
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[allowlist] $*" >&2; }

# 既定値はスクリプト自身の位置から導出する（ingest.sh と同じ。supervisor は HOME を差し替えるため $HOME 基準は不可）
# Defaults derive from the script's own location (same as ingest.sh; supervisor swaps HOME, so $HOME-based defaults break)
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# shellcheck disable=SC1090
if [ -f "$ENV_FILE" ]; then
  . "$ENV_FILE"
else
  log "env file が無い（${ENV_FILE}）。PLAYTEST_ADMIN_KEY 等は環境変数頼みになる"
fi
: "${PLAYTEST_ADMIN_KEY:?PLAYTEST_ADMIN_KEY が未設定です（${ENV_FILE} に書いてください）}"

# 受け口 admin API の呼び出しは lib/receiver-api.sh に一本化する。env.sh の後に読む（既定URLの確定が先行しないため）
# Receiver admin API calls go through lib/receiver-api.sh only; sourced after env.sh so its default URL does not resolve first
# shellcheck source=lib/receiver-api.sh
. "$HERE/lib/receiver-api.sh"

fetch_list() {
  receiver_allowlist_get
}

# 現在のリストへ1件足す/引く。順序は入力順のまま、段ごとの失敗検知用に関数化
# Edits the JSON in place (order preserved); kept as its own step so each stage's failure can be checked
edit_list() {
  local mode="$1" steam_id="$2" current="$3"
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
  receiver_allowlist_put "$1" >/dev/null
}

require_steam_id() {
  [ "${1:-}" != "" ] || { log "steamId を指定してください: $0 $2 <steamId>"; exit 2; }
}

# add/removeの3段（fetch→edit→put）。ネストしたコマンド置換だとerrexitが内側の失敗を拾えないため、変数受け+`||`で明示チェック
# Three steps (fetch/edit/put); nested substitutions would hide inner failures from errexit, so each is captured in a variable and checked with `||`
run_edit() {
  local mode="$1" steam_id="$2" current updated
  current="$(fetch_list)" || { log "許可リストの取得に失敗したため中止した: $mode $steam_id"; exit 1; }
  updated="$(edit_list "$mode" "$steam_id" "$current")" || { log "許可リストの編集に失敗したため中止した: $mode $steam_id"; exit 1; }
  [ -n "$updated" ] || { log "許可リストの編集結果が空だったため中止した: $mode $steam_id"; exit 1; }
  put_list "$updated" || { log "許可リストの更新に失敗したため中止した: $mode $steam_id"; exit 1; }
}

# サブコマンドの振り分け。未知のサブコマンドは使い方を出して失敗する
# Dispatches the subcommand; an unknown one prints usage and fails
case "${1:-}" in
  add)
    require_steam_id "${2:-}" add
    run_edit add "$2"
    log "added: $2"
    ;;
  remove)
    require_steam_id "${2:-}" remove
    run_edit remove "$2"
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

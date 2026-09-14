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
  if [ "$method" = "PUT" ]; then
    out="$("$CURL_CMD" -sS -w '\n%{http_code}' -X PUT -H "X-Admin-Key: $ADMIN_KEY" -H "Content-Type: application/json" --data "$data" "$url")" || rc=$?
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

# GETとPUTで分岐を共有しない。dataの有無ではなくmethod名だけで経路を決める
# GET and PUT never share a branch; the route is decided solely by the method name, never by whether data is present
http_get() { http_call GET "$1"; }
http_put() { http_call PUT "$1" "$2"; }

fetch_list() {
  http_get "$BASE/v1/allowlist"
}

# 現在のリストへ1件足す/引く。HTTPは呼ばず、渡された内容をpythonのjsonで編集するだけ。
# 順序は入力順を保つ。呼び出し元でfetchとputの間にこの関数を挟み、段ごとに失敗を検知できるようにする
# Does not call HTTP; only edits the JSON given to it (input order is preserved). Callers keep fetch and
# put as separate top-level steps around this so each stage's failure can be checked explicitly
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
  http_put "$BASE/v1/allowlist" "$1" >/dev/null
}

require_steam_id() {
  [ "${1:-}" != "" ] || { log "steamId を指定してください: $0 $2 <steamId>"; exit 2; }
}

# add/removeの3段（fetch→edit→put）。ネストしたコマンド置換にすると内側の失敗がbashのerrexitで
# 外側まで伝播しない（コマンド置換の中では-eが既定で継承されない）ため、各段をここで変数に受けて
# 明示的に`||`でチェックする。edit_listはHTTPを呼ばないので、この3段のうちHTTPが絡むのはfetchとputだけ
# Three top-level steps (fetch, edit, put) for add/remove. Nesting these as command substitutions inside
# each other would let an inner failure escape bash's errexit (command substitutions don't inherit -e by
# default), so each step's result is captured in a variable here and checked explicitly with `||`.
# edit_list makes no HTTP call, so only fetch and put touch the network
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

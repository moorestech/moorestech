#!/usr/bin/env bash
# 受け口 admin API の薄いラッパ。curl は差し替え可能で、admin key は引数にしか現れない
# Thin wrappers over the receiver admin API; curl is swappable and the admin key appears only as an argument
RECEIVER_BASE="${PLAYTEST_RECEIVER_BASE:-https://playtest.tar-atari.com}"
CURL_CMD="${CURL_CMD:-curl}"
RECEIVER_MAX_TIME="${RECEIVER_MAX_TIME:-120}"

# 共通の curl 呼び出し。$1 は出力先、以降はフラグ…最後に URL
# Shared curl call: $1 is the output path, the rest are flags followed by the URL
receiver_curl() {
  local out="$1"; shift
  "$CURL_CMD" --silent --show-error --fail --location \
    --max-time "$RECEIVER_MAX_TIME" \
    -H "X-Admin-Key: ${PLAYTEST_ADMIN_KEY:?PLAYTEST_ADMIN_KEY が未設定}" \
    -o "$out" "$@"
}

# cursor は不透明文字列なので必ず URL エンコードしてから付ける
# The cursor is opaque, so it is always percent-encoded before being appended
receiver_inbox_page() {
  local cursor="$1" out="$2" url="$RECEIVER_BASE/v1/inbox"
  if [ -n "$cursor" ]; then
    url="$url?cursor=$(python3 -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1],safe=""))' "$cursor")"
  fi
  receiver_curl "$out" "$url"
}

receiver_get_object() {
  local kind="$1" steam_id="$2" id="$3" path="$4" out="$5"
  receiver_curl "$out" "$RECEIVER_BASE/v1/inbox/$kind/$steam_id/$id/$path"
}

receiver_ack() {
  local kind="$1" steam_id="$2" id="$3"
  receiver_curl /dev/null -X POST "$RECEIVER_BASE/v1/inbox/$kind/$steam_id/$id/ack"
}

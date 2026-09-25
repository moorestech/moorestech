#!/usr/bin/env bash
# 受け口 admin API の薄いラッパ。curl は差し替え可能で、admin key は引数にしか現れない
# Thin wrappers over the receiver admin API; curl is swappable and the admin key appears only as an argument
RECEIVER_BASE="${PLAYTEST_RECEIVER_BASE:-https://playtest.moores.tech}"
CURL_CMD="${CURL_CMD:-curl}"
RECEIVER_MAX_TIME="${RECEIVER_MAX_TIME:-120}"

# パスセグメントを percent-encode する。keys.ts の許可セグメントは空白・#・?・% 等の
# URL特殊文字を通すため、組み立て前に必ずエンコードしないと壊れたURLになる
# Percent-encodes one path segment: keys.ts allows spaces/#/?/% etc through its safe-segment
# check, so encoding before URL assembly is mandatory or the URL breaks
url_encode() {
  python3 -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1],safe=""))' "$1"
}

# '/' 区切りのパスをセグメントごとにエンコードして再結合する（各セグメント内の '/' は無い前提）
# Encodes a '/'-separated path segment-by-segment and rejoins it (assumes no '/' within a segment)
url_encode_path() {
  python3 -c 'import sys,urllib.parse
print("/".join(urllib.parse.quote(seg, safe="") for seg in sys.argv[1].split("/")))' "$1"
}

# 受け口 admin API の唯一の curl 呼び出し口。$1 は出力先、以降はフラグ…最後に URL。非2xx はステータスと
# 本文先頭を stderr へ出す（admin key は出さない）。リダイレクトは追わない（curl は X-Admin-Key を
# リダイレクト先の別ホストへも送るため、鍵の流出経路になる）
# The single curl entry point for the receiver admin API: $1 is the output path, the rest are flags then the URL.
# Non-2xx prints status and body head to stderr (never the admin key). Redirects are never followed, because
# curl would forward X-Admin-Key to a different redirect host and leak the key
receiver_curl() {
  local out="$1"; shift
  local code
  code="$("$CURL_CMD" --silent --show-error \
    --max-time "$RECEIVER_MAX_TIME" \
    -H "X-Admin-Key: ${PLAYTEST_ADMIN_KEY:?PLAYTEST_ADMIN_KEY が未設定}" \
    -w '%{http_code}' -o "$out" "$@")" || { echo "[receiver] curl 自体が失敗: $*" >&2; return 1; }
  case "$code" in
    2??) return 0 ;;
    *) echo "[receiver] status=${code} body=$(head -c 200 "$out" 2>/dev/null) $*" >&2; return 1 ;;
  esac
}

# cursor は不透明文字列なので必ず URL エンコードしてから付ける
# The cursor is opaque, so it is always percent-encoded before being appended
receiver_inbox_page() {
  local cursor="$1" out="$2" url="$RECEIVER_BASE/v1/inbox"
  if [ -n "$cursor" ]; then
    url="$url?cursor=$(url_encode "$cursor")"
  fi
  receiver_curl "$out" "$url"
}

receiver_get_object() {
  local kind="$1" steam_id="$2" id="$3" path="$4" out="$5"
  receiver_curl "$out" \
    "$RECEIVER_BASE/v1/inbox/$(url_encode "$kind")/$(url_encode "$steam_id")/$(url_encode "$id")/$(url_encode_path "$path")"
}

receiver_ack() {
  local kind="$1" steam_id="$2" id="$3"
  receiver_curl /dev/null -X POST \
    "$RECEIVER_BASE/v1/inbox/$(url_encode "$kind")/$(url_encode "$steam_id")/$(url_encode "$id")/ack"
}

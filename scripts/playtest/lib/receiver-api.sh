#!/usr/bin/env bash
# 受け口 admin API の薄いラッパ。curl は差し替え可能で、admin key は引数にしか現れない
# Thin wrappers over the receiver admin API; curl is swappable and the admin key appears only as an argument
RECEIVER_BASE="${PLAYTEST_RECEIVER_BASE:-https://playtest.moores.tech}"
CURL_CMD="${CURL_CMD:-curl}"
RECEIVER_MAX_TIME="${RECEIVER_MAX_TIME:-120}"
# R2 への PUT は数GBになり得るので、制限時間は RECEIVER_MAX_TIME に「この速さで送れる秒数」を足して決める
# A PUT to R2 can be gigabytes, so its time limit is RECEIVER_MAX_TIME plus the seconds needed at this rate
R2_PUT_MIN_BYTES_PER_SEC="${R2_PUT_MIN_BYTES_PER_SEC:-1000000}"

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
# 直近の HTTP ステータスは RECEIVER_LAST_STATUS に残す（curl 自体の失敗は空）。呼び出し側はログ文言でなくこれで分岐する
# The latest HTTP status is left in RECEIVER_LAST_STATUS (empty when curl itself failed); callers branch on it, not on log text
receiver_curl() {
  local out="$1"; shift
  local code
  RECEIVER_LAST_STATUS=""
  code="$("$CURL_CMD" --silent --show-error \
    --max-time "$RECEIVER_MAX_TIME" \
    -H "X-Admin-Key: ${PLAYTEST_ADMIN_KEY:?PLAYTEST_ADMIN_KEY が未設定}" \
    -w '%{http_code}' -o "$out" "$@")" || { echo "[receiver] curl 自体が失敗: $*" >&2; return 1; }
  RECEIVER_LAST_STATUS="$code"
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

# 開発者トークンの管理応答を出力先へ保存する
# Save developer token administration responses to the output path
receiver_dev_token_issue() {
  local machine="$1" out="$2" body
  body="$(python3 -c 'import json,sys;print(json.dumps({"machine":sys.argv[1]}))' "$machine")" || return 1
  receiver_curl "$out" -X POST -H "content-type: application/json" --data "$body" "$RECEIVER_BASE/v1/admin/dev-tokens"
}
receiver_dev_token_list() { receiver_curl "$1" "$RECEIVER_BASE/v1/admin/dev-tokens"; }
receiver_dev_token_revoke() {
  local machine="$1" out="$2"
  receiver_curl "$out" -X DELETE "$RECEIVER_BASE/v1/admin/dev-tokens/$(url_encode_path "$machine")"
}

# zip 取り込みの箱を R2 へ上げる署名付き URL をもらう。本文は r2_import.py declare の出力
# Ask for presigned URLs to upload a zip-imported box to R2; the body is r2_import.py declare's output
receiver_import_urls() {
  local kind="$1" steam_id="$2" id="$3" body_file="$4" out="$5"
  receiver_curl "$out" -X POST -H "content-type: application/json" --data-binary "@$body_file" \
    "$RECEIVER_BASE/v1/inbox/$(url_encode "$kind")/$(url_encode "$steam_id")/$(url_encode "$id")/import"
}

# zip 取り込みを確定する。受け口が宣言の全ファイルの存在と長さを確かめてから ACKED を書く。409 の本文は不足・食い違いの一覧
# Settle a zip import; the receiver checks every declared file's presence and length before writing ACKED. A 409 body lists what is missing or mismatched
receiver_import_complete() {
  local kind="$1" steam_id="$2" id="$3" out="$4"
  receiver_curl "$out" -X POST \
    "$RECEIVER_BASE/v1/inbox/$(url_encode "$kind")/$(url_encode "$steam_id")/$(url_encode "$id")/import/complete"
}

# 片付いた箱の生データを R2 から消す（ACKED と PURGED の印だけ残る）。本文は {"reason": "..."}
# Delete a settled box's raw data from R2 (only the ACKED and PURGED markers remain); the body is {"reason": "..."}
receiver_purge() {
  local kind="$1" steam_id="$2" id="$3" body_file="$4" out="$5"
  receiver_curl "$out" -X DELETE -H "content-type: application/json" --data-binary "@$body_file" \
    "$RECEIVER_BASE/v1/inbox/$(url_encode "$kind")/$(url_encode "$steam_id")/$(url_encode "$id")"
}

# 署名付き URL へ 1 ファイルを PUT する。署名が Content-Length と If-None-Match: * を含むので、412（既にある）は送信済みとして扱う
# PUT one file to a presigned URL; the signature covers Content-Length and If-None-Match: *, so 412 (already there) counts as sent
r2_put_presigned() {
  local file="$1" url="$2" code bytes max_time
  bytes="$(wc -c < "$file" | tr -d ' ')" || { echo "[receiver] PUT するファイルの大きさを読めない: $file" >&2; return 1; }
  max_time=$((RECEIVER_MAX_TIME + bytes / R2_PUT_MIN_BYTES_PER_SEC))
  code="$("$CURL_CMD" --silent --show-error --max-time "$max_time" -X PUT -H "If-None-Match: *" \
    --upload-file "$file" -w '%{http_code}' -o /dev/null "$url")" || { echo "[receiver] PUT 自体が失敗: $file" >&2; return 1; }
  case "$code" in
    2??|412) return 0 ;;
    *) echo "[receiver] PUT status=${code}（${bytes} bytes・制限 ${max_time} 秒）: $file" >&2; return 1 ;;
  esac
}

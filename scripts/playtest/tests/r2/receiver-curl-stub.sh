#!/usr/bin/env bash
# 受け口 admin API の偽 curl（fetch-report.sh・prune.sh のテスト用）。FAKE_R2/objects/<kind>/<steamId>/<id>/<path> を R2 とみなす
# GET はオブジェクトを返し、無くて箱に PURGED があれば 410、それも無ければ 404。DELETE は FAKE_R2/deleted.txt に記録して 200
# FAKE_DELETE_FAIL に一致する箱の DELETE は 409 を返す。全呼び出しを FAKE_R2/calls.log に残す
# Fake curl for the receiver admin API (tests of fetch-report.sh and prune.sh); FAKE_R2/objects/<kind>/<steamId>/<id>/<path> is R2
# GET returns the object, 410 when absent but the box has PURGED, else 404; DELETE is logged to FAKE_R2/deleted.txt and answers 200
# A DELETE of the box matching FAKE_DELETE_FAIL answers 409; every call is logged to FAKE_R2/calls.log
set -euo pipefail
out=/dev/null; url=""; method=GET; body=""; key=""
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -X) method="$2"; shift 2 ;;
    -H) case "$2" in "X-Admin-Key: "*) key="${2#X-Admin-Key: }" ;; esac; shift 2 ;;
    --data-binary) body="${2#@}"; shift 2 ;;
    -w|--max-time) shift 2 ;;
    --silent|--show-error) shift ;;
    *) url="$1"; shift ;;
  esac
done
path="${url#*://*/v1/inbox/}"
decoded="$(python3 -I -c 'import sys,urllib.parse;print(urllib.parse.unquote(sys.argv[1]))' "$path")"
echo "$method $path key=$key" >> "$FAKE_R2/calls.log"

if [ "$method" = DELETE ]; then
  if [ "$decoded" = "${FAKE_DELETE_FAIL:-}" ]; then printf '{"reason":"not-acked"}' > "$out"; echo 409; exit 0; fi
  echo "$decoded $(cat "$body")" >> "$FAKE_R2/deleted.txt"
  printf '{"purged":true,"deletedObjects":3,"deletedBytes":10}' > "$out"; echo 200; exit 0
fi
src="$FAKE_R2/objects/$decoded"
if [ -f "$src" ]; then mkdir -p "$(dirname "$out")"; cp "$src" "$out"; echo 200; exit 0; fi
box="$(echo "$decoded" | cut -d/ -f1-3)"
if [ -f "$FAKE_R2/objects/$box/PURGED" ]; then
  printf '{"reason":"purged","purgedAt":"2026-10-01T00:00:00Z"}' > "$out"; echo 410; exit 0
fi
printf '{"reason":"not-found"}' > "$out"; echo 404

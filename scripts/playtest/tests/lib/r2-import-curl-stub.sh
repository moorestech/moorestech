#!/usr/bin/env bash
# zip 取り込みの偽 curl: 受け口の import POST に署名付き URL を返し、その URL への PUT を偽 R2 へ保存する
# FAKE_IMPORT_DIR に calls.log（POST/PUT の記録）と r2/（PUT された実体）を書く。FAKE_IMPORT_MODE で応答を変える:
#   ok … 全部成功 / conflict … import が conflicts を返す / putfail … video.mp4 の PUT が 500 / exists … PUT が全部 412（送信済み）
# Fake curl for the zip import: answers the receiver's import POST with presigned URLs and stores PUTs into a fake R2
# Writes calls.log (POST/PUT records) and r2/ (PUT bodies) under FAKE_IMPORT_DIR; FAKE_IMPORT_MODE switches the replies:
#   ok = all succeed / conflict = import returns conflicts / putfail = PUT of video.mp4 answers 500 / exists = every PUT answers 412
set -euo pipefail
out=/dev/null; url=""; method=GET; body=""; upload=""; headers=(); max_time=""
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -X) method="$2"; shift 2 ;;
    -H) headers+=("$2"); shift 2 ;;
    --data-binary) body="${2#@}"; shift 2 ;;
    --upload-file) upload="$2"; shift 2 ;;
    --max-time) max_time="$2"; shift 2 ;;
    -w) shift 2 ;;
    --silent|--show-error) shift ;;
    *) url="$1"; shift ;;
  esac
done
mode="${FAKE_IMPORT_MODE:-ok}"
log="$FAKE_IMPORT_DIR/calls.log"

# import POST: 宣言の全ファイルへ偽の署名付き URL を返す（conflict では video.mp4 を長さ違いとして返す）
# Import POST: return a fake presigned URL for every declared file (conflict mode reports video.mp4 as a length mismatch)
if [ "$method" = POST ] && [ "${url%/import}" != "$url" ]; then
  printf 'POST %s key=%s\n' "${url#*://*/v1/}" "$(printf '%s\n' "${headers[@]}" | sed -n 's/^X-Admin-Key: //p')" >> "$log"
  python3 -I - "$body" "$mode" > "$out" <<'PY'
import json, sys
files = json.load(open(sys.argv[1], encoding="utf-8"))["files"]
if sys.argv[2] == "conflict":
    reply = {"uploads": [], "conflicts": [{"path": "video.mp4", "expectedBytes": 3, "actualBytes": 9}]}
else:
    reply = {"uploads": [dict(f, url="https://r2.invalid/put/" + f["path"]) for f in files], "conflicts": []}
print(json.dumps(dict(reply, expiresInSeconds=900)))
PY
  echo 200; exit 0
fi

# 署名付き URL への PUT: If-None-Match: * が付いていることを記録し、実体を偽 R2 へ置く
# PUT to a presigned URL: record whether If-None-Match: * was sent and store the body in the fake R2
if [ "$method" = PUT ]; then
  path="${url#https://r2.invalid/put/}"
  inm=no; for h in "${headers[@]}"; do [ "$h" = "If-None-Match: *" ] && inm=yes; done
  printf 'PUT %s inm=%s\n' "$path" "$inm" >> "$log"
  printf '%s %s\n' "$path" "$max_time" >> "$FAKE_IMPORT_DIR/put-times.log"
  if [ "$mode" = putfail ] && [ "$path" = video.mp4 ]; then echo 500; exit 0; fi
  if [ "$mode" = exists ]; then echo 412; exit 0; fi
  mkdir -p "$(dirname "$FAKE_IMPORT_DIR/r2/$path")"; cp "$upload" "$FAKE_IMPORT_DIR/r2/$path"; echo 200; exit 0
fi
echo "UNEXPECTED $method $url" >> "$log"; echo 599

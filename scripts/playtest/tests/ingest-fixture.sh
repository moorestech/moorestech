#!/usr/bin/env bash
# 取り込みテストの共通下ごしらえ（logs repo・偽 R2・curl スタブ・run_ingest）。呼び出し側が HERE と TMP を定義してから source する
# Shared setup for the ingest tests (logs repo, fake R2, curl stub, run_ingest); the caller defines HERE and TMP before sourcing
LOGS="$TMP/logs"; R2="$TMP/r2"
mkdir -p "$LOGS/harness/playtest" "$LOGS/harness/bug-report/inbox" "$R2/objects"
( cd "$LOGS" && git init -q && git config user.email t@t && git config user.name t \
  && echo x > .gitkeep && git add -A && git commit -qm init )

mk_object() { mkdir -p "$(dirname "$R2/objects/$1")"; printf '%s' "$2" > "$R2/objects/$1"; }
# READY 本文は PlaytestUploader.ComposeSummary() と同じ形（kind/id/fileCount/files/skipped/manifest）で作る。
# files を空文字で渡すと files キー自体を持たない（旧クライアント相当の）要約になる
# READY bodies mirror PlaytestUploader.ComposeSummary() (kind/id/fileCount/files/skipped/manifest);
# an empty files argument omits the files key entirely, like a legacy client summary
mk_ready() {
  mk_object "$1/READY" "$(python3 -c '
import json, os, sys
box, files = sys.argv[1], sys.argv[2]
kind, _steam, bundle_id = box.split("/")
manifest_path = os.path.join(sys.argv[3], box, "manifest.json")
summary = {"kind": kind, "id": bundle_id}
if files:
    summary["fileCount"] = len(json.loads(files))
    summary["files"] = json.loads(files)
else:
    summary["fileCount"] = 0
summary["skipped"] = []
summary["manifest"] = open(manifest_path).read() if os.path.isfile(manifest_path) else None
print(json.dumps(summary, ensure_ascii=False))
' "$1" "$2" "$R2/objects")"
}


# curl スタブ: -o の出力先へ偽 R2 のファイルを置き、POST は ack として記録する
# curl stub: copies fake-R2 files to the -o target and records POSTs as acks
cat > "$TMP/curl" <<'SH'
#!/usr/bin/env bash
# receiver-api.sh は -w '%{http_code}' で status を読むので、この偽 curl も
# 常に exit 0 でステータス3桁を標準出力へ書く（本物の curl の非--fail挙動と同じ）
# receiver-api.sh reads status via -w '%{http_code}', so this fake curl also
# always exits 0 and prints a 3-digit status to stdout (matching real curl without --fail)
out=""; url=""; method=GET
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -X) method="$2"; shift 2 ;;
    -w) shift 2 ;;
    -H|--max-time) shift 2 ;;
    --silent|--show-error|--fail|--location) shift ;;
    *) url="$1"; shift ;;
  esac
done
path="${url#*://*/v1/}"; path="${path%%\?*}"
if [ "$method" = POST ]; then echo "$path" >> "$FAKE_R2/acked.txt"; : > "$out"; echo 200; exit 0; fi
if [ "$path" = inbox ]; then cp "$FAKE_R2/inbox.json" "$out"; echo 200; exit 0; fi
# decode前の生パスを記録する。空白・#・非ASCIIが生のまま届いたら、それは
# percent-encodeされていない壊れたURL（本物のcurlなら#以降がfragmentとして
# 落ちる/空白で不正URLになる）なので400で拒否する
# Record the raw pre-decode path. A literal space/#/non-ASCII byte means the
# URL was never percent-encoded (real curl would drop everything after '#'
# as a fragment, or choke on the space), so reject it with 400
echo "$path" >> "$FAKE_R2/requested.txt"
if python3 -c 'import sys
p = sys.argv[1]
sys.exit(1 if any(ord(c) > 126 or c in " #" for c in p) else 0)' "$path"; then :; else echo 400; exit 0; fi
# receiver-api.sh は各セグメントを percent-encode して渡す。実オブジェクト名（空白・#・日本語等）へ
# 戻すには decode してからファイル系へ当てる必要がある
# receiver-api.sh percent-encodes each segment; decode before mapping onto the real object name
# (spaces, '#', Japanese, etc.)
decoded="$(python3 -c 'import sys,urllib.parse;print(urllib.parse.unquote(sys.argv[1]))' "$path")"
src="$FAKE_R2/objects/${decoded#inbox/}"
[ -f "$src" ] || { echo 404; exit 0; }
mkdir -p "$(dirname "$out")"; cp "$src" "$out"; echo 200
SH
chmod +x "$TMP/curl"

# Steam Web API の応答を固定し、問い合わせ回数も記録する
# Fix the Steam Web API response and record lookup counts
steam_stub_curl() {
  local out="" url=""
  while [ "$#" -gt 0 ]; do
    case "$1" in
      -o) out="$2"; shift 2 ;;
      -w|--max-time|-H) shift 2 ;;
      -*) shift ;;
      *) url="$1"; shift ;;
    esac
  done
  echo "$url" >> "$TMP/steam-calls.log"
  if [ "${STEAM_STUB_MODE:-ok}" = ok ]; then
    printf '{"response":{"players":[{"steamid":"76561198000000001","personaname":"Tester <One>","profileurl":"https://steamcommunity.com/profiles/76561198000000001/"}]}}' > "$out"
    printf 200
  else
    printf unavailable > "$out"
    printf 503
  fi
}
export -f steam_stub_curl
export TMP

ISOLATED_TMPDIR="$TMP/tmpdir"; mkdir -p "$ISOLATED_TMPDIR"

run_ingest() {
  MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy \
  FAKE_R2="$R2" CURL_CMD="$TMP/curl" STEAM_CURL_CMD=steam_stub_curl \
  GIT_PUSH="${GIT_PUSH:-0}" TMPDIR="$ISOLATED_TMPDIR" bash "$HERE/../ingest.sh"
}

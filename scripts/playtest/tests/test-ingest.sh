#!/usr/bin/env bash
# curl を偽の受け口に差し替えて、取り込み・ack・異常箱の据え置き・自動投入しないことを検証する
# Verifies ingest, ack, the un-acked broken box and the absence of auto-enqueue, with curl stubbed
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT

LOGS="$TMP/logs"; R2="$TMP/r2"
mkdir -p "$LOGS/harness/playtest" "$LOGS/harness/bug-report/inbox" "$R2/objects"
( cd "$LOGS" && git init -q && git config user.email t@t && git config user.name t \
  && echo x > .gitkeep && git add -A && git commit -qm init )

# 偽の R2: バグ報告・感想・進行記録・files[] が壊れた箱の4件
# Fake R2 with four boxes: a bug report, a feedback report, a progress record and a broken one
mk_object() { mkdir -p "$(dirname "$R2/objects/$1")"; printf '%s' "$2" > "$R2/objects/$1"; }
mk_object report/7656001/20260913_100000_bug1/READY '{"kind":"bug","files":["manifest.json","screenshot.png"]}'
mk_object report/7656001/20260913_100000_bug1/manifest.json '{"kind":"bug","steamId":"7656001","description":"ベルトが止まる"}'
mk_object report/7656001/20260913_100000_bug1/screenshot.png 'PNG'
mk_object report/7656002/20260913_110000_fb1/READY '{"kind":"feedback","files":["manifest.json"]}'
mk_object report/7656002/20260913_110000_fb1/manifest.json '{"kind":"feedback","steamId":"7656002","description":"序盤が長い"}'
mk_object progress/7656001/20260913_120000_pg1/READY '{"files":["record.json"]}'
mk_object progress/7656001/20260913_120000_pg1/record.json '{"schemaVersion":1,"steamId":"7656001","playSeconds":600}'
mk_object report/7656003/20260913_130000_bad1/READY '{"kind":"bug"}'
# files[] に空白・#・日本語を含む箱（percent-encode/decode の往復を検証する）
# A box whose files[] entry has a space, a '#' and Japanese characters (round-trips through percent-encoding)
mk_object 'report/7656004/20260913_140000_bug2/READY' '{"kind":"bug","files":["manifest.json","note #1 メモ.txt"]}'
mk_object 'report/7656004/20260913_140000_bug2/manifest.json' '{"kind":"bug","steamId":"7656004","description":"特殊文字ファイル名"}'
mk_object 'report/7656004/20260913_140000_bug2/note #1 メモ.txt' 'hello'
cat > "$R2/inbox.json" <<'JSON'
{"items":[
 {"kind":"report","steamId":"7656001","id":"20260913_100000_bug1","readyAt":"2026-09-13T01:00:00Z"},
 {"kind":"report","steamId":"7656002","id":"20260913_110000_fb1","readyAt":"2026-09-13T02:00:00Z"},
 {"kind":"progress","steamId":"7656001","id":"20260913_120000_pg1","readyAt":"2026-09-13T03:00:00Z"},
 {"kind":"report","steamId":"7656003","id":"20260913_130000_bad1","readyAt":"2026-09-13T04:00:00Z"},
 {"kind":"report","steamId":"7656004","id":"20260913_140000_bug2","readyAt":"2026-09-13T05:00:00Z"}],
 "cursor":null}
JSON

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

ISOLATED_TMPDIR="$TMP/tmpdir"; mkdir -p "$ISOLATED_TMPDIR"

run_ingest() {
  MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy \
  FAKE_R2="$R2" CURL_CMD="$TMP/curl" GIT_PUSH=0 TMPDIR="$ISOLATED_TMPDIR" bash "$HERE/../ingest.sh"
}
run_ingest

P="$LOGS/harness/playtest"
[ -f "$P/reports/7656001/20260913_100000_bug1/manifest.json" ] || { echo "NG: バグ報告が置かれていない"; exit 1; }
[ -f "$P/reports/7656001/20260913_100000_bug1/ingest.json" ] || { echo "NG: ingest.json が無い"; exit 1; }
grep -q '"readyAt":"2026-09-13T01:00:00Z"' "$P/reports/7656001/20260913_100000_bug1/ingest.json" || { echo "NG: readyAt が写っていない"; exit 1; }
[ -f "$P/reports/7656002/20260913_110000_fb1/manifest.json" ] || { echo "NG: 感想が置かれていない"; exit 1; }
[ -f "$P/progress/7656001/20260913_120000_pg1/record.json" ] || { echo "NG: 進行記録が置かれていない"; exit 1; }
[ ! -e "$P/reports/7656003/20260913_130000_bad1" ] || { echo "NG: files[] 無しの箱が公開された"; exit 1; }
[ -f "$P/reports/7656004/20260913_140000_bug2/note #1 メモ.txt" ] || { echo "NG: 空白/#/日本語を含むファイル名が取り込まれていない"; exit 1; }
grep -q 'report/7656004/20260913_140000_bug2/ack' "$R2/acked.txt" || { echo "NG: 特殊文字ファイル名の箱が ack されていない"; exit 1; }
grep -qF 'report/7656004/20260913_140000_bug2/note%20%231%20%E3%83%A1%E3%83%A2.txt' "$R2/requested.txt" || { echo "NG: 特殊文字ファイル名がURLエンコード済みの形で届いていない"; exit 1; }

# 自動投入しない（ADR 0061）。inbox は空のまま、マーカーも付かない
# No auto-enqueue (ADR 0061): the auto-fix inbox stays empty and no marker is written
I="$LOGS/harness/bug-report/inbox"
[ -z "$(ls -A "$I")" ] || { echo "NG: 取り込みが自動修正ランへ自動投入した"; exit 1; }
[ ! -e "$P/reports/7656001/20260913_100000_bug1/AUTOFIX_QUEUED" ] || { echo "NG: 取り込みが AUTOFIX_QUEUED を付けた"; exit 1; }

grep -q 'report/7656001/20260913_100000_bug1/ack' "$R2/acked.txt" || { echo "NG: ack されていない"; exit 1; }
grep -q '20260913_130000_bad1/ack' "$R2/acked.txt" && { echo "NG: 壊れた箱が ack された"; exit 1; }
# set -o pipefail 下では「cmd | grep -q」が一致後の早期終了で SIGPIPE を拾い誤検知するため、一度ファイルへ落として調べる
# Under set -o pipefail, "cmd | grep -q" can misreport via SIGPIPE from grep's early exit, so redirect to a file first
git -C "$LOGS" log --oneline > "$TMP/gitlog.txt"
grep -q 'playtest: ingest' "$TMP/gitlog.txt" || { echo "NG: logs repo に commit が無い"; exit 1; }

# ack が失われて再配信された場合: 再ダウンロードせず ack だけやり直す
# When an item is redelivered after a lost ack: re-ack only, never re-download
rm -f "$R2/acked.txt"
BEFORE="$(cat "$P/reports/7656001/20260913_100000_bug1/ingest.json")"
run_ingest
grep -q 'report/7656001/20260913_100000_bug1/ack' "$R2/acked.txt" || { echo "NG: 再 ack されない"; exit 1; }
[ "$BEFORE" = "$(cat "$P/reports/7656001/20260913_100000_bug1/ingest.json")" ] || { echo "NG: 再ダウンロードされた"; exit 1; }

# 死んだPIDの残骸ロックがあっても取り込みが進む（SIGKILL/OOM/再起動でtrapが走らなかった想定）
# Ingest still proceeds despite a stale lock left by a dead PID (simulating a killed/OOM'd/rebooted worker)
sh -c 'exit 0' & DEAD_PID=$!
wait "$DEAD_PID" 2>/dev/null || true
LOCK_DIR="$ISOLATED_TMPDIR/moorestech-playtest-ingest.lock"
mkdir -p "$LOCK_DIR"; echo "$DEAD_PID" > "$LOCK_DIR/pid"
rm -f "$R2/acked.txt"
run_ingest
grep -q 'report/7656001/20260913_100000_bug1/ack' "$R2/acked.txt" || { echo "NG: 死んだPIDの残骸ロックで取り込みが止まった"; exit 1; }

# LOGS が git repo でなければ、受け口に触る前に止まる（ダウンロードも ack もしない）
# When LOGS is not a git repo, it stops before touching the receiver (no download, no ack)
NOGIT="$TMP/nogit"; mkdir -p "$NOGIT"
rm -f "$R2/acked.txt"
if MOORESTECH_LOGS="$NOGIT" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy \
   FAKE_R2="$R2" CURL_CMD="$TMP/curl" GIT_PUSH=0 TMPDIR="$ISOLATED_TMPDIR" bash "$HERE/../ingest.sh"; then
  echo "NG: logs repo が無いのに正常終了した"; exit 1
fi
[ ! -d "$NOGIT/harness" ] || { echo "NG: logs repo が無いのに取り込みが進んだ"; exit 1; }
[ ! -f "$R2/acked.txt" ] || { echo "NG: logs repo が無いのに ack された"; exit 1; }
echo OK

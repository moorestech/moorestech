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
cat > "$R2/inbox.json" <<'JSON'
{"items":[
 {"kind":"report","steamId":"7656001","id":"20260913_100000_bug1","readyAt":"2026-09-13T01:00:00Z"},
 {"kind":"report","steamId":"7656002","id":"20260913_110000_fb1","readyAt":"2026-09-13T02:00:00Z"},
 {"kind":"progress","steamId":"7656001","id":"20260913_120000_pg1","readyAt":"2026-09-13T03:00:00Z"},
 {"kind":"report","steamId":"7656003","id":"20260913_130000_bad1","readyAt":"2026-09-13T04:00:00Z"}],
 "cursor":null}
JSON

# curl スタブ: -o の出力先へ偽 R2 のファイルを置き、POST は ack として記録する
# curl stub: copies fake-R2 files to the -o target and records POSTs as acks
cat > "$TMP/curl" <<'SH'
#!/usr/bin/env bash
out=""; url=""; method=GET
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -X) method="$2"; shift 2 ;;
    -H|--max-time) shift 2 ;;
    --silent|--show-error|--fail|--location) shift ;;
    *) url="$1"; shift ;;
  esac
done
path="${url#*://*/v1/}"; path="${path%%\?*}"
if [ "$method" = POST ]; then echo "$path" >> "$FAKE_R2/acked.txt"; : > "$out"; exit 0; fi
if [ "$path" = inbox ]; then cp "$FAKE_R2/inbox.json" "$out"; exit 0; fi
src="$FAKE_R2/objects/${path#inbox/}"
[ -f "$src" ] || exit 22
mkdir -p "$(dirname "$out")"; cp "$src" "$out"
SH
chmod +x "$TMP/curl"

run_ingest() {
  MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy \
  FAKE_R2="$R2" CURL_CMD="$TMP/curl" GIT_PUSH=0 bash "$HERE/../ingest.sh"
}
run_ingest

P="$LOGS/harness/playtest"
[ -f "$P/reports/7656001/20260913_100000_bug1/manifest.json" ] || { echo "NG: バグ報告が置かれていない"; exit 1; }
[ -f "$P/reports/7656001/20260913_100000_bug1/ingest.json" ] || { echo "NG: ingest.json が無い"; exit 1; }
grep -q '"readyAt":"2026-09-13T01:00:00Z"' "$P/reports/7656001/20260913_100000_bug1/ingest.json" || { echo "NG: readyAt が写っていない"; exit 1; }
[ -f "$P/reports/7656002/20260913_110000_fb1/manifest.json" ] || { echo "NG: 感想が置かれていない"; exit 1; }
[ -f "$P/progress/7656001/20260913_120000_pg1/record.json" ] || { echo "NG: 進行記録が置かれていない"; exit 1; }
[ ! -e "$P/reports/7656003/20260913_130000_bad1" ] || { echo "NG: files[] 無しの箱が公開された"; exit 1; }

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
echo OK

#!/usr/bin/env bash
# 取り込みの防御経路（不正な steamId/id・改行入りの files[]・制御文字入り item・固定パスのロック・push 前の上流確認）を検証する
# Verifies ingest's guards: unsafe steamId/id, a files[] entry with a newline, control characters, the fixed lock path and the upstream check
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
# shellcheck source=ingest-fixture.sh
. "$HERE/ingest-fixture.sh"
P="$LOGS/harness/playtest"

# 改行入りのパス: 行分割すると "../../escape" が別行になり .. 検査をすり抜けていた形
# A path with a newline: split per line, "../../escape" used to slip past the .. check as its own row
mk_object report/7656011/20260913_150000_nl/manifest.json '{"kind":"bug"}'
mk_ready report/7656011/20260913_150000_nl '["manifest.json","a\n../../escape"]'
python3 - "$R2/inbox.json" <<'PY'
import json, sys
items = [
    {"kind": "report", "steamId": "..", "id": "20260913_150000_dots", "readyAt": "2026-09-13T06:00:00Z"},
    {"kind": "report", "steamId": "7656010", "id": "", "readyAt": "2026-09-13T06:00:00Z"},
    {"kind": "report", "steamId": "7656011", "id": "20260913_150000_nl", "readyAt": "2026-09-13T06:00:00Z"},
    {"kind": "report", "steamId": "7656012", "id": "x\nreport\t7656012\t..", "readyAt": "2026-09-13T06:00:00Z"},
]
json.dump({"items": items, "cursor": None}, open(sys.argv[1], "w"))
PY

run_ingest 2> "$TMP/run1.log"
grep -q 'steamId/id が安全なパスセグメントでない: report/../20260913_150000_dots' "$TMP/run1.log" || { echo "NG: .. の steamId が理由付きで拒否されていない"; exit 1; }
grep -q 'steamId/id が安全なパスセグメントでない: report/7656010/' "$TMP/run1.log" || { echo "NG: 空の id が理由付きで拒否されていない"; exit 1; }
grep -q "files\[\] に不正なパスを含む: report/7656011/20260913_150000_nl" "$TMP/run1.log" || { echo "NG: 改行入りパスが拒否されていない"; exit 1; }
grep -q '制御文字を含む item を飛ばす' "$TMP/run1.log" || { echo "NG: 制御文字入り item が理由付きで飛ばされていない"; exit 1; }
[ ! -e "$R2/acked.txt" ] || { echo "NG: 不正な箱が ack された"; exit 1; }
[ ! -e "$LOGS/harness/escape" ] && [ ! -e "$P/escape" ] || { echo "NG: 箱の外へ書き出された"; exit 1; }
find "$P" -type f > "$TMP/files.txt"
[ ! -s "$TMP/files.txt" ] || { echo "NG: 不正な箱が取り込まれた: $(cat "$TMP/files.txt")"; exit 1; }

# 生きている別プロセスのロックは $TMPDIR が違っても効く（supervisor 配下と手動実行の相互排他）
# A live lock held by another process excludes this run even under a different $TMPDIR (supervisor vs manual)
printf '%s' '{"items":[{"kind":"report","steamId":"7656013","id":"20260913_160000_ok","readyAt":"2026-09-13T07:00:00Z"}],"cursor":null}' > "$R2/inbox.json"
mk_object report/7656013/20260913_160000_ok/manifest.json '{"kind":"bug"}'
mk_ready report/7656013/20260913_160000_ok '["manifest.json"]'
LOCK_DIR="$LOGS/.git/moorestech-playtest-ingest.lock"
mkdir -p "$LOCK_DIR"; echo $$ > "$LOCK_DIR/pid"
ISOLATED_TMPDIR="$TMP/other-tmpdir"; mkdir -p "$ISOLATED_TMPDIR"
run_ingest 2> "$TMP/run2.log"
grep -q '別の取り込みが進行中' "$TMP/run2.log" || { echo "NG: 固定パスのロックが効いていない"; exit 1; }
[ ! -e "$R2/acked.txt" ] || { echo "NG: ロック中に取り込みが進んだ"; exit 1; }
rm -rf "$LOCK_DIR"

# 上流未設定で差を数えられないときは 0 と読み替えず ERROR を出す
# When the ahead count fails (no upstream), it logs an ERROR instead of reading it as 0
GIT_PUSH=1 run_ingest 2> "$TMP/run3.log"
grep -q 'report/7656013/20260913_160000_ok/ack' "$R2/acked.txt" || { echo "NG: 正常な箱が ack されない"; exit 1; }
grep -q 'ERROR: upstream との差を数えられず push しない' "$TMP/run3.log" || { echo "NG: rev-list の失敗が無音"; exit 1; }
echo OK

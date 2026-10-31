#!/usr/bin/env bash
# fetch-report.sh を偽の受け口で検証する（正常取得・既定の取得先・410 で exit 3・404・既存の取得先と不正な steamId の拒否・途中失敗）
# Verifies fetch-report.sh against a fake receiver: normal fetch, default destination, 410 -> exit 3, 404, rejected inputs, mid-fetch failure
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
FETCH="$HERE/../../fetch-report.sh"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
R2="$TMP/r2"; mkdir -p "$R2/objects" "$TMP/tmpdir"
fail() { echo "NG: $*"; exit 1; }
mk_object() { mkdir -p "$(dirname "$R2/objects/$1")"; printf '%s' "$2" > "$R2/objects/$1"; }
mk_ready() { mk_object "$1/READY" "$(python3 -I -c 'import json,sys;print(json.dumps({"kind":"report","files":json.loads(sys.argv[1]),"skipped":[]}))' "$2")"; }
run_fetch() {
  PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy CURL_CMD="$HERE/receiver-curl-stub.sh" FAKE_R2="$R2" \
  TMPDIR="$TMP/tmpdir" bash "$FETCH" "$@"
}

# 正常取得: READY の files[] を全部（生データも）取り、取得先を stdout に 1 行出す
# Normal fetch: every file in READY's files[] (raw data included) is downloaded and the destination is printed
B=report/7656001/20261030_100000_bug1
mk_object "$B/manifest.json" '{"kind":"bug"}'
mk_object "$B/video.mp4" 'MP4'
mk_object "$B/world/save.json" '{"w":1}'
mk_object "$B/logs/a b#メモ.txt" 'log'
mk_ready "$B" '["manifest.json","video.mp4","world/save.json","logs/a b#メモ.txt"]'
run_fetch --out "$TMP/out1" 7656001 20261030_100000_bug1 > "$TMP/out1.txt" 2> "$TMP/err1.txt" || fail "正常取得が失敗: $(cat "$TMP/err1.txt")"
[ "$(cat "$TMP/out1.txt")" = "$TMP/out1" ] || fail "stdout が取得先 1 行でない: $(cat "$TMP/out1.txt")"
for f in READY manifest.json video.mp4 world/save.json "logs/a b#メモ.txt"; do
  cmp -s "$R2/objects/$B/$f" "$TMP/out1/$f" || fail "$f が取れていない"
done
[ ! -e "$TMP/out1.partial" ] && [ ! -e "$TMP/out1.partial.err" ] || fail "partial が残った"
grep -q 'key=dummy' "$R2/calls.log" || fail "管理キーが渡っていない"

# 既定の取得先は ${TMPDIR}/moorestech-playtest-fetch/<kind>/<sid>/<id>。--kind progress も同じ規則
# The default destination is ${TMPDIR}/moorestech-playtest-fetch/<kind>/<sid>/<id>, and --kind progress follows it too
run_fetch 7656001 20261030_100000_bug1 > "$TMP/out2.txt" 2> /dev/null || fail "既定の取得先で失敗"
DEF="$TMP/tmpdir/moorestech-playtest-fetch/report/7656001/20261030_100000_bug1"
[ "$(cat "$TMP/out2.txt")" = "$DEF" ] && [ -f "$DEF/video.mp4" ] || fail "既定の取得先が違う: $(cat "$TMP/out2.txt")"
mk_object progress/7656001/20261030_110000_pg1/record.json '{}'
mk_ready progress/7656001/20261030_110000_pg1 '["record.json"]'
run_fetch --kind progress 7656001 20261030_110000_pg1 > "$TMP/out3.txt" 2> /dev/null || fail "--kind progress で失敗"
[ -f "$TMP/tmpdir/moorestech-playtest-fetch/progress/7656001/20261030_110000_pg1/record.json" ] || fail "進行記録が取れていない"

# 片付けで消えた箱（410）は exit 3 で、purgedAt を含む理由を出し、取得先を作らない
# A box purged after settling (410) exits 3 with a reason carrying purgedAt and creates no destination
mk_object report/7656002/20261001_purged/PURGED '{}'
rc=0; run_fetch --out "$TMP/out4" 7656002 20261001_purged > "$TMP/out4.txt" 2> "$TMP/err4.txt" || rc=$?
[ "$rc" -eq 3 ] || fail "410 の終了コードが $rc: $(cat "$TMP/err4.txt")"
grep -q 'status=410' "$TMP/err4.txt" && grep -q 'purgedAt' "$TMP/err4.txt" && grep -q '削除済み' "$TMP/err4.txt" || fail "410 の理由が出ていない"
[ ! -s "$TMP/out4.txt" ] && [ ! -e "$TMP/out4" ] && [ ! -e "$TMP/out4.partial" ] || fail "410 なのに取得先か stdout が残った"

# 無い箱（404）は exit 1（3 ではない）
# A missing box (404) exits 1, not 3
rc=0; run_fetch --out "$TMP/out5" 7656003 nothing > /dev/null 2> "$TMP/err5.txt" || rc=$?
[ "$rc" -eq 1 ] && grep -q 'READY を取れない' "$TMP/err5.txt" || fail "404 の扱いが違う（rc=${rc}）"

# 既存の取得先は拒否し、中身に触らず受け口にも問い合わせない
# An existing destination is refused, left untouched, and the receiver is never queried
mkdir -p "$TMP/out6"; echo keep > "$TMP/out6/mine.txt"; : > "$R2/calls.log"
rc=0; run_fetch --out "$TMP/out6" 7656001 20261030_100000_bug1 > /dev/null 2> "$TMP/err6.txt" || rc=$?
[ "$rc" -eq 1 ] && grep -q '取得先が既にある' "$TMP/err6.txt" || fail "既存の取得先が拒否されない（rc=${rc}）"
[ "$(cat "$TMP/out6/mine.txt")" = keep ] && [ "$(ls "$TMP/out6")" = mine.txt ] || fail "既存の取得先の中身が変わった"
[ ! -s "$R2/calls.log" ] || fail "既存の取得先なのに受け口へ問い合わせた"

# 不正な steamId/id は受け口に触る前に拒否する
# Unsafe steamId/id values are refused before touching the receiver
for bad in ".." "a/b" ""; do
  rc=0; run_fetch --out "$TMP/out7" "$bad" 20261030_100000_bug1 > /dev/null 2> "$TMP/err7.txt" || rc=$?
  [ "$rc" -eq 1 ] && grep -q '安全なパスセグメントでない' "$TMP/err7.txt" || fail "不正な steamId '${bad}' が拒否されない（rc=${rc}）"
done
rc=0; run_fetch --out "$TMP/out7" 7656001 ".." > /dev/null 2>&1 || rc=$?
[ "$rc" -eq 1 ] || fail "不正な id が拒否されない"
[ ! -s "$R2/calls.log" ] && [ ! -e "$TMP/out7" ] || fail "不正な入力で受け口に触った"

# files[] の 1 件が取れなければ exit 1 で、取得先も partial も残さない
# When one file in files[] cannot be fetched it exits 1 and leaves neither destination nor partial
mk_object report/7656004/20261030_broken/manifest.json '{}'
mk_ready report/7656004/20261030_broken '["manifest.json","video.mp4"]'
rc=0; run_fetch --out "$TMP/out8" 7656004 20261030_broken > /dev/null 2> "$TMP/err8.txt" || rc=$?
[ "$rc" -eq 1 ] && grep -q '取得失敗 video.mp4' "$TMP/err8.txt" || fail "途中失敗の扱いが違う（rc=${rc}）"
[ ! -e "$TMP/out8" ] && [ ! -e "$TMP/out8.partial" ] || fail "途中失敗で取得先か partial が残った"
echo OK

#!/usr/bin/env bash
# settled.py と prune.sh を偽の受け口で検証する（記録無しは無言・7日未満は対象外・一覧・--execute の DELETE と記録・不正な記録・削除失敗）
# Verifies settled.py and prune.sh with a fake receiver: silent without records, <7 days skipped, listing, --execute DELETE and record,
# malformed records and a failed DELETE
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
PRUNE="$HERE/../../r2_store/prune.sh"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
LOGS="$TMP/logs"; R2="$TMP/r2"; mkdir -p "$LOGS" "$R2"
fail() { echo "NG: $*"; exit 1; }
run_prune() {
  MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy \
  CURL_CMD="$HERE/receiver-curl-stub.sh" FAKE_R2="$R2" bash "$PRUNE" "$@"
}
days_ago() { python3 -I -c 'import sys,datetime as d;print((d.datetime.now(d.timezone.utc)-d.timedelta(days=float(sys.argv[1]))).strftime("%Y-%m-%dT%H:%M:%SZ"))' "$1"; }
# 片付けの記録を書く。引数は「キー outcome 何日前」の組の並び（日付に bad を渡すと不正な日付になる）
# Writes the resolution records; arguments are "key outcome days-ago" triples (passing bad as the days makes an invalid date)
write_resolutions() {
  local rows=()
  while [ "$#" -gt 0 ]; do
    if [ "$3" = bad ]; then rows+=("$1" "$2" "not-a-date"); else rows+=("$1" "$2" "$(days_ago "$3")"); fi
    shift 3
  done
  mkdir -p "$LOGS/.state"
  python3 -I - "$LOGS/.state/report-resolutions.json" "${rows[@]}" <<'PY'
import json, sys
rows = sys.argv[2:]
resolutions = {rows[i]: {"outcome": rows[i + 1], "resolvedAt": rows[i + 2]} for i in range(0, len(rows), 3)}
json.dump({"resolutions": resolutions}, open(sys.argv[1], "w"))
PY
}

# 記録が無ければ stdout は無言で exit 0、受け口にも触らない
# Without records stdout stays silent, the exit code is 0 and the receiver is never touched
run_prune > "$TMP/out1.txt" 2> "$TMP/err1.txt" || fail "記録無しで失敗: $(cat "$TMP/err1.txt")"
[ ! -s "$TMP/out1.txt" ] && [ ! -e "$R2/calls.log" ] || fail "記録無しなのに出力か問い合わせがある"

# 7日以上は一覧に出し、7日未満は出さない。既定は消さない
# 7+ days old are listed, younger ones are not; the default deletes nothing
write_resolutions report/7656001/old_merged merged 8 progress/7656002/old_rejected rejected 30 report/7656003/young merged 6.9
run_prune > "$TMP/out2.txt" 2> "$TMP/err2.txt" || fail "一覧が失敗: $(cat "$TMP/err2.txt")"
grep -q '^削除対象: report/7656001/old_merged（merged・片付け ' "$TMP/out2.txt" || fail "8日前の報告が一覧に無い: $(cat "$TMP/out2.txt")"
grep -q '^削除対象: progress/7656002/old_rejected（rejected' "$TMP/out2.txt" || fail "30日前の進行記録が一覧に無い"
grep -q 'young' "$TMP/out2.txt" && fail "7日未満の報告が一覧に出た"
grep -q -- '--execute' "$TMP/out2.txt" || fail "一覧だけである旨が無い"
[ ! -e "$R2/calls.log" ] && [ ! -e "$LOGS/.state/r2-purges.json" ] || fail "一覧だけなのに消した"

# 形の不正な記録は stderr に出して飛ばし、読めた分の一覧は続ける（終了コードは 1）
# Malformed records are reported on stderr and skipped while the readable ones are still listed (exit code 1)
write_resolutions report/7656001/old_merged merged 8 "report/../escape" merged 9 report/7656004/badoutcome fixed 9 \
  report/7656005/baddate merged bad other/7656006/badkind merged 9
rc=0; run_prune > "$TMP/out3.txt" 2> "$TMP/err3.txt" || rc=$?
[ "$rc" -eq 1 ] || fail "不正な記録があるのに終了コードが $rc"
grep -q 'old_merged' "$TMP/out3.txt" || fail "不正な記録があると正しい記録まで出なくなった"
for key in "report/../escape" badoutcome baddate badkind; do
  grep -qF "$key" "$TMP/err3.txt" || fail "不正な記録 $key が stderr に無い: $(cat "$TMP/err3.txt")"
  grep -qF "$key" "$TMP/out3.txt" && fail "不正な記録 $key が一覧に出た"
done

# --execute は対象ごとに理由付きの DELETE を呼び、r2-purges.json に記録する。削除に失敗した箱は記録しない
# --execute calls DELETE with a reason per target and records it in r2-purges.json; a failed deletion is not recorded
write_resolutions report/7656001/old_merged merged 8 progress/7656002/old_rejected rejected 30 report/7656003/young merged 6.9 \
  report/7656007/refused merged 10
rc=0; FAKE_DELETE_FAIL=report/7656007/refused run_prune --execute > "$TMP/out4.txt" 2> "$TMP/err4.txt" || rc=$?
[ "$rc" -eq 1 ] || fail "削除失敗があるのに終了コードが $rc"
grep -q '^R2 から消した: report/7656001/old_merged {"purged":true' "$TMP/out4.txt" || fail "消した報告が出ていない: $(cat "$TMP/out4.txt")"
grep -q '^R2 から消した: progress/7656002/old_rejected' "$TMP/out4.txt" || fail "進行記録を消していない"
grep -q '^消せなかった: report/7656007/refused' "$TMP/out4.txt" && grep -q 'status=409' "$TMP/err4.txt" || fail "削除失敗が理由付きで出ていない"
grep -qxF 'DELETE report/7656001/old_merged key=dummy' "$R2/calls.log" || fail "DELETE が管理キー付きで呼ばれていない"
grep -q '^report/7656001/old_merged {"reason": "settled:merged at ' "$R2/deleted.txt" || fail "DELETE の本文に理由が無い: $(cat "$R2/deleted.txt")"
grep -q 'young' "$R2/calls.log" && fail "7日未満の報告を消した"
python3 -I - "$LOGS/.state/r2-purges.json" <<'PY' || fail "r2-purges.json の記録が違う"
import json, sys
purged = json.load(open(sys.argv[1]))["purged"]
assert sorted(purged) == ["progress/7656002/old_rejected", "report/7656001/old_merged"], purged
assert purged["report/7656001/old_merged"]["outcome"] == "merged" and purged["report/7656001/old_merged"]["purgedAt"], purged
PY

# 次回は消した物を出さず、消せなかった物だけが残る
# The next run skips what was purged and only the refused box remains
run_prune > "$TMP/out5.txt" 2> /dev/null || fail "2回目の一覧が失敗"
[ "$(grep -c '^削除対象:' "$TMP/out5.txt")" -eq 1 ] && grep -q 'refused' "$TMP/out5.txt" || fail "消した物が再び出た: $(cat "$TMP/out5.txt")"

# 片付けの記録が壊れていれば何も消さず exit 1。logs が無い・引数違いも exit 1
# A broken resolutions file deletes nothing and exits 1; a missing logs dir or a wrong argument also exits 1
echo '{broken' > "$LOGS/.state/report-resolutions.json"; : > "$R2/calls.log"
rc=0; run_prune --execute > /dev/null 2> "$TMP/err6.txt" || rc=$?
[ "$rc" -eq 1 ] && grep -q '何も消さない' "$TMP/err6.txt" && [ ! -s "$R2/calls.log" ] || fail "壊れた記録の扱いが違う（rc=${rc}）"
rc=0; MOORESTECH_LOGS="$TMP/none" PLAYTEST_ENV_FILE=/dev/null bash "$PRUNE" > /dev/null 2>&1 || rc=$?
[ "$rc" -eq 1 ] || fail "logs が無いのに終了コードが $rc"
rc=0; run_prune --force > /dev/null 2>&1 || rc=$?
[ "$rc" -eq 1 ] || fail "未知の引数で終了コードが $rc"
echo OK

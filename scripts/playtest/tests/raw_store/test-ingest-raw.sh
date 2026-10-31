#!/usr/bin/env bash
# 取り込みが生データを logs repo の外へ分けること、投入・一覧が生データ側を読み、期限切れ・欠落で理由を出して止まることを検証する
# Verifies ingest splits raw data out of the logs repo, and that enqueue/listing read the raw side and stop with a reason when expired or missing
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
# shellcheck source=../ingest-fixture.sh
. "$HERE/ingest-fixture.sh"
ng() { echo "NG: $*"; exit 1; }

B=report/7656001/20261020_100000_bug1
RAWFILES=(snapshots/tick_1.json snapshots/packets_2.bin world/world.json video.mp4 frames/0001.png recording/pid_1/a.bin
  __attachments/000000/chunk repo/commits.bundle repo/untracked/a.cs crashDumps/crash.dmp mystery.bin)
KEEPFILES=(manifest.json screenshot.png frames.tsv logs/unity.log repo/head.diff repo/untracked.txt)
mk_object "$B/manifest.json" '{"kind":"bug","steamId":"7656001","description":"止まる","remoteExec":{"state":"Disabled","ledgerFiles":[]}}'
for f in "${RAWFILES[@]}" "${KEEPFILES[@]:1}"; do mk_object "$B/$f" "body-$f"; done
# 許可リストの物でも 10MB を超えるログは生データへ回る
# Even an allowlisted log goes raw once it exceeds 10MB
python3 -c 'import sys; open(sys.argv[1], "wb").write(b"x" * 11_000_000)' "$R2/objects/$B/logs/huge.log"
FILES_JSON="$(python3 -c 'import json,sys; print(json.dumps(sys.argv[1:]))' "${RAWFILES[@]}" "${KEEPFILES[@]}" logs/huge.log)"
mk_ready "$B" "$FILES_JSON"
mk_object progress/7656001/20261020_110000_pg1/record.json '{"schemaVersion":1,"steamId":"7656001","playSeconds":60}'
mk_ready progress/7656001/20261020_110000_pg1 '["record.json"]'
cat > "$R2/inbox.json" <<'JSON'
{"items":[{"kind":"report","steamId":"7656001","id":"20261020_100000_bug1","readyAt":"2026-10-20T01:00:00Z"},
 {"kind":"progress","steamId":"7656001","id":"20261020_110000_pg1","readyAt":"2026-10-20T02:00:00Z"}],"cursor":null}
JSON

# 生データ置き場が無ければ受け口に触らず止まる（ack しない）
# Without the raw store, stop before touching the receiver (no ack)
rmdir "$RAWSTORE"
if run_ingest > "$TMP/noraw.log" 2>&1; then ng "生データ置き場が無いのに正常終了した"; fi
grep -q '生データ置き場が無い' "$TMP/noraw.log" || ng "置き場が無い理由が出ていない"
[ ! -f "$R2/acked.txt" ] || ng "置き場が無いのに ack した"
mkdir -p "$RAWSTORE"

# 前回 logs 側の公開まで届かなかった生データの残りは置き換えられる
# A raw-side leftover from a run that never published the logs side gets replaced
mkdir -p "$RAWSTORE/reports/7656001/20261020_100000_bug1"; echo stale > "$RAWSTORE/reports/7656001/20261020_100000_bug1/stale.txt"
run_ingest > "$TMP/ingest.log" 2>&1 || { cat "$TMP/ingest.log"; ng "取り込みが失敗した"; }
grep -q '置き換える' "$TMP/ingest.log" || ng "残骸の置き換えを知らせていない"

BOX="$LOGS/harness/playtest/reports/7656001/20261020_100000_bug1"
RAWBOX="$RAWSTORE/reports/7656001/20261020_100000_bug1"
[ ! -e "$RAWBOX/stale.txt" ] || ng "古い残骸が残った"
for f in "${KEEPFILES[@]}" READY ingest.json raw.json; do [ -f "$BOX/$f" ] || ng "logs 側に $f が無い"; done
for f in "${RAWFILES[@]}" logs/huge.log; do
  [ ! -e "$BOX/$f" ] || ng "logs 側に生データ $f が残った"
  [ "$(head -c 5 "$RAWBOX/$f")" = "$(head -c 5 "$R2/objects/$B/$f")" ] || ng "生データ側に $f が無い"
done
git -C "$LOGS" ls-files harness/playtest > "$TMP/tracked.txt"
grep -qE 'snapshots/|world/|video.mp4|huge.log|mystery.bin' "$TMP/tracked.txt" && ng "生データが git に入った"
grep -q '20261020_100000_bug1/raw.json' "$TMP/tracked.txt" || ng "raw.json が git に入っていない"
python3 - "$BOX/raw.json" "$RAWBOX/raw.json" "${#RAWFILES[@]}" <<'PY' || ng "raw.json の中身が違う"
import json, sys
ref = json.load(open(sys.argv[1]))
assert ref == json.load(open(sys.argv[2])), "両側の raw.json が違う"
assert ref["rawRelative"] == "reports/7656001/20261020_100000_bug1", ref
assert ref["retentionDays"] == 60 and len(ref["files"]) == int(sys.argv[3]) + 1, ref
from datetime import datetime, timedelta
parse = lambda t: datetime.strptime(t, "%Y-%m-%dT%H:%M:%SZ")
assert parse(ref["expiresAt"]) - parse(ref["ingestedAt"]) == timedelta(days=60), ref
PY
[ ! -e "$LOGS/harness/playtest/progress/7656001/20261020_110000_pg1/raw.json" ] || ng "生データの無い進行記録に raw.json が付いた"
[ ! -e "$RAWSTORE/progress" ] || ng "生データの無い進行記録が生データ側に置かれた"
[ -z "$(find "$RAWSTORE" "$LOGS/harness" -name '*.partial')" ] || ng "下書きが残った"

# 投入は生データを inbox の箱へ揃える
# Enqueue brings the raw data into the inbox box
enqueue() { MOORESTECH_LOGS="$LOGS" MOORESTECH_PLAYTEST_RAW="$RAWSTORE" bash "$HERE/../enqueue-autofix.sh" 7656001 20261020_100000_bug1; }
enqueue 2> "$TMP/enq.log" || { cat "$TMP/enq.log"; ng "投入できない"; }
I="$LOGS/harness/bug-report/inbox/20261020_100000_bug1"
[ -f "$I/snapshots/tick_1.json" ] && [ -f "$I/world/world.json" ] && [ -f "$I/manifest.json" ] || ng "inbox の箱に生データが揃っていない"

# 期限前に生データが欠けたら、理由を出して投入しない
# Raw data missing before its expiry makes enqueue refuse with a reason
rm -rf "$I" "$BOX/AUTOFIX_QUEUED" "$RAWBOX/snapshots/tick_1.json"
set +e; enqueue 2> "$TMP/missing.log"; code=$?; set -e
[ "$code" = 7 ] && [ ! -e "$I" ] || ng "欠落した生データで投入した（exit=$code）"
grep -q 'missing' "$TMP/missing.log" && grep -q '欠けている' "$TMP/missing.log" || ng "欠落の理由が出ていない"

# 期限切れで消えた生データは期限切れとして示す（投入しない・一覧に理由が出る）
# Raw data deleted after expiry shows as expired (no enqueue, reason in the listing)
rm -rf "$RAWBOX"
python3 - "$BOX/raw.json" <<'PY'
import json, sys
ref = json.load(open(sys.argv[1])); ref["expiresAt"] = "2026-01-01T00:00:00Z"
open(sys.argv[1], "w").write(json.dumps(ref))
PY
set +e; enqueue 2> "$TMP/expired.log"; code=$?; set -e
[ "$code" = 7 ] || ng "期限切れの生データで投入した（exit=$code）"
grep -q '期限 2026-01-01T00:00:00Z を過ぎて削除済み' "$TMP/expired.log" || ng "期限切れの理由が出ていない"
MOORESTECH_LOGS="$LOGS" MOORESTECH_PLAYTEST_RAW="$RAWSTORE" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY= \
  bash "$HERE/../find-reports.sh" > "$TMP/find.log" 2>&1 || true
grep -q 'raw=expired' "$TMP/find.log" && grep -q '^生データ: 生データは期限' "$TMP/find.log" || { cat "$TMP/find.log"; ng "一覧に期限切れが出ていない"; }
echo OK

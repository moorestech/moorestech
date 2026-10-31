#!/usr/bin/env bash
# 書き出し zip の取り込みを、偽の logs repo・偽の受け口と R2（curl スタブ）・固定した Steam 応答で検証する（本物の logs repo には触れない）
# Verifies importing an export zip against a fake logs repo, a fake receiver and R2 (curl stub) and a pinned Steam response
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
# shellcheck source=ingest-fixture.sh
. "$HERE/ingest-fixture.sh"

IMPORT_DIR="$TMP/import"; mkdir -p "$IMPORT_DIR"
run_import() {
  MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null STEAM_WEB_API_KEY=dummy STEAM_CURL_CMD=steam_stub_curl \
  PLAYTEST_ADMIN_KEY=dummy CURL_CMD="$HERE/lib/r2-import-curl-stub.sh" FAKE_IMPORT_DIR="$IMPORT_DIR" \
  GIT_PUSH=0 TMPDIR="$ISOLATED_TMPDIR" bash "$HERE/../import-export-zip.sh" "$1"
}

# python で zip を作る: 引数は出力先と、追加したい不正エントリ名（任意）
# Builds the zip with python: output path, plus an optional malicious entry name
make_zip() {
  python3 -I - "$1" "${2:-}" <<'PY'
import json, sys, zipfile
out, evil = sys.argv[1], sys.argv[2]
boxes = [{"kind": "reports", "id": "20261019_150326_bug1", "state": "failed"},
         {"kind": "progress", "id": "20261019_160000_pg1", "state": "pending"},
         {"kind": "reports", "id": "20261019_170000_dev1", "state": "pending"}]
with zipfile.ZipFile(out, "w") as z:
    z.writestr("reports/20261019_150326_bug1/manifest.json", '{"kind":"bug","steamId":"7656001"}')
    z.writestr("reports/20261019_150326_bug1/video.mp4", "MP4")
    z.writestr("reports/20261019_150326_bug1/READY", "{}")
    z.writestr("reports/20261019_150326_bug1/UPLOAD_FAILED", "reason-x")
    z.writestr("progress/20261019_160000_pg1/record.json", '{"steamId":"7656001"}')
    z.writestr("progress/20261019_160000_pg1/READY", "{}")
    z.writestr("reports/20261019_170000_dev1/manifest.json", '{"kind":"bug"}')
    z.writestr("reports/20261019_170000_dev1/READY", "{}")
    z.writestr("export.json", json.dumps({"schemaVersion": 1, "exportedAt": "2026-10-19T17:00:00Z", "boxes": boxes, "missing": []}))
    if evil:
        z.writestr(evil, "x")
PY
}

P="$LOGS/harness/playtest"
BUG="$P/reports/7656001/20261019_150326_bug1"
make_zip "$TMP/export.zip"
rc=0; run_import "$TMP/export.zip" > "$TMP/run1.log" 2>&1 || rc=$?
[ "$rc" -eq 1 ] || { echo "NG: steamId 無しの箱があるのに終了コードが $rc"; exit 1; }
# logs には小物と remote.json だけが残り、動画は R2 へ上がって remote.json に並ぶ
# Only small files and remote.json stay in logs; the video goes up to R2 and is listed in remote.json
( cd "$BUG" && find . -type f | sed 's|^\./||' | LC_ALL=C sort | tr '\n' ' ' ) > "$TMP/bug-files.txt"
[ "$(cat "$TMP/bug-files.txt")" = "READY export-markers/READY export-markers/UPLOAD_FAILED ingest.json manifest.json remote.json " ] \
  || { echo "NG: logs の箱に小物以外が残った: $(cat "$TMP/bug-files.txt")"; exit 1; }
python3 -c 'import json,sys; r=json.load(open(sys.argv[1])); assert r["files"] == ["video.mp4"], r' "$BUG/remote.json" \
  || { echo "NG: remote.json に動画が並んでいない"; exit 1; }
[ "$(cat "$IMPORT_DIR/r2/video.mp4")" = MP4 ] || { echo "NG: 動画が R2 へ上がっていない"; exit 1; }
# 箱の全ファイルが If-None-Match: * 付きで PUT され、import は管理キー付きで呼ばれる
# Every file of the box is PUT with If-None-Match: *, and the import is called with the admin key
grep -qxF 'POST inbox/report/7656001/20261019_150326_bug1/import key=dummy' "$IMPORT_DIR/calls.log" || { echo "NG: import が呼ばれていない"; exit 1; }
grep -qxF 'POST inbox/progress/7656001/20261019_160000_pg1/import key=dummy' "$IMPORT_DIR/calls.log" || { echo "NG: 進行記録の import が無い"; exit 1; }
sed -n 's/^PUT \(.*\) inm=yes$/\1/p' "$IMPORT_DIR/calls.log" | LC_ALL=C sort | tr '\n' ' ' > "$TMP/puts.txt"
[ "$(cat "$TMP/puts.txt")" = "READY READY export-markers/READY export-markers/READY export-markers/UPLOAD_FAILED manifest.json record.json video.mp4 " ] \
  || { echo "NG: PUT された物が箱の全ファイルと違う: $(cat "$TMP/puts.txt") / $(cat "$IMPORT_DIR/calls.log")"; exit 1; }
grep -q 'inm=no' "$IMPORT_DIR/calls.log" && { echo "NG: If-None-Match の無い PUT がある"; exit 1; }
grep -q 'UNEXPECTED' "$IMPORT_DIR/calls.log" && { echo "NG: 想定外の curl 呼び出し: $(cat "$IMPORT_DIR/calls.log")"; exit 1; }
grep -q '20261019_170000_dev1' "$IMPORT_DIR/calls.log" && { echo "NG: steamId 無しの箱を R2 へ上げようとした"; exit 1; }
[ ! -e "$BUG/UPLOAD_FAILED" ] || { echo "NG: 失敗印が箱直下に残った"; exit 1; }
[ "$(cat "$BUG/export-markers/UPLOAD_FAILED")" = reason-x ] || { echo "NG: 失敗理由が読めない"; exit 1; }
python3 -I - "$BUG/READY" <<'PY' || { echo "NG: READY の形が違う"; exit 1; }
import json, sys
r = json.load(open(sys.argv[1]))
assert sorted(r["files"]) == ["manifest.json", "video.mp4"], r
assert r["fileCount"] == 2 and r["kind"] == "report" and r["importedFrom"] == "export-zip" and r["skipped"] == [], r
assert json.loads(r["manifest"])["steamId"] == "7656001"
PY
grep -q '"readyAt":"2026-10-19T17:00:00Z"' "$BUG/ingest.json" || { echo "NG: readyAt に exportedAt が入っていない"; exit 1; }
[ -f "$P/progress/7656001/20261019_160000_pg1/record.json" ] && [ -f "$P/progress/7656001/20261019_160000_pg1/ingest.json" ] \
  || { echo "NG: 進行記録が置かれていない"; exit 1; }
[ -z "$(find "$P" -name '20261019_170000_dev1*')" ] || { echo "NG: steamId 無しの箱が置かれた"; exit 1; }
grep -q 'steamId' "$TMP/run1.log" || { echo "NG: steamId の理由がログに無い"; exit 1; }
[ -z "$(find "$P" -name '*.partial')" ] || { echo "NG: partial が残った"; exit 1; }
git -C "$LOGS" log --oneline > "$TMP/gitlog.txt"
grep -q 'playtest: import export zip export.zip' "$TMP/gitlog.txt" || { echo "NG: logs repo への commit が無い"; exit 1; }

# 再実行: 既存の2箱は取り込み済みで飛ばされ、内容は変わらない
# Re-run: the two existing boxes are skipped as already imported and stay unchanged
BEFORE="$(cat "$BUG/ingest.json")"
CALLS_BEFORE="$(wc -l < "$IMPORT_DIR/calls.log")"
sleep 1
rc=0; run_import "$TMP/export.zip" > "$TMP/run2.log" 2>&1 || rc=$?
[ "$rc" -eq 1 ] || { echo "NG: 2回目の終了コードが $rc"; exit 1; }
[ "$(grep -c '取り込み済み。' "$TMP/run2.log")" -eq 2 ] || { echo "NG: 取り込み済みで飛ばされていない"; cat "$TMP/run2.log"; exit 1; }
[ "$BEFORE" = "$(cat "$BUG/ingest.json")" ] || { echo "NG: 再実行で内容が変わった"; exit 1; }
[ "$CALLS_BEFORE" = "$(wc -l < "$IMPORT_DIR/calls.log")" ] || { echo "NG: 取り込み済みの箱を R2 へ上げ直した"; exit 1; }

# R2 に長さ違いの同名がある（conflicts）・PUT が失敗する箱は取り込まない。全部 412（送信済み）なら取り込む
# Boxes with conflicts or a failing PUT are not imported; all-412 (already uploaded) still imports
for mode in conflict putfail exists; do
  rm -rf "$P/reports" "$P/progress"
  rc=0; FAKE_IMPORT_MODE=$mode run_import "$TMP/export.zip" > "$TMP/$mode.log" 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || { echo "NG: $mode の終了コードが $rc"; exit 1; }
  if [ "$mode" = exists ]; then
    [ -f "$BUG/remote.json" ] || { echo "NG: 412（送信済み）なのに取り込まれない: $(cat "$TMP/$mode.log")"; exit 1; }
    continue
  fi
  [ -z "$(find "$P" -name '20261019_150326_bug1*')" ] || { echo "NG: $mode の箱が取り込まれた"; exit 1; }
  [ -z "$(find "$P" -name '*.partial')" ] || { echo "NG: $mode で partial が残った"; exit 1; }
  grep -q 'ERROR: R2' "$TMP/$mode.log" || { echo "NG: $mode の理由がログに無い: $(cat "$TMP/$mode.log")"; exit 1; }
done
grep -q 'R2 に長さの違う同名ファイルがある' "$TMP/conflict.log" || { echo "NG: conflicts の中身がログに無い"; exit 1; }
grep -q 'PUT status=500: .*video.mp4' "$TMP/putfail.log" || { echo "NG: PUT 失敗のステータスがログに無い"; exit 1; }

# find-reports.sh の取り込み済み一覧に出る
# The box shows in find-reports.sh's imported listing
MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null bash "$HERE/../find-reports.sh" > "$TMP/find.log" 2>&1 || true
grep -q '20261019_150326_bug1' "$TMP/find.log" || { echo "NG: find-reports.sh に出ない"; cat "$TMP/find.log"; exit 1; }

# 不正なエントリ名の zip は何も展開せず失敗する
# A zip with a traversal entry extracts nothing and fails
rm -rf "$LOGS/harness/playtest/reports" "$LOGS/harness/playtest/progress"
make_zip "$TMP/evil.zip" "../evil"
rc=0; run_import "$TMP/evil.zip" > "$TMP/evil.log" 2>&1 || rc=$?
[ "$rc" -eq 1 ] || { echo "NG: 不正エントリの zip の終了コードが $rc"; exit 1; }
[ -z "$(find "$P" -type d \( -name reports -o -name progress \))" ] || { echo "NG: 不正 zip なのに箱が置かれた"; exit 1; }
[ ! -e "$TMP/evil" ] || { echo "NG: zip の外へ書かれた"; exit 1; }
grep -q '不正なエントリ名' "$TMP/evil.log" || { echo "NG: 不正エントリの理由が無い"; exit 1; }

# 予約名（remote.json・ingest.json・大文字小文字違いの ready）を含む箱は置かれず、理由がログに出る
# Boxes carrying reserved names (remote.json, ingest.json, case-variant ready) are not placed and the reason is logged
for reserved in remote.json ingest.json ready; do
  rm -rf "$LOGS/harness/playtest/reports" "$LOGS/harness/playtest/progress"
  make_zip "$TMP/res.zip" "reports/20261019_150326_bug1/$reserved"
  rc=0; run_import "$TMP/res.zip" > "$TMP/res.log" 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || { echo "NG: $reserved 入りの終了コードが $rc"; exit 1; }
  [ -z "$(find "$P" -name '20261019_150326_bug1*')" ] || { echo "NG: $reserved 入りの箱が置かれた"; exit 1; }
  grep -qE '予約名|大文字小文字' "$TMP/res.log" || { echo "NG: $reserved の理由がログに無い"; cat "$TMP/res.log"; exit 1; }
done
# MARKER_NAMES が C# の印の定数（PlaytestOutboxScanner の *Marker）と一致する。片方だけ増えると印が資料として取り込まれる
# MARKER_NAMES must equal the C# marker constants; a one-sided addition would import a marker as payload
SCANNER="$HERE/../../../moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/Outbox/PlaytestOutboxScanner.cs"
python3 -I - "$SCANNER" "$HERE/../lib" <<'PY' || { echo "NG: 印の一覧が C# とずれている"; exit 1; }
import re, sys
sys.path.insert(0, sys.argv[2])
import import_export_zip
cs = set(re.findall(r'const string \w+Marker = "([^"]+)"', open(sys.argv[1], encoding="utf-8").read()))
py = set(import_export_zip.MARKER_NAMES)
if cs != py or not cs:
    print(f"C#={sorted(cs)} python={sorted(py)}", file=sys.stderr)
    sys.exit(1)
PY
echo OK

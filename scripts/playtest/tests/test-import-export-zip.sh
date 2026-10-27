#!/usr/bin/env bash
# 書き出し zip の取り込みを、偽の logs repo と固定した Steam 応答で検証する（本物の logs repo には触れない）
# Verifies importing an export zip against a fake logs repo and a pinned Steam response (never touches the real logs repo)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
# shellcheck source=ingest-fixture.sh
. "$HERE/ingest-fixture.sh"

run_import() {
  MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null STEAM_WEB_API_KEY=dummy STEAM_CURL_CMD=steam_stub_curl \
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
for f in manifest.json video.mp4 READY ingest.json export-markers/UPLOAD_FAILED; do
  [ -f "$BUG/$f" ] || { echo "NG: $f が置かれていない"; exit 1; }
done
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
sleep 1
rc=0; run_import "$TMP/export.zip" > "$TMP/run2.log" 2>&1 || rc=$?
[ "$rc" -eq 1 ] || { echo "NG: 2回目の終了コードが $rc"; exit 1; }
[ "$(grep -c '取り込み済み。' "$TMP/run2.log")" -eq 2 ] || { echo "NG: 取り込み済みで飛ばされていない"; cat "$TMP/run2.log"; exit 1; }
[ "$BEFORE" = "$(cat "$BUG/ingest.json")" ] || { echo "NG: 再実行で内容が変わった"; exit 1; }

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

# 予約名（印・run.env・大文字小文字違いの ready）を含む箱は置かれず、理由がログに出る
# Boxes carrying reserved names (markers, run.env, case-variant ready) are not placed and the reason is logged
for reserved in AUTOFIX_QUEUED run.env ready; do
  rm -rf "$LOGS/harness/playtest/reports" "$LOGS/harness/playtest/progress"
  make_zip "$TMP/res.zip" "reports/20261019_150326_bug1/$reserved"
  rc=0; run_import "$TMP/res.zip" > "$TMP/res.log" 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || { echo "NG: $reserved 入りの終了コードが $rc"; exit 1; }
  [ -z "$(find "$P" -name '20261019_150326_bug1*')" ] || { echo "NG: $reserved 入りの箱が置かれた"; exit 1; }
  grep -qE '予約名|大文字小文字' "$TMP/res.log" || { echo "NG: $reserved の理由がログに無い"; cat "$TMP/res.log"; exit 1; }
done
echo OK

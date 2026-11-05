#!/usr/bin/env bash
# 許可リストの判定（box_layout.py select）が非0で終わった箱は、取り込みも ack もせず理由を出すことを検証する
# 本物の scripts を一時ディレクトリへ写し、box_layout.py だけを select で落ちる偽物に差し替える（本体は触らない）
# Verifies a box whose allowlist check (box_layout.py select) exits non-zero is neither ingested nor acked, with the reason logged
# Real scripts are copied to a temp dir and only box_layout.py is swapped for a fake that fails select (the originals are untouched)
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
# shellcheck source=../ingest-fixture.sh
. "$HERE/ingest-fixture.sh"

SANDBOX="$TMP/sandbox/scripts/playtest"
mkdir -p "$SANDBOX"
cp "$HERE/../ingest.sh" "$SANDBOX/" && cp -R "$HERE/../lib" "$HERE/../r2_store" "$SANDBOX/"
cat > "$SANDBOX/r2_store/box_layout.py" <<'PY'
import sys
print("select broke on purpose", file=sys.stderr)
sys.exit(1)
PY

mk_object report/7656001/20260913_100000_bug1/manifest.json '{"kind":"bug","steamId":"7656001","description":"x"}'
mk_ready report/7656001/20260913_100000_bug1 '["manifest.json"]'
printf '%s' '{"items":[{"kind":"report","steamId":"7656001","id":"20260913_100000_bug1","readyAt":"2026-09-13T01:00:00Z"}],"cursor":null}' > "$R2/inbox.json"

MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy \
FAKE_R2="$R2" CURL_CMD="$TMP/curl" STEAM_CURL_CMD=steam_stub_curl \
GIT_PUSH=0 TMPDIR="$ISOLATED_TMPDIR" bash "$SANDBOX/ingest.sh" 2> "$TMP/run.log" || true

grep -q '許可リストの判定に失敗 report/7656001/20260913_100000_bug1（ack しない）' "$TMP/run.log" || { echo "NG: select 失敗の理由が出ない: $(cat "$TMP/run.log")"; exit 1; }
[ ! -e "$R2/acked.txt" ] || { echo "NG: select が落ちた箱が ack された: $(cat "$R2/acked.txt")"; exit 1; }
[ -z "$(find "$LOGS/harness/playtest" -type f)" ] || { echo "NG: select が落ちた箱が置かれた: $(find "$LOGS/harness/playtest" -type f)"; exit 1; }
echo OK

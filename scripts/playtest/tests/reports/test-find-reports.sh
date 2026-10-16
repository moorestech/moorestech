#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")/../.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export LOGS="$WORK/logs" PLAYTEST_ENV_FILE="$WORK/no-env" PLAYTEST_ADMIN_KEY=fake
export CURL_CMD="$WORK/curl" FAKE_ROOT="$WORK"
mkdir -p "$LOGS/harness/playtest/reports/123/older" "$LOGS/harness/playtest/reports/123/newer"
mkdir -p "$LOGS/harness/bug-report/inbox/pending" "$LOGS/harness/bug-report/runs/run1"
for name in older newer; do
  echo '{"steamId":"123","steamPersonaName":"tester"}' > "$LOGS/harness/playtest/reports/123/$name/ingest.json"
  echo '{"kind":"bug","description":"a report"}' > "$LOGS/harness/playtest/reports/123/$name/manifest.json"
done
echo '{"status":"fixed"}' > "$LOGS/harness/bug-report/runs/run1/fix-result.json"
python3 - "$LOGS" <<'PY'
import os,sys
from pathlib import Path
root=Path(sys.argv[1])/'harness/playtest/reports/123'
os.utime(root/'older',(100,100));os.utime(root/'newer',(200,200))
PY
cat > "$CURL_CMD" <<'FAKE'
#!/usr/bin/env bash
set -euo pipefail
out=''; url="${!#}"
while [ "$#" -gt 0 ]; do
  if [ "$1" = -o ]; then out="$2"; shift; fi
  shift
done
if [ -f "$FAKE_ROOT/fail" ]; then echo unavailable > "$out"; printf 500; exit 0; fi
if [[ "$url" == *cursor=* ]]; then
  echo '{"items":[{"kind":"bug","steamId":"dev-mac","id":"remote2","readyAt":"today"}],"cursor":null}' > "$out"
else
  echo '{"items":[{"kind":"bug","steamId":"123","id":"remote1","readyAt":"today"}],"cursor":"page2"}' > "$out"
fi
printf 200
FAKE
chmod +x "$CURL_CMD"
bash "$HERE/find-reports.sh" > "$WORK/result"
python3 - "$WORK/result" <<'PY'
import sys
s=open(sys.argv[1]).read()
for value in ('## 受け口の未取り込み','## 取り込み済み','## 自動修正','remote1','remote2','pending','fixed','tester'):
    assert value in s, value
assert s.index('newer') < s.index('older')
PY
bash "$HERE/find-reports.sh" --limit 1 > "$WORK/limited"
! grep -q older "$WORK/limited"
touch "$WORK/fail"
bash "$HERE/find-reports.sh" > "$WORK/failed"
grep -q 'status=500' "$WORK/failed"
grep -q '## 取り込み済み' "$WORK/failed"
grep -q '## 自動修正' "$WORK/failed"
echo 'PASS find-reports pagination, mtime ordering, limit and receiver failure'

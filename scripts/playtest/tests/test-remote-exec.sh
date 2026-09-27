#!/usr/bin/env bash
# 実際のHTTP入口とトークン不一致を検証する（ポート待受を許可した環境だけで実行）
# Verify real HTTP and wrong-token handling; run only where listening is permitted
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"
PID=''
cleanup() { [ -z "$PID" ] || kill "$PID"; rm -rf "$TMP"; }
trap cleanup EXIT
python3 "$HERE/lib/remote-exec-http-stub.py" "$TMP/access.json" &
PID=$!
for _ in {1..100}; do [ ! -f "$TMP/access.json" ] || break; sleep .05; done
MOORESTECH_REMOTE_EXEC_ACCESS="$TMP/access.json" bash "$HERE/../remote-exec.sh" - <<< 'return 1;' > "$TMP/result"
python3 -c 'import json,sys; assert json.load(open(sys.argv[1]))["result"] == "1"' "$TMP/result"
python3 - "$TMP/access.json" <<'PY'
import json,sys
p=sys.argv[1]
data=json.load(open(p)); data['token']='wrong'
open(p,'w').write(json.dumps(data))
PY
set +e
MOORESTECH_REMOTE_EXEC_ACCESS="$TMP/access.json" bash "$HERE/../remote-exec.sh" - <<< 'return 1;'
status=$?
set -e
[ "$status" = 1 ]
echo OK

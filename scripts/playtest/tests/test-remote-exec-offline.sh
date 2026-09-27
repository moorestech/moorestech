#!/usr/bin/env bash
# ポートを開かず、SSH引数・JSON・HTTPステータスの契約を検証する
# Verify SSH arguments, JSON and HTTP status contracts without opening ports
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
. "$HERE/lib/verify-on-windows-sandbox.sh"
make_sandbox
CLI="$HERE/../remote-exec.sh"
export REX_TEST_ROOT="$SANDBOX"
cat > "$SANDBOX/bin/ssh" <<'PY'
#!/usr/bin/env python3
import base64,json,os,sys
from pathlib import Path
args=sys.argv[1:]
assert args[:3] == ['-o','BatchMode=yes','moores@verify-pc'], args
prefix='powershell -NoProfile -EncodedCommand '
assert args[3].startswith(prefix), args
script=base64.b64decode(args[3][len(prefix):]).decode('utf-16le')
assert 'Invoke-WebRequest' in script and '-MaximumRedirection 0' in script
body=json.load(sys.stdin)
assert body == {'code':'return "日本語";\n','target':'server'},body
Path(os.environ['REX_TEST_ROOT'],'ssh-body.json').write_text(json.dumps(body))
print('{"ok":true,"result":"日本語"}')
PY
chmod +x "$SANDBOX/bin/ssh"
MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores SSH_BIN="$SANDBOX/bin/ssh" \
    bash "$CLI" --windows --target server - <<< 'return "日本語";' > "$SANDBOX/response"
[ -f "$SANDBOX/ssh-body.json" ]
# curlスタブはHTTPを送信せず、ステータスだけを変えて非200の拒否を確かめる
# The curl stub sends no HTTP and varies only status to check every non-200 rejection
cat > "$SANDBOX/bin/curl" <<'PY'
#!/usr/bin/env python3
import json,os,sys
from pathlib import Path
args=sys.argv[1:]
assert 'X-Remote-Exec-Token: dummy' in args
body=json.loads(Path(args[args.index('--data-binary')+1][1:]).read_text())
assert body == {'code':'return 1;\n','target':'client'},body
Path(args[args.index('-o')+1]).write_text('{"ok":true}')
print(os.environ['HTTP_STATUS'],end='')
PY
chmod +x "$SANDBOX/bin/curl"
echo '{"port":12345,"token":"dummy"}' > "$SANDBOX/access.json"
for code in 200 201 302 403 500; do
    status=0
    HTTP_STATUS="$code" PATH="$SANDBOX/bin:$PATH" MOORESTECH_REMOTE_EXEC_ACCESS="$SANDBOX/access.json" \
        bash "$CLI" - <<< 'return 1;' > "$SANDBOX/response" 2> "$SANDBOX/error" || status=$?
    if [ "$code" = 200 ]; then [ "$status" = 0 ]; else [ "$status" = 1 ]; fi
done
for arg in '--target' '--unknown'; do
    status=0
    bash "$CLI" "$arg" </dev/null > /dev/null 2>&1 || status=$?
    [ "$status" = 1 ]
done
echo OK

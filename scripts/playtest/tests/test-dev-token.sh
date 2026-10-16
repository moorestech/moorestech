#!/usr/bin/env bash
# 偽APIで管理操作の契約と開発機の表示を検証する
# Verify administration contracts and developer labels against a fake API
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
cat > "$TMP/curl" <<'PY'
#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
args = sys.argv[1:]
with open(os.environ["CALLS"], "a") as out:
    out.write(json.dumps(args) + "\n")
method = args[args.index("-X") + 1] if "-X" in args else "GET"
response = {"GET": {"machines": [{"machine": "macbook", "createdAt": "2026-10-16T00:00:00Z"}]},
            "POST": {"machine": "macbook", "token": "mdt_" + "a" * 64},
            "DELETE": {"revoked": "macbook"}}[method]
Path(args[args.index("-o") + 1]).write_text(json.dumps(response))
print(os.environ.get("FAKE_STATUS", "200"), end="")
PY
chmod +x "$TMP/curl"
export CALLS="$TMP/calls" CURL_CMD="$TMP/curl" PLAYTEST_ENV_FILE="$TMP/env"
# envファイルが先に接続先へ反映されることも確かめる
# Also ensure the environment file overrides the receiver endpoint before sourcing the API
cat > "$TMP/env" <<'ENV'
export PLAYTEST_ADMIN_KEY=fake-admin
export PLAYTEST_RECEIVER_BASE=https://receiver.invalid
ENV
bash "$HERE/../dev-token.sh" issue macbook > "$TMP/token" 2> "$TMP/issue.err"
[ "$(cat "$TMP/token")" = "mdt_$(printf '%064d' 0 | tr 0 a)" ]
[ "$(wc -l < "$TMP/token")" -eq 1 ]
grep -q 'moorestech/Playtest/Developer Token' "$TMP/issue.err"
bash "$HERE/../dev-token.sh" list > "$TMP/list"
printf 'macbook\t2026-10-16T00:00:00Z\n' > "$TMP/expected"
cmp "$TMP/list" "$TMP/expected"
bash "$HERE/../dev-token.sh" revoke macbook > "$TMP/revoke"
[ ! -s "$TMP/revoke" ]
python3 - "$CALLS" <<'PY'
import json,sys
calls = [json.loads(line) for line in open(sys.argv[1])]
assert len(calls) == 3
for args, method in zip(calls, ["POST", "GET", "DELETE"]):
    assert "X-Admin-Key: fake-admin" in args
    actual = args[args.index("-X") + 1] if "-X" in args else "GET"
    assert actual == method
    assert args[-1] == "https://receiver.invalid/v1/admin/dev-tokens" + ("/macbook" if method == "DELETE" else "")
    if method == "POST":
        assert json.loads(args[args.index("--data") + 1]) == {"machine": "macbook"}
PY
# 不正な引数では通信せず、HTTP失敗を終了コード1へそろえる
# Reject invalid arguments without network calls and map HTTP failures to exit code one
for name in '' Macbook '-mac' 'a/b' 'a b' 'abcdefghijklmnopqrstuvwxyz1234567'; do
  for command in issue revoke; do
    rc=0
    bash "$HERE/../dev-token.sh" "$command" "$name" > "$TMP/out" 2> "$TMP/error" || rc=$?
    [ "$rc" -eq 1 ]
    [ -s "$TMP/error" ]
  done
done
[ "$(wc -l < "$CALLS")" -eq 3 ]
rc=0
FAKE_STATUS=401 bash "$HERE/../dev-token.sh" issue macbook > "$TMP/out" 2> "$TMP/error" || rc=$?
[ "$rc" -eq 1 ]
[ ! -s "$TMP/out" ]
grep -q 'status=401' "$TMP/error"
# 開発機はキャッシュもSteamの鍵も不要で、Steam呼び出しを行わない
# Developer labels require neither a cache nor a Steam key and never call Steam
(
  HERE="$(cd "$HERE/.." && pwd)"
  log() { echo "$*" >&2; }
  unset STEAM_PERSONA_CACHE_DIR STEAM_WEB_API_KEY
  STEAM_CURL_CMD=/does-not-exist
  . "$HERE/lib/steam-persona.sh"
  steam_persona_resolve dev-macbook "$TMP/persona.json"
)
python3 - "$TMP/persona.json" <<'PY'
import json, sys
assert json.load(open(sys.argv[1])) == {"steamPersonaName": "開発機 macbook", "steamProfileUrl": "", "steamPersonaMissing": ""}
PY
echo OK

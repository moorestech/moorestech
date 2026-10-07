#!/usr/bin/env bash
# C#メソッド本体を起動中のゲームへ送り、応答JSONを出す
# Send a C# method body to a running game and print its JSON response
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
WINDOWS=0
TARGET=client
SRC=-
HAS_SOURCE=0
usage() {
    echo 'usage: remote-exec.sh [--windows] [--target client|server] [FILE|-]'
    echo 'server: only the synchronous part runs on the server thread.'
    echo 'Helpers must be static local functions; top-level type declarations are unsupported.'
}
fail() { echo "ERROR: $*" >&2; exit 1; }
while [ $# -gt 0 ]; do
    case "$1" in
        --windows) WINDOWS=1 ;;
        --target)
            [ $# -ge 2 ] || fail '--target requires client or server'
            TARGET="$2"; shift ;;
        -h|--help) usage; exit 0 ;;
        -) [ "$HAS_SOURCE" = 0 ] || fail 'multiple sources'; HAS_SOURCE=1; SRC=- ;;
        --*) fail "unknown option: $1" ;;
        *) [ "$HAS_SOURCE" = 0 ] || fail 'multiple sources'; HAS_SOURCE=1; SRC="$1" ;;
    esac
    shift
done
case "$TARGET" in client|server) ;; *) fail "unknown target: $TARGET" ;; esac

# ソースはstdin経由でJSON化し、長い本文もOSの引数長制限に掛けない
# Encode source via stdin to avoid the OS argument-size limit for long bodies
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
cat -- "$SRC" | python3 -c 'import json,sys; json.dump({"code":sys.stdin.read(),"target":sys.argv[1]},sys.stdout)' "$TARGET" > "$TMP/body"
if [ "$WINDOWS" = 1 ]; then
    # cmd.exeの引用符・パイプ解釈を避け、PowerShellだけに本文を解釈させる
    # Avoid cmd.exe quote/pipe interpretation by encoding the PowerShell script
    . "$HERE/lib/verify-ssh.sh"
    ENCODED="$(python3 -c 'import base64,pathlib,sys; print(base64.b64encode(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8").encode("utf-16le")).decode())' "$HERE/windows/remote-exec.ps1")"
    verify_ssh_remote "powershell -NoProfile -EncodedCommand $ENCODED" < "$TMP/body"
    exit 0
fi

# HOME差し替え環境でもゲームと同じユーザーディレクトリを参照する
# Use the game user directory even when a supervisor replaces HOME
# C# の GameSystemPaths.RemoteExecDirectory にある access.json と同じ場所
# Matches access.json in the C# GameSystemPaths.RemoteExecDirectory
ACCESS="${MOORESTECH_REMOTE_EXEC_ACCESS:-/Users/$(id -un)/Library/Application Support/moorestech/RemoteExec/access.json}"
python3 "$HERE/lib/remote-exec-access.py" "$ACCESS" > "$TMP/access" || exit 1
PORT="$(head -n 1 "$TMP/access")"
TOKEN="$(tail -n 1 "$TMP/access")"
STATUS="$(curl -sS -o "$TMP/response" -w '%{http_code}' -X POST "http://127.0.0.1:${PORT}/api/remote-exec" \
    -H "X-Remote-Exec-Token: ${TOKEN}" -H 'Content-Type: application/json' --data-binary "@$TMP/body")" || exit 1
cat "$TMP/response"
[ "$STATUS" = 200 ] || fail "HTTP $STATUS"
if ! python3 -c 'import json,sys; outcome=json.load(open(sys.argv[1],encoding="utf-8")).get("outcome"); sys.exit(0 if outcome=="Succeeded" else 1)' "$TMP/response"; then
    echo 'ERROR: execution outcome was not Succeeded' >&2
    exit 2
fi

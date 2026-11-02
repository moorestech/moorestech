#!/usr/bin/env bash
# ダッシュボードの起動ラッパー bin/run.sh が env.sh を source し、受け口の URL と管理キーを環境変数で server.py へ渡すことを検証する
# python3 は PATH 先頭の偽物に差し替え、受け取った環境変数と引数だけを書き出させる（サーバーは起動しない）
# Verifies bin/run.sh sources env.sh and hands the receiver URL and admin key to server.py as env vars
# python3 is swapped for a fake at the front of PATH that only records the env and args it got (no server starts)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
RUN="$HERE/../../dashboard/bin/run.sh"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
mkdir -p "$TMP/bin"
cat > "$TMP/bin/python3" <<'PY'
#!/usr/bin/env bash
printf 'base=%s key=%s args=%s\n' "${PLAYTEST_RECEIVER_BASE-unset}" "${PLAYTEST_ADMIN_KEY-unset}" "$*" > "$FAKE_OUT"
PY
chmod +x "$TMP/bin/python3"

run() { env -u PLAYTEST_ADMIN_KEY -u PLAYTEST_RECEIVER_BASE PATH="$TMP/bin:$PATH" FAKE_OUT="$TMP/out.txt" "$@"; }

# env.sh の値が環境変数で渡り、引数はそのまま server.py へ届く
# env.sh values arrive as env vars and arguments pass through to server.py unchanged
printf 'export PLAYTEST_ADMIN_KEY="$SECRET_SOURCE"\nexport PLAYTEST_RECEIVER_BASE=http://receiver.invalid\n' > "$TMP/env.sh"
run SECRET_SOURCE=k1 PLAYTEST_ENV_FILE="$TMP/env.sh" bash "$RUN" --port 9999 2> "$TMP/err.txt"
grep -q '^base=http://receiver.invalid key=k1 args=.*server.py --port 9999$' "$TMP/out.txt" \
  || { echo "NG: env.sh の値（\$VAR 展開を含む）が渡らない: $(cat "$TMP/out.txt")"; exit 1; }

# env.sh に URL が無ければ receiver-api.sh の既定 URL が渡る（既定は1か所だけ）
# Without a URL in env.sh, receiver-api.sh's default is handed over (the default lives in one place)
printf 'export PLAYTEST_ADMIN_KEY=k2\n' > "$TMP/env.sh"
run PLAYTEST_ENV_FILE="$TMP/env.sh" bash "$RUN" 2> "$TMP/err.txt"
grep -q '^base=https://playtest.moores.tech key=k2 ' "$TMP/out.txt" || { echo "NG: 既定 URL が渡らない: $(cat "$TMP/out.txt")"; exit 1; }

# env.sh が無ければ理由を出し、鍵は空のまま起動する（server.py が起動ログに欠落を出し中継は 503）
# Without env.sh the reason is logged and the key stays empty (server.py logs it at startup and relays answer 503)
run PLAYTEST_ENV_FILE="$TMP/absent.sh" bash "$RUN" 2> "$TMP/err.txt"
grep -q '^base=https://playtest.moores.tech key= ' "$TMP/out.txt" || { echo "NG: env.sh 無しで鍵が空にならない: $(cat "$TMP/out.txt")"; exit 1; }
grep -q 'env file が無い' "$TMP/err.txt" || { echo "NG: env.sh が無い理由が出ない: $(cat "$TMP/err.txt")"; exit 1; }
echo OK

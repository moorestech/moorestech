#!/usr/bin/env bash
# allowlist.sh を curl スタブで検証する。スタブはJSONファイルを許可リストの実体として使う
# Verifies allowlist.sh with a curl stub that keeps the allowlist in a JSON file
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo '{"steamIds":[]}' > "$TMP/state.json"

# curl スタブ: -X PUT なら state.json へ書き込み、そうでなければ返す（末尾に改行区切りでstatus付与）
# curl stub: -X PUT writes state.json, otherwise it returns it (status appended after a newline)
cat > "$TMP/curl" <<'SH'
#!/usr/bin/env bash
state="$STATE_FILE"
method=GET; data=""
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2;;
    --data) data="$2"; shift 2;;
    *) shift;;
  esac
done
if [ "$method" = "PUT" ]; then printf '%s' "$data" > "$state"; printf '%s\n200' "$data"; else printf '%s\n200' "$(cat "$state")"; fi
SH
chmod +x "$TMP/curl"

cat > "$TMP/env.sh" <<'SH'
PLAYTEST_RECEIVER_BASE=https://example.invalid
PLAYTEST_ADMIN_KEY=dummy-admin-key
SH

run() { PLAYTEST_ENV_FILE="$TMP/env.sh" CURL_CMD="$TMP/curl" STATE_FILE="$TMP/state.json" bash "$HERE/../allowlist.sh" "$@"; }

run add 76561198000000001 >/dev/null
grep -q '76561198000000001' "$TMP/state.json" || { echo "NG: addで追加されていない"; exit 1; }

run add 76561198000000001 >/dev/null
count="$(python3 -c "import json,sys;print(len(json.load(open(sys.argv[1]))['steamIds']))" "$TMP/state.json")"
[ "$count" = "1" ] || { echo "NG: 同じIDのaddで重複した ($count)"; exit 1; }

run add 76561198000000002 >/dev/null
[ "$(run list | wc -l | tr -d ' ')" = "2" ] || { echo "NG: listが2行でない"; exit 1; }
run list | grep -qx '76561198000000002' || { echo "NG: listにIDが出ない"; exit 1; }

run remove 76561198000000001 >/dev/null
grep -q '76561198000000001' "$TMP/state.json" && { echo "NG: removeで消えていない"; exit 1; }

# 未設定のenvは即エラー終了する（無音で通さない）
# Missing settings must fail loudly rather than silently proceed
cat > "$TMP/empty-env.sh" <<'SH'
PLAYTEST_RECEIVER_BASE=https://example.invalid
SH
if PLAYTEST_ENV_FILE="$TMP/empty-env.sh" CURL_CMD="$TMP/curl" STATE_FILE="$TMP/state.json" bash "$HERE/../allowlist.sh" list >/dev/null 2>&1; then
  echo "NG: PLAYTEST_ADMIN_KEY 未設定でも動いてしまった"; exit 1
fi

# 引数不足・未知のサブコマンドも失敗する
# Missing arguments and unknown subcommands must fail too
if run add >/dev/null 2>&1; then echo "NG: steamId 無しの add が通った"; exit 1; fi
if run nope >/dev/null 2>&1; then echo "NG: 未知のサブコマンドが通った"; exit 1; fi

# curl スタブが401を返すと非0終了し、stderrにstatusが出る（無音で通さない）
# A curl stub returning 401 must fail loudly, with the status logged to stderr
cat > "$TMP/curl-401" <<'SH'
#!/usr/bin/env bash
printf '{"error":"unauthorized"}\n401'
SH
chmod +x "$TMP/curl-401"
if PLAYTEST_ENV_FILE="$TMP/env.sh" CURL_CMD="$TMP/curl-401" STATE_FILE="$TMP/state.json" bash "$HERE/../allowlist.sh" list >/dev/null 2>"$TMP/401.log"; then
  echo "NG: curlが401を返しても成功してしまった"; exit 1
fi
grep -q '401' "$TMP/401.log" || { echo "NG: 401がstderrに出ていない"; exit 1; }

# GETが1回目失敗し2回目以降成功するスタブでも、addは1回目の失敗で止まりPUTへ進まない（回帰: 偽成功でexit 0だった）
# Even if GET fails first then would succeed, add must stop at the first failure (regression: used to exit 0)
echo '{"steamIds":["76561198000000003"]}' > "$TMP/flaky-state.json"
rm -f "$TMP/flaky-count"
cat > "$TMP/curl-flaky" <<'SH'
#!/usr/bin/env bash
state="$STATE_FILE"
count_file="$FLAKY_COUNT_FILE"
n=0
[ -f "$count_file" ] && n="$(cat "$count_file")"
n=$((n + 1))
echo "$n" > "$count_file"
method=GET; data=""
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2;;
    --data) data="$2"; shift 2;;
    *) shift;;
  esac
done
if [ "$n" = "1" ]; then printf '{"error":"unauthorized"}\n401'; exit 0; fi
if [ "$method" = "PUT" ]; then printf '%s' "$data" > "$state"; printf '%s\n200' "$data"; else printf '%s\n200' "$(cat "$state")"; fi
SH
chmod +x "$TMP/curl-flaky"
before="$(cat "$TMP/flaky-state.json")"
if PLAYTEST_ENV_FILE="$TMP/env.sh" CURL_CMD="$TMP/curl-flaky" STATE_FILE="$TMP/flaky-state.json" FLAKY_COUNT_FILE="$TMP/flaky-count" \
   bash "$HERE/../allowlist.sh" add 76561198000000099 >/dev/null 2>"$TMP/flaky.log"; then
  echo "NG: 1回目GET失敗・2回目成功のケースでaddが成功してしまった（偽成功）"; exit 1
fi
after="$(cat "$TMP/flaky-state.json")"
[ "$before" = "$after" ] || { echo "NG: 1回目GET失敗なのにstate.jsonが変わった"; exit 1; }
grep -q 'added: 76561198000000099' "$TMP/flaky.log" && { echo "NG: 実際には失敗しているのにaddedログが出た"; exit 1; }

echo "OK"

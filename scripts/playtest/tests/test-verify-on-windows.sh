#!/usr/bin/env bash
# fail()で集計してFAILURES件数を末尾判定する方式のため、set -eは使わない(1件の失敗で打ち切らない)
# Aggregated via fail() and judged by FAILURES at the end, so set -e is not used (one failure must not abort the rest)
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/verify-on-windows-sandbox.sh
. "$SCRIPT_DIR/lib/verify-on-windows-sandbox.sh"

# 成功系: WoL→ssh待ち→ps1送付→実行→回収→READY確認→ACK
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
grep -q "^wakeonlan " "$SANDBOX/calls.log" || fail "WoL was not sent"
grep -q "run-smoke.ps1" "$SANDBOX/calls.log" || fail "run-smoke.ps1 was not copied to the machine"
READY_LINE=$(grep -n "/v1/inbox/report/7656/20260913_180000_aaaa1111/READY" "$SANDBOX/calls.log" | head -n1 | cut -d: -f1)
ACK_LINE=$(grep -n -- "-X POST .*/v1/inbox/report/7656/20260913_180000_aaaa1111/ack" "$SANDBOX/calls.log" | head -n1 | cut -d: -f1)
[ -n "$READY_LINE" ] || fail "the report's READY was not fetched by reportSteamId/bundle id"
[ -n "$ACK_LINE" ] && [ "${READY_LINE:-0}" -lt "$ACK_LINE" ] || fail "the report was not ACKed after READY was confirmed"
grep -q -- "--max-time" "$SANDBOX/calls.log" || fail "the receiver was not reached through lib/receiver-api.sh"
grep -q "CURL_BIN\|\bcurl \|/v1/inbox\"" "$SCRIPT_DIR/../verify-on-windows.sh" && fail "verify-on-windows.sh still calls the receiver with raw curl"
grep -q -- "-ExpectedBuildLabel '$LABEL'" "$SANDBOX/calls.log" || fail "run-smoke.ps1 was not told the expected build label"

# ssh到達が遅れても期限内なら成功する
make_sandbox
OUTPUT=$(SSH_READY_AT=3 run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "delayed ssh did not succeed: $OUTPUT"

# 期限まで起きなければ検証失敗
make_sandbox
OUTPUT=$(SSH_READY_AT=999 SSH_WAIT_TIMEOUT_SECONDS=0 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "unreachable machine did not fail"
case "$OUTPUT" in *"到達"*) ;; *) fail "unreachable machine did not say why";; esac
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "inbox was checked despite an unreachable machine"

# 待ちは実時間の締め切りで打ち切る。ssh 自体が時間を食い sleep が長くても期限を大きく超えない（F15）
# The wait is cut off by a wall-clock deadline; even with slow ssh calls and a long poll sleep it never overruns much
make_sandbox
STARTED=$SECONDS
OUTPUT=$(SSH_READY_AT=999 SSH_UNREACHABLE_DELAY=1 SSH_WAIT_TIMEOUT_SECONDS=3 SSH_POLL_SECONDS=30 run_target); STATUS=$?
[ "$STATUS" -eq 3 ] || fail "a slow unreachable machine did not exit 3 (got $STATUS): $OUTPUT"
[ $((SECONDS - STARTED)) -le 8 ] || fail "the ssh wait overran its 3s deadline (took $((SECONDS - STARTED))s)"

# 許可リスト外のラベルは何も呼ばずに落ちる（リモートPowerShell文字列への注入対策・F14）
# A label outside the allowlist fails before any call (guards injection into the remote PowerShell string)
make_sandbox
OUTPUT=$(VERIFY_LABEL="x'; Remove-Item C:/ -Recurse; '" run_target); STATUS=$?
[ "$STATUS" -eq 2 ] || fail "an injected label was not refused (got $STATUS): $OUTPUT"
[ ! -f "$SANDBOX/calls.log" ] || fail "an injected label reached wakeonlan/ssh"

# 必須envの欠落は起こす前に落ちる
make_sandbox
OUTPUT=$(MOORESTECH_VERIFY_MAC="" bash -c '
  MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores MOORESTECH_VERIFY_MAC= \
  PLAYTEST_RECEIVER_BASE=x PLAYTEST_ADMIN_KEY=y MOORESTECH_STEAM_USER=z \
  bash "$1" "$2"' _ "$TARGET" "$LABEL" 2>&1); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing MAC did not fail"
case "$OUTPUT" in *MOORESTECH_VERIFY_MAC*) ;; *) fail "missing MAC was not named";; esac

# phase2が失敗したら受け口を見ずに落ちる
make_sandbox
OUTPUT=$(PHASE2_SUCCESS=false run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "failed phase2 did not fail the verification"
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "inbox was checked despite a failed phase2"

# 退行防止: フェーズのトップレベルsuccessがfalseでも、配下のstepsに真のsuccessが
# 1件でもあれば全文一致(旧grep実装)は合格にしてしまっていた。トップレベルだけを見て弾く
# Regression: even when a phase's top-level success is false, a whole-text match (the old grep
# implementation) would pass if any nested step's success was true. Only the top level must count
make_sandbox
OUTPUT=$(PHASE1_SUCCESS=false run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a false top-level success with a true nested step did not fail"
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "inbox was checked despite a failed phase1"

# READY が受け口に無ければ（再試行しても）落ち、ACK もしない
# When READY never appears on the receiver (even after retries) the run fails and nothing is ACKed
make_sandbox
OUTPUT=$(RECEIVER_HAS_REPORT=0 run_target); STATUS=$?
[ "$STATUS" -eq 6 ] || fail "a missing READY did not exit 6 (got $STATUS): $OUTPUT"
[ "$(grep -c "/READY" "$SANDBOX/calls.log")" -gt 1 ] || fail "a missing READY was not retried"
grep -q "/ack" "$SANDBOX/calls.log" && fail "an unconfirmed report was ACKed"

# 別の報告IDの READY しか無ければ合格にしない（バンドルIDでの照合が効いている）
# Only another report id's READY must not pass (bundle-id matching works)
make_sandbox
OUTPUT=$(SMOKE_REPORT_ID=bbbb2222 run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "the matching bundle id did not pass: $OUTPUT"
grep -q "20260913_180000_bbbb2222/ack" "$SANDBOX/calls.log" || fail "the matching bundle id was not ACKed"

# result.json に reportSteamId が無ければ受け口を叩かず落ちる。数値で書かれていても読める
# Without reportSteamId in result.json the run fails without touching the receiver; a numeric value is still read
make_sandbox
OUTPUT=$(REPORT_STEAM_ID_FIELD="" run_target); STATUS=$?
[ "$STATUS" -eq 6 ] || fail "a missing reportSteamId did not exit 6 (got $STATUS): $OUTPUT"
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "the receiver was called without reportSteamId"
make_sandbox
OUTPUT=$(REPORT_STEAM_ID_FIELD=', "reportSteamId": 7656' run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "a numeric reportSteamId did not pass: $OUTPUT"

# ACK に失敗したら、検証用の報告が取り込まれてしまうため失敗にする
# A failed ACK fails the run, since the smoke report would otherwise be ingested
make_sandbox
OUTPUT=$(ACK_STATUS=500 run_target); STATUS=$?
[ "$STATUS" -eq 8 ] || fail "a failed ACK did not exit 8 (got $STATUS): $OUTPUT"

# 検証機側がラベル不一致等で非0を返したら、verify も非0で終わり回収も受け口確認もしない
# When the machine side exits non-zero (e.g. label mismatch), verify also fails without collecting or checking the inbox
make_sandbox
OUTPUT=$(SMOKE_RUN_EXIT=2 run_target); STATUS=$?
[ "$STATUS" -eq 2 ] || fail "a label mismatch on the machine did not propagate exit 2 (got $STATUS): $OUTPUT"
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "inbox was checked despite a failed smoke on the machine"
grep -q -- "-r .*results" "$SANDBOX/calls.log" && fail "results were collected despite a failed smoke on the machine"

# 同じラベルの2回目で phase2 の result.json が来なければ、1回目の合格結果を読まずに落ちる
# On a second run with the same label, a missing phase2 result.json fails instead of reading the first run's pass
make_sandbox
run_target >/dev/null 2>&1 || fail "the first same-label run did not pass"
OUTPUT=$(SCP_OMIT_PHASE2=1 run_target); STATUS=$?
[ "$STATUS" -eq 4 ] || fail "a stale phase2 result from the first run was reused (got $STATUS): $OUTPUT"

# 検証機の Windows PowerShell 5.1 は BOM 無し UTF-8 を ANSI として読み日本語で壊れるため、run-smoke.ps1 は BOM 付きで保つ
# Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI and breaks on Japanese text, so run-smoke.ps1 must keep its BOM
[ "$(head -c 3 "$SCRIPT_DIR/../windows/run-smoke.ps1" | od -An -tx1 | tr -d ' \n')" = "efbbbf" ] || fail "run-smoke.ps1 lost its UTF-8 BOM"
if [ "$FAILURES" -ne 0 ]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi
echo "PASS: verify-on-windows contract"

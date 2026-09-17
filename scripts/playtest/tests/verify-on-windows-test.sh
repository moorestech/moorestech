#!/bin/bash
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET="$SCRIPT_DIR/../verify-on-windows.sh"
FAILURES=0
LABEL="playtest-20260913-1730"

fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

make_sandbox() {
    SANDBOX="$(mktemp -d)"
    mkdir -p "$SANDBOX/bin" "$SANDBOX/artifacts"
    cat >"$SANDBOX/bin/wakeonlan" <<EOF
#!/bin/bash
echo "wakeonlan \$*" >>"$SANDBOX/calls.log"
exit \${WOL_EXIT:-0}
EOF
    # sshは SSH_READY_AT 回目の呼び出しから成功する（到達待ちのループを再現する）
    # ssh starts succeeding at call number SSH_READY_AT, reproducing the reachability loop
    cat >"$SANDBOX/bin/ssh" <<EOF
#!/bin/bash
echo "ssh \$*" >>"$SANDBOX/calls.log"
count=\$(grep -c '^ssh ' "$SANDBOX/calls.log")
[ "\$count" -ge "\${SSH_READY_AT:-1}" ] || exit 255
exit \${SSH_RUN_EXIT:-0}
EOF
    cat >"$SANDBOX/bin/scp" <<EOF
#!/bin/bash
echo "scp \$*" >>"$SANDBOX/calls.log"
# 最後の引数がローカルの回収先なら、検証機が残したはずの result.json を再現する
# When the last argument is the local collection directory, reproduce the result.json the machine would leave
for last; do :; done
case "\$last" in
  "$SANDBOX"/artifacts*)
    mkdir -p "\$last/phase1" "\$last/phase2"
    printf '{"phase": "phase1", "success": %s}' "\${PHASE1_SUCCESS:-true}" >"\$last/phase1/result.json"
    printf '{"phase": "phase2", "success": %s, "reportBundleDirectory": "x"}' "\${PHASE2_SUCCESS:-true}" >"\$last/phase2/result.json"
    ;;
esac
exit 0
EOF
    cat >"$SANDBOX/bin/curl" <<EOF
#!/bin/bash
echo "curl \$*" >>"$SANDBOX/calls.log"
if [ "\${INBOX_HAS_REPORT:-1}" = "1" ]; then
  echo '{"items":[{"kind":"report","steamId":"7656","id":"abc","readyAt":"2026-09-13T18:00:00Z"}],"cursor":""}'
else
  echo '{"items":[],"cursor":""}'
fi
EOF
    chmod +x "$SANDBOX/bin/"*
}

run_target() {
    ( MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores \
      MOORESTECH_VERIFY_MAC=00:11:22:33:44:55 \
      MOORESTECH_RECEIVER_BASE=https://playtest.tar-atari.com \
      MOORESTECH_RECEIVER_ADMIN_KEY=dummy \
      MOORESTECH_STEAM_USER=steamuser \
      WAKEONLAN_BIN="$SANDBOX/bin/wakeonlan" SSH_BIN="$SANDBOX/bin/ssh" \
      SCP_BIN="$SANDBOX/bin/scp" CURL_BIN="$SANDBOX/bin/curl" \
      VERIFY_ARTIFACT_ROOT="$SANDBOX/artifacts" \
      SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS-60}" SSH_POLL_SECONDS=0 \
      WOL_EXIT="${WOL_EXIT-0}" SSH_READY_AT="${SSH_READY_AT-1}" SSH_RUN_EXIT="${SSH_RUN_EXIT-0}" \
      PHASE1_SUCCESS="${PHASE1_SUCCESS-true}" PHASE2_SUCCESS="${PHASE2_SUCCESS-true}" \
      INBOX_HAS_REPORT="${INBOX_HAS_REPORT-1}" \
      bash "$TARGET" "$LABEL" 2>&1 )
}

# 成功系: WoL→ssh待ち→ps1送付→実行→回収→inbox確認
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
grep -q "^wakeonlan " "$SANDBOX/calls.log" || fail "WoL was not sent"
grep -q "run-smoke.ps1" "$SANDBOX/calls.log" || fail "run-smoke.ps1 was not copied to the machine"
grep -q "/v1/inbox" "$SANDBOX/calls.log" || fail "the receiver inbox was not checked"

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

# 必須envの欠落は起こす前に落ちる
make_sandbox
OUTPUT=$(MOORESTECH_VERIFY_MAC="" bash -c '
  MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores MOORESTECH_VERIFY_MAC= \
  MOORESTECH_RECEIVER_BASE=x MOORESTECH_RECEIVER_ADMIN_KEY=y MOORESTECH_STEAM_USER=z \
  bash "$1" "$2"' _ "$TARGET" "$LABEL" 2>&1); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing MAC did not fail"
case "$OUTPUT" in *MOORESTECH_VERIFY_MAC*) ;; *) fail "missing MAC was not named";; esac

# phase2が失敗したら受け口を見ずに落ちる
make_sandbox
OUTPUT=$(PHASE2_SUCCESS=false run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "failed phase2 did not fail the verification"
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "inbox was checked despite a failed phase2"

# 報告が受け口に届いていなければ落ちる
make_sandbox
OUTPUT=$(INBOX_HAS_REPORT=0 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing report in the inbox did not fail"

if [ "$FAILURES" -ne 0 ]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi
echo "PASS: verify-on-windows contract"

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
    # scpはディレクトリごとコピーする実装（-r remote:.../results dest）に合わせ、
    # destの直下ではなく dest/results/<phase> へ result.json を置く
    # scp copies the directory itself (-r remote:.../results dest), so results land under
    # dest/results/<phase>, mirroring scp's real nesting behavior when the dest already exists
    cat >"$SANDBOX/bin/scp" <<EOF
#!/bin/bash
echo "scp \$*" >>"$SANDBOX/calls.log"
# 最後の引数がローカルの回収先なら、検証機が残したはずの result.json を再現する
# When the last argument is the local collection directory, reproduce the result.json the machine would leave
for last; do :; done
case "\$last" in
  "$SANDBOX"/artifacts*)
    dest="\$last/results"
    mkdir -p "\$dest/phase1" "\$dest/phase2"
    printf '{"phase": "phase1", "success": %s}' "\${PHASE1_SUCCESS:-true}" >"\$dest/phase1/result.json"
    # reportBundleDirectoryは検証機(Windows)のパス。バックスラッシュ区切りでIDを末尾に持たせ、
    # basenameがそのままでは割れないことを再現する
    # reportBundleDirectory is the check machine's (Windows) path; backslash-separated with the id
    # as the leaf, reproducing that a plain basename cannot split it
    BS='\'
    printf '{"phase": "phase2", "success": %s, "reportBundleDirectory": "C:%smoorestech-smoke%soutbox%sreport%s20260913_180000_%s"}' \
      "\${PHASE2_SUCCESS:-true}" "\$BS" "\$BS" "\$BS" "\$BS" "\${SMOKE_REPORT_ID:-aaaa1111}" >"\$dest/phase2/result.json"
    ;;
esac
exit 0
EOF
    # inboxは常に無関係な進行報告(progress)を1件含む(現実の運用は無人)。
    # 対象のreport項目はINBOX_HAS_REPORTでon/offし、無関係項目だけでは合格にならないことを検証できるようにする
    # The inbox always carries one unrelated progress item (real operation is unattended);
    # the target report item is toggled by INBOX_HAS_REPORT so a mismatch-only inbox can be tested
    cat >"$SANDBOX/bin/curl" <<EOF
#!/bin/bash
echo "curl \$*" >>"$SANDBOX/calls.log"
items='{"kind":"progress","steamId":"7656","id":"zzzz9999","readyAt":"2026-09-13T17:00:00Z"}'
if [ "\${INBOX_HAS_REPORT:-1}" = "1" ]; then
  items="\$items,{\"kind\":\"report\",\"steamId\":\"7656\",\"id\":\"20260913_180000_\${SMOKE_REPORT_ID:-aaaa1111}\",\"readyAt\":\"2026-09-13T18:00:00Z\"}"
fi
if [ "\${INBOX_HAS_UNRELATED_REPORT:-0}" = "1" ]; then
  items="\$items,{\"kind\":\"report\",\"steamId\":\"7656\",\"id\":\"yyyy8888\",\"readyAt\":\"2026-09-13T17:30:00Z\"}"
fi
echo "{\"items\":[\$items],\"cursor\":\"\"}"
EOF
    chmod +x "$SANDBOX/bin/"*
}

run_target() {
    ( MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores \
      MOORESTECH_VERIFY_MAC=00:11:22:33:44:55 \
      MOORESTECH_RECEIVER_BASE=https://playtest.tar-atari.com \
      MOORESTECH_RECEIVER_ADMIN_KEY=dummy \
      WAKEONLAN_BIN="$SANDBOX/bin/wakeonlan" SSH_BIN="$SANDBOX/bin/ssh" \
      SCP_BIN="$SANDBOX/bin/scp" CURL_BIN="$SANDBOX/bin/curl" \
      VERIFY_ARTIFACT_ROOT="$SANDBOX/artifacts" \
      SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS-60}" SSH_POLL_SECONDS=0 \
      WOL_EXIT="${WOL_EXIT-0}" SSH_READY_AT="${SSH_READY_AT-1}" SSH_RUN_EXIT="${SSH_RUN_EXIT-0}" \
      PHASE1_SUCCESS="${PHASE1_SUCCESS-true}" PHASE2_SUCCESS="${PHASE2_SUCCESS-true}" \
      INBOX_HAS_REPORT="${INBOX_HAS_REPORT-1}" SMOKE_REPORT_ID="${SMOKE_REPORT_ID-aaaa1111}" \
      INBOX_HAS_UNRELATED_REPORT="${INBOX_HAS_UNRELATED_REPORT-0}" \
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

# 報告が受け口に届いていなければ落ちる（無関係な進行報告は常にinboxへ同居している）
make_sandbox
OUTPUT=$(INBOX_HAS_REPORT=0 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing report in the inbox did not fail"

# 退行防止: inboxに無関係な報告(別id)だけがあっても合格にしない（レビュー指摘の偽陽性）
# Regression: an unrelated report (different id) alone in the inbox must not pass (review's false-positive finding)
make_sandbox
OUTPUT=$(INBOX_HAS_REPORT=0 INBOX_HAS_UNRELATED_REPORT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "an unrelated report in the inbox was wrongly treated as this run's report"

# 対象の報告が無関係な項目に混じっていれば合格にする（バンドルIDでの照合が効いている）
# The target report succeeds even mixed in with unrelated items (bundle-id matching works)
make_sandbox
OUTPUT=$(INBOX_HAS_REPORT=1 INBOX_HAS_UNRELATED_REPORT=1 SMOKE_REPORT_ID=bbbb2222 run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "the matching report among unrelated items did not pass: $OUTPUT"

if [ "$FAILURES" -ne 0 ]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi
echo "PASS: verify-on-windows contract"

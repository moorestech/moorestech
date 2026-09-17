#!/bin/bash
# 検証機を起こし、配布ビルドの通し検証を回して結果と受け口への到達を確認する
# Wakes the check machine, runs the distribution smoke and confirms the result and the receiver delivery
#
# usage: verify-on-windows.sh <steamBuildLabel>
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_LABEL="${1:?usage: verify-on-windows.sh <steamBuildLabel>}"

WAKEONLAN_BIN="${WAKEONLAN_BIN:-wakeonlan}"
SSH_BIN="${SSH_BIN:-ssh}"
SCP_BIN="${SCP_BIN:-scp}"
CURL_BIN="${CURL_BIN:-curl}"
SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS:-600}"
SSH_POLL_SECONDS="${SSH_POLL_SECONDS:-10}"
VERIFY_ARTIFACT_ROOT="${VERIFY_ARTIFACT_ROOT:-$HOME/hermes-agent/data/services/playtest/runs/$BUILD_LABEL/verify}"

missing=""
[ -n "${MOORESTECH_VERIFY_HOST:-}" ] || missing="$missing MOORESTECH_VERIFY_HOST"
[ -n "${MOORESTECH_VERIFY_USER:-}" ] || missing="$missing MOORESTECH_VERIFY_USER"
[ -n "${MOORESTECH_VERIFY_MAC:-}" ] || missing="$missing MOORESTECH_VERIFY_MAC"
[ -n "${MOORESTECH_RECEIVER_BASE:-}" ] || missing="$missing MOORESTECH_RECEIVER_BASE"
[ -n "${MOORESTECH_RECEIVER_ADMIN_KEY:-}" ] || missing="$missing MOORESTECH_RECEIVER_ADMIN_KEY"
[ -n "${MOORESTECH_STEAM_USER:-}" ] || missing="$missing MOORESTECH_STEAM_USER"
if [ -n "$missing" ]; then
    echo "ERROR: 必須の環境変数が未設定です:$missing" >&2
    exit 2
fi

REMOTE="$MOORESTECH_VERIFY_USER@$MOORESTECH_VERIFY_HOST"
REMOTE_ROOT="C:/moorestech-smoke/$BUILD_LABEL"
mkdir -p "$VERIFY_ARTIFACT_ROOT"

echo "[verify] waking $MOORESTECH_VERIFY_HOST"
"$WAKEONLAN_BIN" "$MOORESTECH_VERIFY_MAC" || echo "WARN: WoLパケットの送信に失敗しました（既に起動している可能性があります）" >&2

# 起動待ちは期限付きポーリング。起こせなければ検証失敗として告知しない
# Bounded polling for boot; if it never wakes, the verification fails and nothing gets announced
echo "[verify] waiting for ssh (timeout ${SSH_WAIT_TIMEOUT_SECONDS}s)"
waited=0
until "$SSH_BIN" -o BatchMode=yes -o ConnectTimeout=5 "$REMOTE" "echo ok" >/dev/null 2>&1; do
    if [ "$waited" -ge "$SSH_WAIT_TIMEOUT_SECONDS" ]; then
        echo "ERROR: 検証機 $MOORESTECH_VERIFY_HOST に ${SSH_WAIT_TIMEOUT_SECONDS}秒以内へ到達できませんでした" >&2
        exit 3
    fi
    sleep "$SSH_POLL_SECONDS"
    waited=$((waited + SSH_POLL_SECONDS + 1))
done

# 検証機側スクリプトは毎回送る（手置きコピーとの版ずれを構造的に消す）
# The machine-side script is copied every run, structurally removing drift from a hand-placed copy
"$SSH_BIN" -o BatchMode=yes "$REMOTE" "powershell -NoProfile -Command \"New-Item -ItemType Directory -Force -Path '$REMOTE_ROOT' | Out-Null\""
"$SCP_BIN" -o BatchMode=yes "$SCRIPT_DIR/windows/run-smoke.ps1" "$REMOTE:$REMOTE_ROOT/run-smoke.ps1"

echo "[verify] running smoke on $MOORESTECH_VERIFY_HOST"
"$SSH_BIN" -o BatchMode=yes "$REMOTE" \
    "powershell -NoProfile -ExecutionPolicy Bypass -File '$REMOTE_ROOT/run-smoke.ps1' -SteamUser '$MOORESTECH_STEAM_USER' -ResultRoot '$REMOTE_ROOT/results'"

"$SCP_BIN" -o BatchMode=yes -r "$REMOTE:$REMOTE_ROOT/results/*" "$VERIFY_ARTIFACT_ROOT"

for phase in phase1 phase2; do
    result="$VERIFY_ARTIFACT_ROOT/$phase/result.json"
    if [ ! -f "$result" ]; then
        echo "ERROR: $phase の result.json を回収できませんでした: $result" >&2
        exit 4
    fi
    if ! grep -q '"success": *true' "$result"; then
        echo "ERROR: $phase の通し検証が失敗しました: $(cat "$result")" >&2
        exit 5
    fi
done

# 報告が受け口まで届いたことを確認する（クライアント側のUPLOADEDだけでは受領を保証できない）
# Confirm the report reached the receiver; the client-side UPLOADED marker alone does not prove receipt
# 照合はバンドルIDで行う。inboxは未ACKのものだけを返すため、取り込み(plan H)が先にACKすると見えなくなる
# Match by bundle id; the inbox lists only un-ACKed items, so an ingest run (plan H) that ACKs first hides it
REPORT_ID="$(basename "$(sed -n 's/.*"reportBundleDirectory" *: *"\([^"]*\)".*/\1/p' "$VERIFY_ARTIFACT_ROOT/phase2/result.json")")"
if [ -z "$REPORT_ID" ]; then
    echo "ERROR: phase2 の result.json から報告バンドルIDを読めませんでした" >&2
    exit 6
fi
echo "[verify] checking the receiver inbox for $REPORT_ID"
found=0
attempt=0
while [ "$attempt" -lt 6 ]; do
    inbox="$("$CURL_BIN" -sS -H "X-Admin-Key: $MOORESTECH_RECEIVER_ADMIN_KEY" "$MOORESTECH_RECEIVER_BASE/v1/inbox")"
    echo "$inbox" >"$VERIFY_ARTIFACT_ROOT/inbox.json"
    if printf '%s' "$inbox" | grep -q '"kind"'; then
        found=1
        break
    fi
    attempt=$((attempt + 1))
    sleep "$SSH_POLL_SECONDS"
done
if [ "$found" -eq 0 ]; then
    echo "ERROR: 受け口に報告 $REPORT_ID が届いていません（取り込みが先にACKした可能性があるため、検証中は plan H の ingest を止めること）: $inbox" >&2
    exit 6
fi

echo "[verify] passed: $BUILD_LABEL"

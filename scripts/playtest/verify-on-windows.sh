#!/usr/bin/env bash
# 検証機を起こし、配布ビルドの通し検証を回して結果と受け口への到達を確認する
# Wakes the check machine, runs the distribution smoke and confirms the result and the receiver delivery
#
# usage: verify-on-windows.sh <steamBuildLabel>
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_LABEL="${1:?usage: verify-on-windows.sh <steamBuildLabel>}"

WAKEONLAN_BIN="${WAKEONLAN_BIN:-wakeonlan}"
SSH_BIN="${SSH_BIN:-ssh}"
SCP_BIN="${SCP_BIN:-scp}"
CURL_BIN="${CURL_BIN:-curl}"
SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS:-600}"
SSH_POLL_SECONDS="${SSH_POLL_SECONDS:-10}"
VERIFY_ARTIFACT_ROOT="${VERIFY_ARTIFACT_ROOT:-$HOME/hermes-agent/data/services/playtest/runs/$BUILD_LABEL/verify}"
PLAYTEST_RECEIVER_BASE="${PLAYTEST_RECEIVER_BASE:-https://playtest.tar-atari.com}"

missing=""
[ -n "${MOORESTECH_VERIFY_HOST:-}" ] || missing="$missing MOORESTECH_VERIFY_HOST"
[ -n "${MOORESTECH_VERIFY_USER:-}" ] || missing="$missing MOORESTECH_VERIFY_USER"
[ -n "${MOORESTECH_VERIFY_MAC:-}" ] || missing="$missing MOORESTECH_VERIFY_MAC"
[ -n "${PLAYTEST_ADMIN_KEY:-}" ] || missing="$missing PLAYTEST_ADMIN_KEY"
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
    "powershell -NoProfile -ExecutionPolicy Bypass -File '$REMOTE_ROOT/run-smoke.ps1' -ResultRoot '$REMOTE_ROOT/results' -ExpectedBuildLabel '$BUILD_LABEL'"

# 前回実行の残骸を先に消す。scpは宛先に既存のresultsがあるとその中へ入れ子で置くため、
# 消さないまま再実行すると古いresult.json/announce.mdを読んでしまう
# Clear any leftover from a previous run first; scp nests results/ inside an existing
# destination, so skipping this would leave a stale result.json readable by the next run
rm -rf "$VERIFY_ARTIFACT_ROOT/results"

# ワイルドカード展開はリモート側シェルに依存し、SFTPプロトコルのscpでは効かないことがある。
# ディレクトリごとコピーして展開を回避する（宛先は既にmkdir -p済みなので中へ results/ ごと入る）
# A remote-shell-dependent wildcard can silently no-op under the SFTP scp protocol; copy the
# directory itself instead (the destination already exists, so scp nests results/ inside it)
"$SCP_BIN" -o BatchMode=yes -r "$REMOTE:$REMOTE_ROOT/results" "$VERIFY_ARTIFACT_ROOT"
COLLECTED_ROOT="$VERIFY_ARTIFACT_ROOT/results"

for phase in phase1 phase2; do
    result="$COLLECTED_ROOT/$phase/result.json"
    if [ ! -f "$result" ]; then
        echo "ERROR: $phase の result.json を回収できませんでした: $result" >&2
        exit 4
    fi
    # トップレベルのsuccessだけを見る。ステップ配列にも同名キーがあるため、
    # grepの全文一致ではどちらか一方が真なだけで合格にしてしまう(兄弟のallowlist.shに合わせpython3 jsonで読む)
    # Read only the top-level success; the steps array carries the same key name, so a whole-text
    # grep would pass when either one alone is true (python3 json, matching sibling allowlist.sh)
    if ! python3 -c 'import json,sys; sys.exit(0 if json.load(open(sys.argv[1])).get("success") is True else 1)' "$result"; then
        echo "ERROR: $phase の通し検証が失敗しました: $(cat "$result")" >&2
        exit 5
    fi
done

# 報告が受け口まで届いたことを確認する（クライアント側のUPLOADEDだけでは受領を保証できない）
# Confirm the report reached the receiver; the client-side UPLOADED marker alone does not prove receipt
# 照合はバンドルIDで行う。inboxは未ACKのものだけを返すため、取り込み(plan H)が先にACKすると見えなくなる
# Match by bundle id; the inbox lists only un-ACKed items, so an ingest run (plan H) that ACKs first hides it
# reportBundleDirectoryは検証機(Windows)のパスで、JSON中はバックスラッシュがエスケープされ2文字("\\")で
# 現れる。basenameはスラッシュしか割らないため使わず、末尾の区切り文字(\または/、連続もまとめて)より後ろだけ取る
# reportBundleDirectory is a Windows path; JSON escaping renders each backslash as two chars ("\\").
# basename only splits on '/', so instead strip everything through the last run of '\' or '/' chars
REPORT_DIRECTORY_RAW="$(sed -n 's/.*"reportBundleDirectory" *: *"\([^"]*\)".*/\1/p' "$COLLECTED_ROOT/phase2/result.json")"
REPORT_ID="$(printf '%s' "$REPORT_DIRECTORY_RAW" | sed 's#.*[\\/]##')"
if [ -z "$REPORT_ID" ]; then
    echo "ERROR: phase2 の result.json から報告バンドルIDを読めませんでした" >&2
    exit 6
fi
echo "[verify] checking the receiver inbox for $REPORT_ID"
found=0
attempt=0
while [ "$attempt" -lt 6 ]; do
    # set -e の下でcurl失敗がそのままスクリプトを落とさないよう、失敗時は空文字にして次の再試行へ回す
    # Under set -e, a curl failure must not abort the script outright; fall back to empty and retry
    inbox="$("$CURL_BIN" -sS -H "X-Admin-Key: $PLAYTEST_ADMIN_KEY" "$PLAYTEST_RECEIVER_BASE/v1/inbox")" || {
        echo "WARN: 受け口inboxの取得に失敗しました（再試行します）" >&2
        inbox=""
    }
    echo "$inbox" >"$VERIFY_ARTIFACT_ROOT/inbox.json"
    # itemsは "{...},{...}" のフラットな並び(admin.tsのgetInboxが1オブジェクト1件で返す)なので
    # }{ を境に割ってから各要素にkind/idの両方を要求する。無関係な他報告のkind一致だけでは合格にしない
    # items is a flat "{...},{...}" list (admin.ts's getInbox emits one flat object per entry); split
    # on }{ and require BOTH kind and id per element so an unrelated report's mere presence never passes
    while IFS= read -r item; do
        case "$item" in
            *'"kind":"report"'*'"id":"'"$REPORT_ID"'"'*)
                found=1
                break
                ;;
        esac
    done <<EOF
$(printf '%s' "$inbox" | sed 's/},{/}\n{/g')
EOF
    [ "$found" -eq 1 ] && break
    attempt=$((attempt + 1))
    sleep "$SSH_POLL_SECONDS"
done
if [ "$found" -eq 0 ]; then
    echo "ERROR: 受け口に報告 $REPORT_ID が届いていません（取り込みが先にACKした可能性があるため、検証中は plan H の ingest を止めること）: $inbox" >&2
    exit 6
fi

echo "[verify] passed: $BUILD_LABEL"

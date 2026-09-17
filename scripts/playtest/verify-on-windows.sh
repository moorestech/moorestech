#!/usr/bin/env bash
# 検証機を起こし、配布ビルドの通し検証を回して結果と受け口への到達を確認する
# Wakes the check machine, runs the distribution smoke and confirms the result and the receiver delivery
#
# usage: verify-on-windows.sh <steamBuildLabel>
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_LABEL="${1:?usage: verify-on-windows.sh <steamBuildLabel>}"
# ラベルはリモートPowerShell文字列とパスへ埋め込むため、単体実行でも入口で許可リスト検証する
# The label is embedded in remote PowerShell strings and paths, so it is allowlist-validated at entry even when run standalone
# shellcheck source=lib/release-preflight.sh
. "$SCRIPT_DIR/lib/release-preflight.sh"
playtest_require_build_label "$BUILD_LABEL"

WAKEONLAN_BIN="${WAKEONLAN_BIN:-wakeonlan}"
SSH_BIN="${SSH_BIN:-ssh}"
SCP_BIN="${SCP_BIN:-scp}"
SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS:-600}"
SSH_POLL_SECONDS="${SSH_POLL_SECONDS:-10}"
VERIFY_ARTIFACT_ROOT="${VERIFY_ARTIFACT_ROOT:-$HOME/hermes-agent/data/services/playtest/runs/$BUILD_LABEL/verify}"
RECEIVER_ATTEMPTS="${RECEIVER_ATTEMPTS:-6}"

missing=""
[ -n "${MOORESTECH_VERIFY_HOST:-}" ] || missing="$missing MOORESTECH_VERIFY_HOST"
[ -n "${MOORESTECH_VERIFY_USER:-}" ] || missing="$missing MOORESTECH_VERIFY_USER"
[ -n "${MOORESTECH_VERIFY_MAC:-}" ] || missing="$missing MOORESTECH_VERIFY_MAC"
[ -n "${PLAYTEST_ADMIN_KEY:-}" ] || missing="$missing PLAYTEST_ADMIN_KEY"
if [ -n "$missing" ]; then
    echo "ERROR: 必須の環境変数が未設定です:$missing" >&2
    exit 2
fi
# 受け口 admin API は共有 lib だけを通す（リダイレクト非追従・鍵非出力の約束を一箇所に保つ）
# The receiver admin API is reached only through the shared lib (keeps no-redirect and never-print-key in one place)
# shellcheck source=lib/receiver-api.sh
. "$SCRIPT_DIR/lib/receiver-api.sh"

REMOTE="$MOORESTECH_VERIFY_USER@$MOORESTECH_VERIFY_HOST"
REMOTE_ROOT="C:/moorestech-smoke/$BUILD_LABEL"
mkdir -p "$VERIFY_ARTIFACT_ROOT"

echo "[verify] waking $MOORESTECH_VERIFY_HOST"
"$WAKEONLAN_BIN" "$MOORESTECH_VERIFY_MAC" || echo "WARN: WoLパケットの送信に失敗しました（既に起動している可能性があります）" >&2

# 起動待ちは期限付きポーリング。起こせなければ検証失敗として告知しない
# Bounded polling for boot; if it never wakes, the verification fails and nothing gets announced
echo "[verify] waiting for ssh (timeout ${SSH_WAIT_TIMEOUT_SECONDS}s)"
# 経過は実時間の絶対締め切りで測る（ssh自体のConnectTimeout分を見積もりで足すと実時間とずれる）。sleepも残り時間で打ち切る
# Elapsed time is measured against an absolute wall-clock deadline (estimating ssh's own ConnectTimeout drifts); sleep is capped by the remainder
deadline=$((SECONDS + SSH_WAIT_TIMEOUT_SECONDS))
until "$SSH_BIN" -o BatchMode=yes -o ConnectTimeout=5 "$REMOTE" "echo ok" >/dev/null 2>&1; do
    remaining=$((deadline - SECONDS))
    if [ "$remaining" -le 0 ]; then
        echo "ERROR: 検証機 $MOORESTECH_VERIFY_HOST に ${SSH_WAIT_TIMEOUT_SECONDS}秒以内へ到達できませんでした" >&2
        exit 3
    fi
    sleep "$((SSH_POLL_SECONDS < remaining ? SSH_POLL_SECONDS : remaining))"
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

# 報告が受け口まで届いたことを、ACK状態に左右されない READY マーカーの取得で確認し、確認後は自分でACKする
# （未ACKだけを返す inbox 照合は取り込みが先にACKすると偽陰性になるため使わない）。ただしアップロードからこのACKまでの間に
# 定期取り込みが走ると検証用の報告をバグ報告として取り込みACKしてしまい、こちらのACKは冪等に成功して気づけない。
# ACKは後片付けであって取り込みとの競合を防がないため、検証中は playtest-ingest を止める運用が前提（README 参照）
# Confirm the report reached the receiver by fetching its READY marker, independent of ACK state, then ACK it ourselves
# (the un-ACKed-only inbox match false-negatives once ingest ACKs first). If the periodic ingest runs between the upload and
# this ACK, it ingests and ACKs the smoke report as a real bug report and our idempotent ACK still succeeds silently.
# The ACK is cleanup, not protection against that race, so playtest-ingest must be stopped during verification (see README)
# reportBundleDirectory は検証機(Windows)のパスなので、末尾の区切り文字(\ または /)より後ろをバンドルIDとして取る
# reportBundleDirectory is a Windows path, so the bundle id is whatever follows the last separator (\ or /)
REPORT_FIELDS="$(python3 -c '
import json, re, sys
result = json.load(open(sys.argv[1]))
print(re.split(r"[\\/]+", result.get("reportBundleDirectory", "").rstrip("\\/"))[-1]); print(result.get("reportSteamId", ""))
' "$COLLECTED_ROOT/phase2/result.json")"
REPORT_ID="$(printf '%s\n' "$REPORT_FIELDS" | sed -n 1p)"
REPORT_STEAM_ID="$(printf '%s\n' "$REPORT_FIELDS" | sed -n 2p)"
if [ -z "$REPORT_ID" ] || [ -z "$REPORT_STEAM_ID" ]; then
    echo "ERROR: phase2 の result.json から報告バンドルID/reportSteamId を読めませんでした: id='${REPORT_ID}' steamId='${REPORT_STEAM_ID}'" >&2
    exit 6
fi
echo "[verify] checking the receiver for report $REPORT_STEAM_ID/$REPORT_ID"
found=0
attempt=0
while [ "$attempt" -lt "$RECEIVER_ATTEMPTS" ]; do
    # 取得失敗は lib が理由を stderr へ出す。set -e で落とさず次の再試行へ回す
    # The lib logs the reason for a failed fetch; do not abort under set -e, retry instead
    if receiver_get_object report "$REPORT_STEAM_ID" "$REPORT_ID" READY "$VERIFY_ARTIFACT_ROOT/report-ready"; then
        found=1
        break
    fi
    attempt=$((attempt + 1))
    sleep "$SSH_POLL_SECONDS"
done
if [ "$found" -eq 0 ]; then
    echo "ERROR: 受け口に報告 ${REPORT_STEAM_ID}/${REPORT_ID} の READY が ${RECEIVER_ATTEMPTS} 回試しても見つかりません" >&2
    exit 6
fi
# 検証用の報告が取り込み再開後に拾われないよう、確認できたらACKする。ACKできなければ後で取り込まれるため失敗にする
# ACK the verified smoke report so it is not picked up once ingest resumes; failing to ACK would let it in later, so that fails the run
if ! receiver_ack report "$REPORT_STEAM_ID" "$REPORT_ID"; then
    echo "ERROR: 受け口の報告 ${REPORT_STEAM_ID}/${REPORT_ID} を ACK できませんでした（取り込みに検証用の報告が混ざるため手でACKしてください）" >&2
    exit 8
fi

echo "[verify] passed: $BUILD_LABEL"

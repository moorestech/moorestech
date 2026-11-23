#!/usr/bin/env bash
# 指定コミットからWindowsとMac（Apple Silicon）の配布ビルドを焼き、Steamへ上げてWindowsを検証する
# Bake Windows and Mac (Apple Silicon) from a commit, upload to Steam and verify Windows
# 標準経路は配布ビルド機（Windows）で焼く release-playtest-windows.sh。これは配布ビルド機が使えないときの予備経路
# The standard route is release-playtest-windows.sh on the Windows build machine; this is the fallback when that machine is unavailable
#
# usage: release-playtest.sh <commit>
# <commit> は SHA か origin/<branch> を渡す。ローカルブランチ名（master 等）は fetch で進まず古いコミットを焼くため使わない
# 実行はメインクローン（moores-wt が worktree を作る clone）の scripts/playtest から行う。別 clone からだと解決した object が無いことがある
# 資格情報は ~/hermes-agent/data/services/playtest/env.sh から供給する（このスクリプトは値を出力しない）
# <commit> is a SHA or origin/<branch>; a local branch name (e.g. master) is not advanced by fetch and would bake a stale commit
# Run it from scripts/playtest of the main clone (the clone moores-wt creates worktrees from); another clone may lack the resolved object
# Credentials come from ~/hermes-agent/data/services/playtest/env.sh; this script never echoes their values
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
COMMIT="${1:?usage: release-playtest.sh <commit>}"

GIT_BIN="${GIT_BIN:-git}"
MOORES_WT_BIN="${MOORES_WT_BIN:-moores-wt}"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.6.10f1/Unity.app/Contents/MacOS/Unity}"
STEAMCMD_BIN="$SCRIPT_DIR/steam_auth/run.py"
CODESIGN_BIN="${CODESIGN_BIN:-/usr/bin/codesign}"
LIPO_BIN="${LIPO_BIN:-/usr/bin/lipo}"
VERIFY_SCRIPT="${VERIFY_SCRIPT:-$SCRIPT_DIR/verify-on-windows.sh}"
PLAYTEST_RUN_ROOT="${PLAYTEST_RUN_ROOT:-$HOME/hermes-agent/data/services/playtest/runs}"
# master data worktree を列挙する moorestech_master のメインclone（moores-wt の MASTER と同じ）
# The moorestech_master main clone whose worktrees are listed (same as moores-wt's MASTER)
MASTER_CLONE="${MOORESTECH_MASTER_CLONE:-$HOME/hermes-agent/data/repos/moorestech_master}"
# build-info.json の branch に焼く配布元 ref。使い捨て worktree の一時ブランチ名を焼かないために明示する
# The distribution ref baked into build-info.json's branch, so the disposable worktree's temporary branch name is never baked
BUILD_BRANCH="${MOORESTECH_BUILD_BRANCH:-master}"

# 検証機を含む必須envとラベルはビルド前に全部見る（半端に焼いてから・上げてから落ちない）
# Check every required env (check machine included) and the label before building, so nothing fails after baking or upload
# shellcheck source=lib/release-preflight.sh
. "$SCRIPT_DIR/lib/release-preflight.sh"
# shellcheck source=lib/release-artifact.sh
. "$SCRIPT_DIR/lib/release-artifact.sh"
# shellcheck source=lib/release-claim.sh
. "$SCRIPT_DIR/lib/release-claim.sh"
# shellcheck source=lib/release-deliver.sh
. "$SCRIPT_DIR/lib/release-deliver.sh"
# shellcheck source=lib/release-steam-vdf.sh
. "$SCRIPT_DIR/lib/release-steam-vdf.sh"
release_claim_run

# 使い捨てworktreeで焼く（メインワークツリーのEditorとブランチを触らない）
# Bake in a disposable worktree so the main worktree's Editor and branch stay untouched
BRANCH="playtest/build-${COMMIT_FULL:0:8}"
WORKTREE="$("$MOORES_WT_BIN" new "$BRANCH" --from "$COMMIT_FULL" --no-editor --fetch | tail -n 1)"
if [ ! -d "$WORKTREE/moorestech_client" ]; then
    echo "ERROR: worktreeを作れませんでした: ${WORKTREE}" >&2
    exit 3
fi
# 使い捨てworktreeを終了時に必ず畳む(成果物はRUN_DIR側にあるので消して問題ない)。
# HEAD照合より前に登録し、照合が失敗してもstaleなworktree/ブランチを残さない
# Always tear down the disposable worktree on exit (artifacts live under RUN_DIR, so this is safe).
# Registered before the HEAD check so a mismatch never leaves a stale worktree/branch behind
trap '"$MOORES_WT_BIN" rm "$WORKTREE" --force --prune-branch' EXIT
# worktreeのHEADが要求コミットとずれていないか、数十分かかるビルドへ入る前に確認する（staleなローカルブランチの再利用対策）
# Confirm the worktree's HEAD matches the requested commit before the lengthy build (guards against reusing a stale local branch)
EXPECTED_COMMIT="$("$GIT_BIN" -C "$WORKTREE" rev-parse HEAD)"
if [ "$EXPECTED_COMMIT" != "$COMMIT_FULL" ]; then
    echo "ERROR: worktree の HEAD が要求コミットと一致しません: got ${EXPECTED_COMMIT} want ${COMMIT_FULL}" >&2
    exit 3
fi

# 同梱元 master data（ピンを HEAD に持つ worktree）と非公開アセット（ffmpeg）をコミット済みピンと突き合わせ、未コミット変更も拒む。
# ずれたまま焼くと strict が数十分後に落ちるか出所の違う成果物になるため、合わせるのは人に任せて止まる
# Match the bundled master data (the worktree at the pin) and private assets (ffmpeg) against the committed pins and refuse dirty trees;
# baking with drift fails strict much later or ships an artifact of a different origin, so stop and leave it to a human
release_resolve_master_data_root "$WORKTREE"
release_require_private_assets "$WORKTREE"
echo "[release-playtest] master data root=$MOORESTECH_MASTER_DATA_ROOT"

# 同じworktreeからWindows→Macの順に焼き、両方の検査が済んでから上げる
# Bake Windows then Mac from the same worktree and upload only after both pass inspection
release_build_player() {
    local method="$1" output="$2" log="$3"
    MOORESTECH_BUILD_OUTPUT="$output" MOORESTECH_STEAM_BUILD_LABEL="$BUILD_LABEL" MOORESTECH_BUILD_BRANCH="$BUILD_BRANCH" \
        MOORESTECH_MASTER_DATA_ROOT="$MOORESTECH_MASTER_DATA_ROOT" \
        "$UNITY_BIN" -batchmode -nographics \
        -projectPath "$WORKTREE/moorestech_client" \
        -executeMethod "Client.Editor.Build.SteamPlaytestBuildCli.$method" \
        -logFile "$log"
}
release_build_player WindowsSteamPlaytestBuild "$RUN_DIR/build-windows" "$RUN_DIR/unity-build-windows.log"
release_require_windows_artifact "$RUN_DIR/build-windows"
release_build_player MacOsSteamPlaytestBuild "$RUN_DIR/build-mac" "$RUN_DIR/unity-build-mac.log"

# Mac成果物の構成・出所・署名・CPUを検査する
# Verify the Mac artifact's layout, origin, signature, and CPU
release_require_mac_artifact "$RUN_DIR/build-mac"
release_deliver

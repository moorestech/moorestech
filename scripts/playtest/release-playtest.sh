#!/usr/bin/env bash
# 指定コミットからWindowsとMac（Apple Silicon）の配布ビルドを焼き、Steamへ上げてWindowsを検証する
# Bake Windows and Mac (Apple Silicon) from a commit, upload to Steam and verify Windows
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
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity}"
STEAMCMD_BIN="${STEAMCMD_BIN:-steamcmd}"
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
# shellcheck source=lib/release-steam-vdf.sh
. "$SCRIPT_DIR/lib/release-steam-vdf.sh"
release_require_env
BUILD_LABEL="${MOORESTECH_STEAM_BUILD_LABEL:-playtest-$(date +%Y%m%d-%H%M)}"
playtest_require_build_label "$BUILD_LABEL"

# 解決の前にfetchする。マージ直後のコミットや古いorigin/masterのrefを掴まないため
# Fetch before resolving, so a just-merged commit or a stale origin/master ref is never used
"$GIT_BIN" -C "$SCRIPT_DIR" fetch origin || {
    echo "ERROR: git fetch origin に失敗しました" >&2
    exit 2
}

# 短縮SHAやブランチ名でも受け付けられるよう、比較の前に40桁へ解決する
# Resolve the input to a 40-char SHA before any comparison, so a short SHA or branch name still works
COMMIT_FULL="$("$GIT_BIN" -C "$SCRIPT_DIR" rev-parse --verify "${COMMIT}^{commit}" 2>/dev/null)" || {
    echo "ERROR: COMMIT を解決できません（存在しないコミットか、このrepoにfetchされていません）: ${COMMIT}" >&2
    exit 2
}
# 焼く branch が嘘にならないよう、要求コミットが配布元 ref から到達できることをビルド前に確かめる
# merge-base --is-ancestorは「祖先でない」を1、「refが無い・objectが無い」を128で返すため、終了コードで理由を分ける
# （128のまま「含まれない」と出すと、ブランチ名の打ち間違いをコミットの取り違えと誤診させてしまう）
# Confirm before the build that the requested commit is reachable from the distribution ref, so the baked branch never lies
# merge-base --is-ancestor returns 1 for "not an ancestor" and 128 for "ref/object missing"; branch on the exit code
# (reporting 128 as "not contained" would misdirect a typo'd branch name toward a commit mixup instead)
if "$GIT_BIN" -C "$SCRIPT_DIR" merge-base --is-ancestor "$COMMIT_FULL" "origin/$BUILD_BRANCH"; then
    ANCESTOR_STATUS=0
else
    ANCESTOR_STATUS=$?
fi
if [ "$ANCESTOR_STATUS" -eq 128 ]; then
    echo "ERROR: origin/${BUILD_BRANCH} が見つかりません（MOORESTECH_BUILD_BRANCH を確認してください）" >&2
    exit 2
elif [ "$ANCESTOR_STATUS" -ne 0 ]; then
    echo "ERROR: ${COMMIT_FULL} は origin/${BUILD_BRANCH} に含まれません（配布元 ref が違うなら MOORESTECH_BUILD_BRANCH で指定してください）" >&2
    exit 2
fi

RUN_DIR="$PLAYTEST_RUN_ROOT/$BUILD_LABEL"
STEAM_DIR="$RUN_DIR/steam"
# RUN_DIRの再利用は前回の成果物・promotion.mdを黙って読ませる温床になるためfail-closedで拒否する
# Reusing RUN_DIR would silently read a previous run's artifacts/promotion.md, so refuse it fail-closed
if [ -e "$RUN_DIR" ]; then
    echo "ERROR: RUN_DIR が既に存在します（前回実行の残骸の可能性）: ${RUN_DIR}" >&2
    exit 2
fi
mkdir -p "$RUN_DIR/build-windows" "$RUN_DIR/build-mac" "$STEAM_DIR/output"
echo "[release-playtest] label=$BUILD_LABEL commit=$COMMIT_FULL branch=$BUILD_BRANCH run=$RUN_DIR"

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

# 両OSの成果物の必須構成・出所・Macの署名とCPUを検査する
# Verify both artifacts' layout and origin plus the Mac signature and CPU
release_require_mac_artifact "$RUN_DIR/build-mac"
release_render_steam_vdfs "$RUN_DIR" "$STEAM_DIR"

"$STEAMCMD_BIN" +login "$MOORESTECH_STEAM_USER" +run_app_build "$STEAM_DIR/app_build_playtest.vdf" +quit

# 検証機の通し検証に通ったものだけを手動反映の対象にする
# Only a build that passed the check machine becomes eligible for manual promotion
"$VERIFY_SCRIPT" "$BUILD_LABEL"

cat >"$RUN_DIR/promotion.md" <<EOF
# moorestech プレイテスト反映手順 ($BUILD_LABEL)

- コミット: $COMMIT_FULL
- Steam アップロード先: playtest-staging（Windows depot と Mac depot を同じビルドに格納）
- 通し検証: Windows は検証機で phase1 / phase2 とも合格。Mac 版は自動検証していない（ADR 0071）

## Mac 版の手動確認（playtest 反映前に必須）

1. Apple Silicon の Mac で Steam のベータ \`playtest-staging\` を選び、更新後に Steam から起動する
2. タイトルから新規ワールドへ入り、Tab でインベントリが開くことを確認する
3. 起動に回避操作（セキュリティ設定の変更・コマンド実行）が要ったか: [ ] 要らなかった / [ ] 要った（手順: ）
   要った場合も配布は止めず、その手順をキーと一緒にテスターへ案内する

## 反映

Steamworks → アプリ 1958160 → SteamPipe → ビルドで、検証済みビルドを \`playtest\` ブランチに手動でライブ設定してください。設定後に対象のビルド ID を確認し、テスターへ告知してください。
EOF
echo "[release-playtest] promotion: $RUN_DIR/promotion.md"

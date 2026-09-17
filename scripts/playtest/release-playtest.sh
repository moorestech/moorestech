#!/bin/bash
# 指定コミットからWindows配布ビルドを焼き、Steamのplaytestブランチへ上げ、検証機で通し検証する
# Bakes the Windows distribution build from a commit, ships it to the Steam playtest branch and verifies it on the check machine
#
# usage: release-playtest.sh <commit>
# 資格情報は ~/hermes-agent/data/services/playtest/env.sh から供給する（このスクリプトは値を出力しない）
# Credentials come from ~/hermes-agent/data/services/playtest/env.sh; this script never echoes their values
set -eu
set -o pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
COMMIT="${1:?usage: release-playtest.sh <commit>}"

MOORES_WT_BIN="${MOORES_WT_BIN:-moores-wt}"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity}"
STEAMCMD_BIN="${STEAMCMD_BIN:-steamcmd}"
VERIFY_SCRIPT="${VERIFY_SCRIPT:-$SCRIPT_DIR/verify-on-windows.sh}"
PLAYTEST_RUN_ROOT="${PLAYTEST_RUN_ROOT:-$HOME/hermes-agent/data/services/playtest/runs}"

# 必須envはビルド前に全部そろっているか見る（半端に焼いてから落ちない）
# Check every required env before building so a half-baked artifact never happens
missing=""
[ -n "${MOORESTECH_STEAM_USER:-}" ] || missing="$missing MOORESTECH_STEAM_USER"
[ -n "${MOORESTECH_STEAM_DEPOT_ID:-}" ] || missing="$missing MOORESTECH_STEAM_DEPOT_ID"
if [ -n "$missing" ]; then
    echo "ERROR: 必須の環境変数が未設定です:$missing (~/hermes-agent/data/services/playtest/env.sh を読み込んでください)" >&2
    exit 2
fi

BUILD_LABEL="${MOORESTECH_STEAM_BUILD_LABEL:-playtest-$(date +%Y%m%d-%H%M)}"
RUN_DIR="$PLAYTEST_RUN_ROOT/$BUILD_LABEL"
BUILD_DIR="$RUN_DIR/build"
STEAM_DIR="$RUN_DIR/steam"
mkdir -p "$BUILD_DIR" "$STEAM_DIR/output"
echo "[release-playtest] label=$BUILD_LABEL commit=$COMMIT run=$RUN_DIR"

# 使い捨てworktreeで焼く（メインワークツリーのEditorとブランチを触らない）
# Bake in a disposable worktree so the main worktree's Editor and branch stay untouched
BRANCH="playtest/build-${COMMIT:0:8}"
WORKTREE="$("$MOORES_WT_BIN" new "$BRANCH" --from "$COMMIT" --no-editor --fetch | tail -n 1)"
if [ ! -d "$WORKTREE/moorestech_client" ]; then
    echo "ERROR: worktreeを作れませんでした: $WORKTREE" >&2
    exit 3
fi

MOORESTECH_BUILD_OUTPUT="$BUILD_DIR" MOORESTECH_STEAM_BUILD_LABEL="$BUILD_LABEL" \
    "$UNITY_BIN" -batchmode -nographics \
    -projectPath "$WORKTREE/moorestech_client" \
    -executeMethod Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild \
    -logFile "$RUN_DIR/unity-build.log"

# 成果物の必須構成を検査する（欠けたままSteamへ上げない）
# Verify the artifact layout so nothing incomplete reaches Steam
BUILD_INFO="$BUILD_DIR/moorestech_Data/StreamingAssets/build-info.json"
for required in "$BUILD_DIR/moorestech.exe" "$BUILD_DIR/game/mods" "$BUILD_INFO"; do
    if [ ! -e "$required" ]; then
        echo "ERROR: 成果物に $required がありません" >&2
        exit 4
    fi
done
if ! grep -q "\"steamBuildLabel\"[[:space:]]*:[[:space:]]*\"$BUILD_LABEL\"" "$BUILD_INFO"; then
    echo "ERROR: build-info.json に steamBuildLabel=$BUILD_LABEL が焼かれていません（共有契約 Global Constraints §1 のキー名）" >&2
    exit 4
fi

# vdfのトークンを差し込む（depot idはアカウント固有なのでrepoへ書かない）
# sedの区切り文字はパスに現れないASCII制御文字を使い、RUN_DIR/BUILD_DIRに'|'を含む環境でも壊れないようにする
# Substitute the vdf tokens; the depot id is account-specific and never committed to the repo
# The sed delimiter is a control char that paths never contain, so a '|' in RUN_DIR/BUILD_DIR cannot break it
SED_DELIM=$'\x01'
for template in app_build_playtest.vdf depot_build_windows.vdf; do
    sed -e "s${SED_DELIM}__BUILD_LABEL__${SED_DELIM}${BUILD_LABEL}${SED_DELIM}g" \
        -e "s${SED_DELIM}__RUN_DIR__${SED_DELIM}${RUN_DIR}${SED_DELIM}g" \
        -e "s${SED_DELIM}__CONTENT_ROOT__${SED_DELIM}${BUILD_DIR}${SED_DELIM}g" \
        -e "s${SED_DELIM}__DEPOT_ID__${SED_DELIM}${MOORESTECH_STEAM_DEPOT_ID}${SED_DELIM}g" \
        "$SCRIPT_DIR/steam/$template" >"$STEAM_DIR/$template"
done

"$STEAMCMD_BIN" +login "$MOORESTECH_STEAM_USER" +run_app_build "$STEAM_DIR/app_build_playtest.vdf" +quit

# 検証機の通し検証に通ったものだけを告知対象にする
# Only a build that passed the check machine becomes announceable
"$VERIFY_SCRIPT" "$BUILD_LABEL"

cat >"$RUN_DIR/announce.md" <<EOF
# moorestech プレイテスト更新 ($BUILD_LABEL)

- コミット: $COMMIT
- Steam ブランチ: playtest
- 通し検証: 合格（検証機で phase1 / phase2 とも成功）

Steam クライアントを再起動すると自動で更新されます。更新後に不具合があれば、ポーズメニューの報告からお知らせください。
EOF
echo "[release-playtest] announce: $RUN_DIR/announce.md"

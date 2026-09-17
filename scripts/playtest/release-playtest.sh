#!/usr/bin/env bash
# 指定コミットからWindows配布ビルドを焼き、Steamのplaytestブランチへ上げ、検証機で通し検証する
# Bakes the Windows distribution build from a commit, ships it to the Steam playtest branch and verifies it on the check machine
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
VERIFY_SCRIPT="${VERIFY_SCRIPT:-$SCRIPT_DIR/verify-on-windows.sh}"
PLAYTEST_RUN_ROOT="${PLAYTEST_RUN_ROOT:-$HOME/hermes-agent/data/services/playtest/runs}"
# build-info.json の branch に焼く配布元 ref。使い捨て worktree の一時ブランチ名を焼かないために明示する
# The distribution ref baked into build-info.json's branch, so the disposable worktree's temporary branch name is never baked
BUILD_BRANCH="${MOORESTECH_BUILD_BRANCH:-master}"

# 必須envはビルド前に全部そろっているか見る（半端に焼いてから落ちない）
# Check every required env before building so a half-baked artifact never happens
missing=""
[ -n "${MOORESTECH_STEAM_USER:-}" ] || missing="$missing MOORESTECH_STEAM_USER"
[ -n "${MOORESTECH_STEAM_DEPOT_ID:-}" ] || missing="$missing MOORESTECH_STEAM_DEPOT_ID"
if [ -n "$missing" ]; then
    echo "ERROR: 必須の環境変数が未設定です:$missing (~/hermes-agent/data/services/playtest/env.sh を読み込んでください)" >&2
    exit 2
fi

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

BUILD_LABEL="${MOORESTECH_STEAM_BUILD_LABEL:-playtest-$(date +%Y%m%d-%H%M)}"
RUN_DIR="$PLAYTEST_RUN_ROOT/$BUILD_LABEL"
BUILD_DIR="$RUN_DIR/build"
STEAM_DIR="$RUN_DIR/steam"
# RUN_DIRの再利用は前回の成果物・announce.mdを黙って読ませる温床になるためfail-closedで拒否する
# Reusing RUN_DIR would silently read a previous run's artifacts/announce.md, so refuse it fail-closed
if [ -e "$RUN_DIR" ]; then
    echo "ERROR: RUN_DIR が既に存在します（前回実行の残骸の可能性）: ${RUN_DIR}" >&2
    exit 2
fi
mkdir -p "$BUILD_DIR" "$STEAM_DIR/output"
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

# ビルドが同梱する master data（worktree + ピンの relativePath。GameDataBundler.MasterDataRepositoryRoot と同じ解決）の HEAD を
# コミット済みのピンと突き合わせる。ずれたまま焼くと strict が数十分後に落ちるか、ずれたマスタを同梱するため、合わせるのは人に任せて止まる
# Match the HEAD of the master data the build bundles (worktree + the pin's relativePath, the same resolution as
# GameDataBundler.MasterDataRepositoryRoot) against the committed pin; baking with drift fails strict much later or ships the wrong
# master, so stop and leave the realignment to a human
PIN_FIELDS="$("$GIT_BIN" -C "$WORKTREE" show HEAD:.moorestech-external-revisions.json | python3 -c '
import json, sys
for revision in json.load(sys.stdin)["repositories"]:
    if revision["key"] == "moorestech_master":
        print(revision["relativePath"]); print(revision["commitHash"]); sys.exit(0)
sys.exit(1)
')" || {
    echo "ERROR: worktree のコミット済みピンから moorestech_master の relativePath/commitHash を読めません: ${WORKTREE}/.moorestech-external-revisions.json" >&2
    exit 3
}
MASTER_RELATIVE_PATH="$(printf '%s\n' "$PIN_FIELDS" | sed -n 1p)"
PINNED_MASTER_COMMIT="$(printf '%s\n' "$PIN_FIELDS" | sed -n 2p)"
MASTER_DATA_ROOT="$(python3 -c 'import os, sys; print(os.path.normpath(os.path.join(sys.argv[1], sys.argv[2])))' "$WORKTREE" "$MASTER_RELATIVE_PATH")"
MASTER_HEAD="$("$GIT_BIN" -C "$MASTER_DATA_ROOT" rev-parse HEAD)" || {
    echo "ERROR: 同梱元の master data の HEAD を読めません: ${MASTER_DATA_ROOT}（ピン ${PINNED_MASTER_COMMIT} のチェックアウトを用意してください）" >&2
    exit 3
}
if [ "$MASTER_HEAD" != "$PINNED_MASTER_COMMIT" ]; then
    echo "ERROR: 同梱元の master data がピンとずれています: ${MASTER_DATA_ROOT} は ${MASTER_HEAD}、ピンは ${PINNED_MASTER_COMMIT}。${MASTER_DATA_ROOT} を ${PINNED_MASTER_COMMIT} へ合わせてから再実行してください" >&2
    exit 3
fi

MOORESTECH_BUILD_OUTPUT="$BUILD_DIR" MOORESTECH_STEAM_BUILD_LABEL="$BUILD_LABEL" MOORESTECH_BUILD_BRANCH="$BUILD_BRANCH" \
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
# steamBuildLabel・成果物のcommitが指定コミットと一致すること・targetが存在することを検査する。
# grepの文字列一致だけではstaleなworktreeや取り違えた成果物のcommitずれに気づけない
# Verify steamBuildLabel, that the artifact's commit matches the requested commit, and that target
# is present; a plain grep match alone cannot catch a stale worktree or a mismatched artifact's commit
if ! BUILD_LABEL="$BUILD_LABEL" COMMIT="$COMMIT_FULL" BRANCH="$BUILD_BRANCH" python3 -c '
import json, os, sys
info = json.load(open(sys.argv[1]))
for key, env in (("steamBuildLabel", "BUILD_LABEL"), ("commit", "COMMIT"), ("branch", "BRANCH")):
    if info.get(key) != os.environ[env]:
        print(f"{key} mismatch: got {info.get(key)!r} want {os.environ[env]!r}", file=sys.stderr)
        sys.exit(1)
if not info.get("target"):
    print("target is missing", file=sys.stderr)
    sys.exit(1)
' "$BUILD_INFO"; then
    echo "ERROR: build-info.json の内容が指定コミット/ラベルと一致しません（共有契約 Global Constraints §1 のキー名。stale worktreeや取り違えた成果物の可能性）: ${BUILD_INFO}" >&2
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

- コミット: $COMMIT_FULL
- Steam ブランチ: playtest
- 通し検証: 合格（検証機で phase1 / phase2 とも成功）

Steam クライアントを再起動すると自動で更新されます。更新後に不具合があれば、ポーズメニューの報告からお知らせください。
EOF
echo "[release-playtest] announce: $RUN_DIR/announce.md"

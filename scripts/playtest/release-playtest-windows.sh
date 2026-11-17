#!/usr/bin/env bash
# 指定コミットの Windows 版と Mac（Apple Silicon）版を配布ビルド機（Windows）で焼き、Mac で検査して Steam へ上げ Windows を検証する（配布の標準経路）
# Mac で焼く release-playtest.sh は、配布ビルド機が使えないときの予備経路として残す
# Bake Windows and Mac (Apple Silicon) players of a commit on the Windows build machine, inspect them on the Mac, upload to Steam and verify Windows (the standard route)
# release-playtest.sh, which bakes on the Mac, stays as the fallback when the Windows build machine is unavailable
#
# usage: release-playtest-windows.sh <commit>
# <commit> は SHA か origin/<branch> を渡す。配布ビルド機の ssh 宛先は MOORESTECH_WINBUILD_HOST（~/hermes-agent/data/services/playtest/env.sh）
# <commit> is a SHA or origin/<branch>; the build machine's ssh target is MOORESTECH_WINBUILD_HOST (~/hermes-agent/data/services/playtest/env.sh)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
COMMIT="${1:?usage: release-playtest-windows.sh <commit>}"

GIT_BIN="${GIT_BIN:-git}"
STEAMCMD_BIN="$SCRIPT_DIR/steam_auth/run.py"
CODESIGN_BIN="${CODESIGN_BIN:-/usr/bin/codesign}"
LIPO_BIN="${LIPO_BIN:-/usr/bin/lipo}"
VERIFY_SCRIPT="${VERIFY_SCRIPT:-$SCRIPT_DIR/verify-on-windows.sh}"
PLAYTEST_RUN_ROOT="${PLAYTEST_RUN_ROOT:-$HOME/hermes-agent/data/services/playtest/runs}"
# build-info.json の branch に焼く配布元 ref
# The distribution ref baked into build-info.json's branch
BUILD_BRANCH="${MOORESTECH_BUILD_BRANCH:-master}"

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
# shellcheck source=lib/release-windows-bake.sh
. "$SCRIPT_DIR/lib/release-windows-bake.sh"

# 配布ビルド機の宛先も、ほかの必須 env と同じくビルド前に見る
# The build machine's target is checked before building, like the other required env
if [ -z "${MOORESTECH_WINBUILD_HOST:-}" ]; then
    echo "ERROR: MOORESTECH_WINBUILD_HOST（配布ビルド機の ssh 宛先）が必要です" >&2
    exit 2
fi
release_claim_run

# Windows で両方を焼いて持ち帰り、Mac で構成・出所・署名・CPU・実行権を検査する
# Bake both on Windows, bring them back, and inspect layout, origin, signature, CPU and executable bits on the Mac
release_bake_on_windows
release_require_windows_artifact "$RUN_DIR/build-windows"
release_require_mac_artifact "$RUN_DIR/build-mac"
release_deliver

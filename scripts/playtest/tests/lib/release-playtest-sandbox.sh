#!/usr/bin/env bash
# release-playtest.sh の契約テストが共有するスタブ群と実行関数。test-release-playtest*.sh から source する
# 各 make_sandbox が作った一時ディレクトリは SANDBOXES に積み、終了時にまとめて削除する
# Shared stubs and runner for the release-playtest.sh contract tests, sourced by test-release-playtest*.sh
# Every temp dir created by make_sandbox is tracked in SANDBOXES and removed together on exit

TARGET="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/release-playtest.sh"
FAILURES=0
COMMIT="93ddfdab3ffffffffffffffffffffffffffffffff"
PINNED_MASTER="c219a2f56c327f33a53dfb0b6994f63bd362220b"

fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

SANDBOXES=()
cleanup() { for dir in "${SANDBOXES[@]}"; do rm -rf "$dir"; done; }
trap cleanup EXIT

finish_contract() {
    if [ "$FAILURES" -ne 0 ]; then
        echo "FAILED: $FAILURES contract checks"
        exit 1
    fi
    echo "PASS: $1"
}

make_sandbox() {
    SANDBOX="$(mktemp -d)"
    SANDBOXES+=("$SANDBOX")
    mkdir -p "$SANDBOX/bin" "$SANDBOX/runs" "$SANDBOX/moorestech_master"

    # 呼び出し順と引数を1ファイルへ記録するスタブ群（bash 3.2 で動く書き方に限る）
    # git は -C の対象ごとに答えを分け、照合対象（worktree／同梱元 master）の取り違えをテストで検出できるようにする
    # Stubs recording invocation order and arguments into one file (written for bash 3.2)
    # The git stub answers per -C target, so mixing up the compared repo (worktree vs bundled master) is caught by the tests
    cat >"$SANDBOX/bin/git" <<EOF
#!/bin/bash
echo "git \$*" >>"$SANDBOX/calls.log"
case "\$*" in
  *"rev-parse --verify"*)
    [ "\${GIT_VERIFY_EXIT:-0}" = "0" ] || exit "\${GIT_VERIFY_EXIT}"
    echo "\${GIT_VERIFY_OUTPUT:-$COMMIT}"
    ;;
  *"merge-base --is-ancestor"*)
    exit "\${GIT_ANCESTOR_EXIT:-0}"
    ;;
  "-C $SANDBOX/wt rev-parse HEAD")
    echo "\${GIT_HEAD_OUTPUT:-\${GIT_VERIFY_OUTPUT:-$COMMIT}}"
    ;;
  "-C $SANDBOX/wt show HEAD:.moorestech-external-revisions.json")
    printf '{"repositories":[{"key":"moorestech_master","relativePath":"../moorestech_master","commitHash":"%s"}]}' "$PINNED_MASTER"
    ;;
  "-C $SANDBOX/moorestech_master rev-parse HEAD")
    [ "\${MASTER_HEAD_EXIT:-0}" = "0" ] || exit "\${MASTER_HEAD_EXIT}"
    echo "\${MASTER_HEAD_OUTPUT:-$PINNED_MASTER}"
    ;;
  *"rev-parse"*|*" show "*)
    echo "unexpected git target: \$*" >&2
    exit 128
    ;;
esac
EOF
    cat >"$SANDBOX/bin/moores-wt" <<EOF
#!/bin/bash
echo "moores-wt \$*" >>"$SANDBOX/calls.log"
case "\$1" in
  rm) exit "\${MOORES_WT_RM_EXIT:-0}" ;;
esac
# 既存の(stale)worktreeディレクトリを模して先に作ってから失敗するケースをMOORES_WT_EXITで再現する
# MOORES_WT_EXIT reproduces a stale worktree dir that already exists before the command fails
mkdir -p "$SANDBOX/wt/moorestech_client"
echo "$SANDBOX/wt"
exit "\${MOORES_WT_EXIT:-0}"
EOF
    cat >"$SANDBOX/bin/unity" <<EOF
#!/bin/bash
echo "unity \$* branch=\$MOORESTECH_BUILD_BRANCH" >>"$SANDBOX/calls.log"
[ "\${UNITY_EXIT:-0}" = "0" ] || exit "\${UNITY_EXIT}"
mkdir -p "\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets" "\$MOORESTECH_BUILD_OUTPUT/game/mods"
touch "\$MOORESTECH_BUILD_OUTPUT/moorestech.exe"
printf '{"commit":"%s","branch":"%s","steamBuildLabel":"%s","target":"StandaloneWindows64"}' \\
  "\${BUILD_INFO_COMMIT:-$COMMIT}" "\${BUILD_INFO_BRANCH:-\$MOORESTECH_BUILD_BRANCH}" "\$MOORESTECH_STEAM_BUILD_LABEL" \\
  >"\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets/build-info.json"
EOF
    cat >"$SANDBOX/bin/steamcmd" <<EOF
#!/bin/bash
echo "steamcmd \$*" >>"$SANDBOX/calls.log"
exit "\${STEAMCMD_EXIT:-0}"
EOF
    cat >"$SANDBOX/bin/verify" <<EOF
#!/bin/bash
echo "verify \$*" >>"$SANDBOX/calls.log"
exit "\${VERIFY_EXIT:-0}"
EOF
    chmod +x "$SANDBOX/bin/"*
}

run_target() {
    ( cd "$SANDBOX" && \
      MOORESTECH_STEAM_USER="${MOORESTECH_STEAM_USER-steamuser}" \
      MOORESTECH_STEAM_DEPOT_ID="${MOORESTECH_STEAM_DEPOT_ID-1958161}" \
      MOORESTECH_STEAM_BUILD_LABEL="${MOORESTECH_STEAM_BUILD_LABEL-}" \
      MOORESTECH_BUILD_BRANCH="${MOORESTECH_BUILD_BRANCH-}" \
      GIT_BIN="$SANDBOX/bin/git" \
      MOORES_WT_BIN="$SANDBOX/bin/moores-wt" UNITY_BIN="$SANDBOX/bin/unity" \
      STEAMCMD_BIN="$SANDBOX/bin/steamcmd" VERIFY_SCRIPT="$SANDBOX/bin/verify" \
      PLAYTEST_RUN_ROOT="$SANDBOX/runs" \
      UNITY_EXIT="${UNITY_EXIT-0}" STEAMCMD_EXIT="${STEAMCMD_EXIT-0}" VERIFY_EXIT="${VERIFY_EXIT-0}" \
      MOORES_WT_EXIT="${MOORES_WT_EXIT-0}" MOORES_WT_RM_EXIT="${MOORES_WT_RM_EXIT-0}" \
      GIT_VERIFY_EXIT="${GIT_VERIFY_EXIT-0}" GIT_VERIFY_OUTPUT="${GIT_VERIFY_OUTPUT-}" GIT_HEAD_OUTPUT="${GIT_HEAD_OUTPUT-}" \
      GIT_ANCESTOR_EXIT="${GIT_ANCESTOR_EXIT-0}" MASTER_HEAD_EXIT="${MASTER_HEAD_EXIT-0}" MASTER_HEAD_OUTPUT="${MASTER_HEAD_OUTPUT-}" \
      BUILD_INFO_COMMIT="${BUILD_INFO_COMMIT-$COMMIT}" BUILD_INFO_BRANCH="${BUILD_INFO_BRANCH-}" \
      bash "$TARGET" "${RELEASE_ARG-$COMMIT}" 2>&1 )
}

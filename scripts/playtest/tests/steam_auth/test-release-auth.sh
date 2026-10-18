#!/usr/bin/env bash
# 保存資格不在ならworktreeもビルドも開始しない。
# Missing cached authentication must stop before worktree creation or building.
set -uo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"
make_sandbox
STEAM_AUTH_EXIT=5 run_target >"$SANDBOX/release.log" 2>&1 && fail "missing cached login succeeded"
grep -q 'cached Steam login unavailable' "$SANDBOX/release.log" || fail "missing diagnostic"
grep -Eq '^(moores-wt|unity|steamcmd) ' "$SANDBOX/calls.log" && fail "build or upload started"
[ "$FAILURES" -eq 0 ] || exit 1
echo 'PASS: release authentication preflight'

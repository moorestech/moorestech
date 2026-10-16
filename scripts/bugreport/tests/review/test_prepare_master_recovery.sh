#!/usr/bin/env bash
# 取得手段が両方失敗しても、報告SHAを復元したrun.envは利用できる
# A run.env remains usable when both fetch paths fail but the reported SHA is restored
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "${TMP:?}"' EXIT
source "$HERE/review/git-fixture.sh"
mv "$TMP/master-origin.git" "$TMP/master-origin.offline"
run="$TMP/runs/fetch-failed"
mkdir -p "$run/repo"
printf 'invalid bundle' > "$run/repo/master-commits.bundle"
cat > "$run/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT"},"masterData":{"commit":"$MASTER_COMMIT"},"serverData":{"relativeTo":"masterData","relativePath":"server_v8"},"snapshotTicks":[]}
JSON
MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" fetch-failed 2> "$TMP/result.log"
source "$run/run.env"
[ "$MASTER_FAILED" = 0 ]
[ "$MASTER_DIR" = "$TMP/mwt/bugfix-fetch-failed/server_v8" ]
[ "$SERVER_DATA_DIR" = "$MASTER_DIR" ]
[ "$(git -C "$MASTER_DIR/.." rev-parse HEAD)" = "$MASTER_COMMIT" ]
grep -q 'origin/master の fetch に失敗' "$TMP/result.log"
grep -q 'master-commits.bundle を取り込めなかった' "$TMP/result.log"
echo 'PASS master recovery despite both fetch failures'

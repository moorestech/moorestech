#!/usr/bin/env bash
# 一時 bare repo と作業クローンで prepare-run.sh の git 操作列と world/ 組み立てを検証する
# Verifies prepare-run.sh's git sequence and world/ assembly using a temp bare repo and clone
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "${TMP:?}"' EXIT

# origin と作業クローン（moorestech 相当）を作る。既定ブランチ名は master に固定する
# Create an origin and a working clone (stands in for moorestech), pinning the default branch to master
git -c init.defaultBranch=master init -q --bare "$TMP/origin.git"
git -c init.defaultBranch=master clone -q "$TMP/origin.git" "$TMP/repo"
( cd "$TMP/repo" && git config user.email t@t && git config user.name t && mkdir -p moorestech_client/Library && echo lib > moorestech_client/Library/x && echo a > a.txt && git add a.txt && git commit -qm base && git push -q origin master )
# 報告コミットは未push（bundle 経由で届く）
# The report commit is unpushed (arrives via bundle)
( cd "$TMP/repo" && echo b > b.txt && git add b.txt && git commit -qm report )
REPORT_COMMIT="$(git -C "$TMP/repo" rev-parse HEAD)"
git -C "$TMP/repo" update-ref refs/bugreport/r1 "$REPORT_COMMIT"
git -C "$TMP/repo" bundle create -q "$TMP/commits.bundle" "origin/master..refs/bugreport/r1"
git -C "$TMP/repo" update-ref -d refs/bugreport/r1

# master data 相当
# Stand-in for master data
git init -q "$TMP/master"; ( cd "$TMP/master" && git config user.email t@t && git config user.name t && mkdir -p server_v8/mods && echo m > server_v8/mods/x && git add . && git commit -qm m )
MASTER_COMMIT="$(git -C "$TMP/master" rev-parse HEAD)"

RUN="$TMP/runs/r1"; mkdir -p "$RUN/repo" "$RUN/snapshots" "$RUN/world"
cp "$TMP/commits.bundle" "$RUN/repo/commits.bundle"
printf 'diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-a\n+changed\n' > "$RUN/repo/head.diff"
mkdir -p "$RUN/repo/untracked/new" && echo n > "$RUN/repo/untracked/new/file.txt"
echo '{"seed":0}' > "$RUN/world/world.json"; echo '{}' > "$RUN/world/map.json"
echo '{"currentTick":600}' > "$RUN/snapshots/tick_600.json"; echo '{"currentTick":1200}' > "$RUN/snapshots/tick_1200.json"
cat > "$RUN/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT","branch":"feature/x","dirty":true},"masterData":{"commit":"$MASTER_COMMIT","dirty":false},"snapshotTicks":[600,1200]}
JSON

MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" r1

. "$RUN/run.env"
[ "$REPORT_COMMIT" = "$(git -C "$WORKTREE" rev-parse HEAD)" ] || { echo "NG: HEAD が報告コミットでない"; exit 1; }
[ "$(git -C "$WORKTREE" rev-parse --abbrev-ref HEAD)" = "bugfix/r1" ] || { echo "NG: ブランチ"; exit 1; }
[ "$(cat "$WORKTREE/a.txt")" = "changed" ] || { echo "NG: diff 未適用"; exit 1; }
[ -f "$WORKTREE/new/file.txt" ] || { echo "NG: 未追跡コピー"; exit 1; }
[ -f "$WORKTREE/b.txt" ] || { echo "NG: bundle のコミットが取り込まれていない"; exit 1; }
[ -f "$WORKTREE/moorestech_client/Library/x" ] || { echo "NG: Library コピー"; exit 1; }
[ "$MASTER_DIR" = "$TMP/mwt/bugfix-r1/server_v8" ] || { echo "NG: MASTER_DIR=$MASTER_DIR"; exit 1; }
[ "$LATEST_TICK" = "1200" ] || { echo "NG: LATEST_TICK=$LATEST_TICK"; exit 1; }
[ "$(cat "$WORLD_DIR/save.json")" = '{"currentTick":1200}' ] || { echo "NG: save.json"; exit 1; }
[ "$COMMIT_MISSING" = "0" ] && [ "$DIFF_APPLY_FAILED" = "0" ] || { echo "NG: フラグ"; exit 1; }

# 二重準備は既存 worktree を消さずに失敗する
# A second preparation fails instead of destroying the existing worktree
if MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
    bash "$HERE/../prepare-run.sh" r1 2>"$TMP/second.log"; then
  echo "NG: worktree が既にあるのに成功した"; exit 1
fi
grep -q "二重準備を避けて中断" "$TMP/second.log" || { echo "NG: 中断理由がログされていない"; exit 1; }
[ -f "$WORKTREE/b.txt" ] || { echo "NG: 既存 worktree が壊された"; exit 1; }

# 報告コミットが手に入らない場合は origin/master を土台にしてフラグを立てる
# When the report commit is unavailable, fall back to origin/master and raise the flag
RUN2="$TMP/runs/r2"; mkdir -p "$RUN2/repo" "$RUN2/snapshots"
echo '{"currentTick":300}' > "$RUN2/snapshots/tick_300.json"
cat > "$RUN2/manifest.json" <<JSON
{"repository":{"commit":"0000000000000000000000000000000000000000","branch":"feature/y","dirty":false},"masterData":{"commit":"","dirty":false},"snapshotTicks":[300]}
JSON
MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" r2 2>"$TMP/r2.log"
grep -q "報告コミットが無いため" "$TMP/r2.log" || { echo "NG: 欠損コミットの理由がログされていない"; exit 1; }
( . "$RUN2/run.env"; [ "$COMMIT_MISSING" = "1" ] && [ -z "$MASTER_DIR" ] ) || { echo "NG: r2 のフラグ/MASTER_DIR"; exit 1; }
echo OK

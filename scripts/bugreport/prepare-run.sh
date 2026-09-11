#!/usr/bin/env bash
# 報告時のコミット＋差分で隔離 worktree を作り、master data worktree・Library・world/ を用意して run.env を書く
# Builds an isolated worktree at the report commit plus diff, the master-data worktree, Library, world/, and run.env
set -euo pipefail
ID="${1:?run id}"
LOGS="${MOORESTECH_LOGS:-$HOME/hermes-agent/data/repos/moorestech_logs}"
REPO="${MOORESTECH_REPO:-$HOME/hermes-agent/data/repos/moorestech}"
WORKTREES="${MOORESTECH_WORKTREES:-$HOME/hermes-agent/data/repos/moorestech-worktrees}"
MASTER="${MOORESTECH_MASTER:-$HOME/hermes-agent/data/repos/moorestech_master}"
MASTER_WORKTREES="${MOORESTECH_MASTER_WORKTREES:-$HOME/hermes-agent/data/repos/moorestech-master-worktrees}"
RUN="$LOGS/runs/$ID"; [ -d "$RUN" ] || RUN="$LOGS/harness/bug-report/runs/$ID"
log() { echo "[prepare] $*" >&2; }

[ -d "$RUN" ] || { log "run ディレクトリが無い: $RUN"; exit 1; }
[ -f "$RUN/manifest.json" ] || { log "manifest.json が無い: $RUN/manifest.json"; exit 1; }

json() { python3 -c "import json,sys;d=json.load(open(sys.argv[1]));print(eval(sys.argv[2]))" "$RUN/manifest.json" "$1"; }
REPORT_COMMIT="$(json "d['repository']['commit']")"
REPORT_BRANCH="$(json "d['repository']['branch']")"
MASTER_COMMIT="$(json "d['masterData']['commit']")"
LATEST_TICK="$(json "max(d['snapshotTicks'])")"

WORKTREE="$WORKTREES/bugfix-$ID"; COMMIT_MISSING=0; DIFF_APPLY_FAILED=0
# 既存の worktree は消さずに失敗させる（他ランの作業物を巻き込まないため）
# Never delete an existing worktree; fail instead so another run's work is not destroyed
[ -e "$WORKTREE" ] && { log "worktree が既に存在する。二重準備を避けて中断: $WORKTREE"; exit 1; }

git -C "$REPO" fetch -q origin master
[ -f "$RUN/repo/commits.bundle" ] && git -C "$REPO" fetch -q "$RUN/repo/commits.bundle" '+refs/bugreport/*:refs/bugreport/*' 2>/dev/null || true
if git -C "$REPO" cat-file -e "$REPORT_COMMIT^{commit}" 2>/dev/null; then base="$REPORT_COMMIT"; else base="origin/master"; COMMIT_MISSING=1; log "報告コミットが無いため origin/master を土台にする: $REPORT_COMMIT"; fi
git -C "$REPO" worktree add -q -b "bugfix/$ID" "$WORKTREE" "$base"
if [ -s "$RUN/repo/head.diff" ]; then
  git -C "$WORKTREE" apply --whitespace=nowarn "$RUN/repo/head.diff" || { DIFF_APPLY_FAILED=1; log "head.diff の適用に失敗"; }
fi
[ -d "$RUN/repo/untracked" ] && cp -R "$RUN/repo/untracked/." "$WORKTREE/"

# master data も報告時の実チェックアウト値で worktree を切る（ピンではなく manifest の値）
# The master-data worktree also uses the manifest's actual checkout, not the pin
MASTER_DIR=""
if [ -n "$MASTER_COMMIT" ] && [ -d "$MASTER" ]; then
  [ -f "$RUN/repo/master-commits.bundle" ] && git -C "$MASTER" fetch -q "$RUN/repo/master-commits.bundle" '+refs/bugreport/*:refs/bugreport/*' 2>/dev/null || true
  if [ -e "$MASTER_WORKTREES/bugfix-$ID" ]; then
    log "master worktree が既に存在するため再利用する: $MASTER_WORKTREES/bugfix-$ID"
  else
    git -C "$MASTER" worktree add -q --detach "$MASTER_WORKTREES/bugfix-$ID" "$MASTER_COMMIT"
    [ -s "$RUN/repo/master.diff" ] && git -C "$MASTER_WORKTREES/bugfix-$ID" apply --whitespace=nowarn "$RUN/repo/master.diff" || true
  fi
  MASTER_DIR="$MASTER_WORKTREES/bugfix-$ID/server_v8"
else
  log "master data の worktree を作らない（commit='$MASTER_COMMIT' repo='$MASTER'）"
fi

# Library は APFS クローン（AGENTS.md）。無ければ初回インポートに任せる
# Library via APFS clone (AGENTS.md); fall back to a first import when absent
if [ -d "$REPO/moorestech_client/Library" ] && [ ! -d "$WORKTREE/moorestech_client/Library" ]; then
  mkdir -p "$WORKTREE/moorestech_client"
  cp -Rc "$REPO/moorestech_client/Library" "$WORKTREE/moorestech_client/Library" 2>/dev/null \
    || { log "APFS クローンに失敗したため通常コピーにする"; cp -R "$REPO/moorestech_client/Library" "$WORKTREE/moorestech_client/Library"; }
fi

# world/: 最新スナップショットを save.json にして固定ワールド起動できる形にする
# world/: place the latest snapshot as save.json so a fixed-world boot can load it
WORLD_DIR="$RUN/world"; mkdir -p "$WORLD_DIR"
if [ -f "$RUN/snapshots/tick_$LATEST_TICK.json" ]; then
  cp "$RUN/snapshots/tick_$LATEST_TICK.json" "$WORLD_DIR/save.json"
else
  log "スナップショットが無いため save.json を置けない: $RUN/snapshots/tick_$LATEST_TICK.json"
fi

cat > "$RUN/run.env" <<ENV
WORKTREE=$WORKTREE
MASTER_DIR=$MASTER_DIR
WORLD_DIR=$WORLD_DIR
REPORT_COMMIT=$REPORT_COMMIT
REPORT_BRANCH=$REPORT_BRANCH
LATEST_TICK=$LATEST_TICK
COMMIT_MISSING=$COMMIT_MISSING
DIFF_APPLY_FAILED=$DIFF_APPLY_FAILED
ENV
log "prepared: $RUN/run.env"

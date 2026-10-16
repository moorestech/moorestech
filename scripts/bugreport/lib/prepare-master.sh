#!/usr/bin/env bash
# master data も報告時の実チェックアウト値で worktree を切る（ピンではなく manifest の値）
# The master-data worktree also uses the manifest's actual checkout, not the pin
MASTER_DIR=""
# master 側の復元も本体側と同じ扱いにする。差分・未追跡を当て、落ちたらフラグとログを必ず残す
# The master side restores exactly like the code side: apply the diff and untracked files, flag and log every failure
restore_master_worktree() {
  local dir="$1"
  if [ -s "$RUN/repo/master.diff" ]; then
    git -C "$dir" apply --whitespace=nowarn "$RUN/repo/master.diff" \
      || { MASTER_DIFF_APPLY_FAILED=1; log "master.diff の適用に失敗。マスタデータが報告時と違う状態で再現する"; }
  else
    MASTER_DIFF_ABSENT=1; log "master.diff が無い/空。マスタの未コミット差分を当てられない: $RUN/repo/master.diff"
  fi
  if [ -d "$RUN/repo/master-untracked" ]; then
    cp -R "$RUN/repo/master-untracked/." "$dir/" \
      || { MASTER_UNTRACKED_FAILED=1; log "マスタの未追跡ファイルのコピーに失敗した。未追跡分を欠いたまま続行する: $dir"; }
  else
    log "マスタの未追跡ファイルが箱に無い。未追跡分を欠いたまま続行する"
  fi
}
# 単独で呼ぶ入口でも同じ断片検証を通す
# Standalone callers use the same part verification
python3 "$(dirname "${BASH_SOURCE[0]}")/restore-commit-transfer.py" "$RUN" \
  || { log "masterコミット資料の復元に失敗したため中断"; return 1; }
if [ -n "$MASTER_COMMIT" ] && [ -d "$MASTER" ]; then
  # 差分bundleの前提をマスタ側でも取得し、取得失敗を復元欠損へ残す
  # Fetch master prerequisites before the incremental bundle and surface fetch failures as restoration gaps
  git -C "$MASTER" fetch -q origin master \
    || { log "master origin/master の fetch に失敗した。マスタ復元は未確認"; }
  if [ -f "$RUN/repo/master-commits.bundle" ]; then
    git -C "$MASTER" fetch -q "$RUN/repo/master-commits.bundle" '+refs/bugreport/*:refs/bugreport/*' \
      || { log "master-commits.bundle を取り込めなかった。マスタの未pushコミットは使えない: $RUN/repo/master-commits.bundle"; }
  else
    log "master-commits.bundle が箱に無い。マスタの報告コミットは手元の参照にある場合だけ使える"
  fi
  if [ -e "$MASTER_WORKTREES/bugfix-$ID" ]; then
    log "master worktree が既に存在するため再利用する: $MASTER_WORKTREES/bugfix-$ID"
  else
    if git -C "$MASTER" worktree add -q --detach "$MASTER_WORKTREES/bugfix-$ID" "$MASTER_COMMIT"; then
      restore_master_worktree "$MASTER_WORKTREES/bugfix-$ID"
    else
      log "master data の worktree を作れなかった（commit=${MASTER_COMMIT}）。master data 無しで続行する"
      MASTER_FAILED=1
    fi
  fi
  # 取得手段の診断ではなく、復元先HEADで最終判定する
  # Judge the restored HEAD rather than individual fetch attempts
  restored_master="$MASTER_WORKTREES/bugfix-$ID"
  if [ "$MASTER_FAILED" = "0" ] && [ "$(git -C "$restored_master" rev-parse HEAD)" = "$MASTER_COMMIT" ]; then
    MASTER_WORKTREE="$restored_master"
    MASTER_DIR="$MASTER_WORKTREE/server_v8"
  else
    MASTER_FAILED=1
    log "master復元先のHEADを確認できない: $restored_master commit=$MASTER_COMMIT"
  fi
else
  log "master data の worktree を作らない（commit='$MASTER_COMMIT' repo='$MASTER'）"
fi

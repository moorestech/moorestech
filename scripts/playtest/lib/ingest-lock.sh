#!/usr/bin/env bash
# 取り込みの単一飛行ロック。呼び出し側が LOCK と log を定義してから source する
# Single-flight lock for ingest; the caller defines LOCK and log before sourcing this

# ロックは「pid 入りのディレクトリ」を rename(2) で公開する。mkdir してから pid を書く形だと pid が空の瞬間に
# 他者が所有者死亡と誤判定して奪えたため、pid を書き終えてから原子的に置く。mv は既存ディレクトリの中へ
# 潜り込むので使わず、既存の非空ディレクトリへは失敗する os.rename を使う
# A lock is a pid-bearing directory published via rename(2). mkdir-then-write-pid left a moment with an empty pid
# that another run misread as a dead owner, so the pid is written first and the directory placed atomically.
# mv would nest into an existing directory, so os.rename (which fails onto a non-empty directory) is used instead
rename_directory_without_clobber() {
  python3 -c 'import os, sys; os.rename(sys.argv[1], sys.argv[2])' "$1" "$2" 2>/dev/null
}

publish_lock_with_pid() {
  local target="$1" staging="$1.staging.$$"
  rm -rf "$staging"
  { mkdir "$staging" && echo $$ > "$staging/pid"; } || { log "ロックの下書きを作れない（${staging}）"; rm -rf "$staging"; return 1; }
  rename_directory_without_clobber "$staging" "$target" && return 0
  rm -rf "$staging"
  return 1
}

# 所有者が死んだロックを rename で退避する。退避したものの pid が確認時と違えば生きたロックを掴んだので戻す
# Renames a dead owner's lock aside; if the evicted pid differs from the one checked, a live lock was grabbed, so restore it
evict_dead_lock() {
  local target="$1" dead_pid="$2" evicted="$1.evicted.$$"
  rm -rf "$evicted"
  rename_directory_without_clobber "$target" "$evicted" || { log "ロック退避で競り負けた（${target}）。次回に持ち越し"; return 1; }
  local evicted_pid; evicted_pid="$(cat "${evicted}/pid" 2>/dev/null || true)"
  if [ "$evicted_pid" != "$dead_pid" ]; then
    rename_directory_without_clobber "$evicted" "$target" || log "ERROR: 誤って退避した生存ロックを戻せない（${evicted}）"
    log "退避中に所有者が入れ替わった（pid=${evicted_pid:-不明}）。譲る（${target}）"
    return 1
  fi
  rm -rf "$evicted"
}

# 生きていれば pid を標準出力へ出して 0、所有者が死んでいれば（pid 不明含む）1
# Prints the owner pid and returns 0 while it lives; returns 1 when the owner is dead (including an unknown pid)
lock_owner_alive() {
  local owner_pid; owner_pid="$(cat "$1/pid" 2>/dev/null || true)"
  echo "$owner_pid"
  [ -n "$owner_pid" ] && kill -0 "$owner_pid" 2>/dev/null
}

# worker は nohup で切り離されており SIGKILL・OOM・再起動で EXIT trap が走らず残骸ロックが残りうるため、死んでいれば奪う。
# 奪取は別の奪取ロックの下で直列化する。直列化しないと、先に奪い終えた側の生きたロックを後続が確認済みの古い pid で退避できてしまう
# The detached nohup worker can die without its EXIT trap and leave an orphan lock, so a dead owner's lock is reclaimed.
# Reclaims are serialised under a separate reclaim lock; otherwise a later reclaimer could evict the first one's live lock on a stale check
acquire_lock() {
  publish_lock_with_pid "$LOCK" && return 0
  local owner_pid reclaim="${LOCK}.reclaim"
  if owner_pid="$(lock_owner_alive "$LOCK")"; then log "別の取り込みが進行中（pid=${owner_pid}, ${LOCK}）"; return 1; fi
  if ! publish_lock_with_pid "$reclaim"; then
    # 奪取ロック自体の残骸は退避だけして今回は譲る（次回の実行が奪取をやり直す）
    # A stale reclaim lock is only evicted and this run yields; the next run retries the reclaim
    local reclaimer_pid
    if reclaimer_pid="$(lock_owner_alive "$reclaim")"; then log "別の実行がロックを奪取中（pid=${reclaimer_pid}）"; return 1; fi
    evict_dead_lock "$reclaim" "$reclaimer_pid" && log "奪取ロックの残骸（pid=${reclaimer_pid:-不明}）を退避した。次回に奪取する"
    return 1
  fi
  # 奪取ロックの下で所有者を確認し直す。確認と退避の間に他者がロックを入れ替えられるのは奪取ロック保持者だけ
  # Re-check the owner under the reclaim lock; only the reclaim-lock holder can swap the lock between check and eviction
  local acquired=1
  if owner_pid="$(lock_owner_alive "$LOCK")"; then
    log "別の取り込みが進行中（pid=${owner_pid}, ${LOCK}）"
  elif [ ! -e "$LOCK" ] || evict_dead_lock "$LOCK" "$owner_pid"; then
    log "ロックの所有者（pid=${owner_pid:-不明}）が居ない/死んでいた。取得し直す（${LOCK}）"
    publish_lock_with_pid "$LOCK" && acquired=0 || log "奪取後のロック取得で他の実行に先を越された（${LOCK}）"
  fi
  rm -rf "$reclaim"
  return "$acquired"
}

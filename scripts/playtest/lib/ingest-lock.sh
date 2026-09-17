#!/usr/bin/env bash
# 取り込みの単一飛行ロック。呼び出し側が LOCK と log を定義してから source する
# Single-flight lock for ingest; the caller defines LOCK and log before sourcing this

# mkdir ロックに PID を添えて生存確認する。worker は nohup で切り離されており
# SIGKILL・OOM・再起動で EXIT trap が走らず残骸ロックが残りうるため、死んでいれば奪う
# The mkdir lock carries a PID so a stale one can be reclaimed: the detached
# nohup worker can die without the EXIT trap running, leaving an orphan lock
acquire_lock() {
  if mkdir "$LOCK" 2>/dev/null; then
    echo $$ > "$LOCK/pid"
    return 0
  fi
  local owner_pid
  owner_pid="$(cat "${LOCK}/pid" 2>/dev/null || true)"
  if [ -n "$owner_pid" ] && kill -0 "$owner_pid" 2>/dev/null; then
    log "別の取り込みが進行中（pid=${owner_pid}, ${LOCK}）"
    return 1
  fi
  log "ロックの所有者（pid=${owner_pid:-不明}）が死んでいる。奪取する（${LOCK}）"
  rm -rf "$LOCK"
  mkdir "$LOCK" 2>/dev/null || { log "ロック奪取に失敗（${LOCK}）"; return 1; }
  echo $$ > "$LOCK/pid"
  return 0
}

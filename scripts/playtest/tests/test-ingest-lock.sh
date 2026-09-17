#!/usr/bin/env bash
# 取り込みロックの排他（同時取得・残骸ロックの同時奪取で勝者が1本だけ・pid の無いロックが公開されない）を検証する
# Verifies the ingest lock: one winner under simultaneous acquisition and simultaneous stale reclaim, and no pid-less lock is ever published
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
LOCK="$TMP/ingest.lock"
CONTENDERS=8

# 各競争者は FIFO の開放を合図に一斉に acquire_lock を呼び、勝者は他者の判定が終わるまで生きて保持する
# Every contender calls acquire_lock at the FIFO signal; a winner stays alive holding the lock until the others have decided
race() {
  local fifo="$TMP/start.fifo" results="$TMP/results.txt" i
  rm -f "$fifo" "$results"; mkfifo "$fifo"
  for i in $(seq 1 "$CONTENDERS"); do
    LOCK="$LOCK" LIB="$HERE/../lib/ingest-lock.sh" FIFO="$fifo" RESULTS="$results" bash -c '
      log() { echo "[lock-test $$] $*" >> "${RESULTS}.log"; }
      . "$LIB"
      read -r _ < "$FIFO" || true
      if acquire_lock; then
        [ "$(cat "$LOCK/pid")" = "$$" ] && echo win >> "$RESULTS" || echo win-without-own-pid >> "$RESULTS"
        sleep 3
      else
        echo lose >> "$RESULTS"
      fi' &
  done
  sleep 1; : > "$fifo"
  wait
}

count_of() { grep -cx "$1" "$TMP/results.txt" || true; }

# 空の状態から同時に取りに来ても勝者は1本だけ
# Simultaneous acquisition from an empty state yields exactly one winner
race
[ "$(count_of win)" = 1 ] || { echo "NG: 同時取得の勝者が1本でない: $(sort "$TMP/results.txt" | uniq -c | tr '\n' ' ')"; exit 1; }
[ "$(count_of win-without-own-pid)" = 0 ] || { echo "NG: 自分の pid の無いロックを掴んだ"; exit 1; }
rm -rf "$LOCK"

# 死んだ pid の残骸ロックを同時に奪取しても勝者は1本だけ（奪取の並走で後続が先行者の生きたロックを消さない）
# Simultaneously reclaiming a dead owner's lock also yields exactly one winner (a later reclaimer never evicts the first one's live lock)
sh -c 'exit 0' & DEAD_PID=$!
wait "$DEAD_PID" 2>/dev/null || true
mkdir -p "$LOCK"; echo "$DEAD_PID" > "$LOCK/pid"
race
[ "$(count_of win)" = 1 ] || { echo "NG: 残骸ロックの同時奪取で勝者が1本でない: $(sort "$TMP/results.txt" | uniq -c | tr '\n' ' ')"; exit 1; }
grep -q '死んでいた。取得し直す' "$TMP/results.txt.log" || { echo "NG: 奪取の理由がログに出ていない"; exit 1; }
rm -rf "$LOCK"

# 奪取ロックの残骸（奪取中に死んだ実行）は退避され、次の実行で本ロックを奪取できる
# A stale reclaim lock (a run that died mid-reclaim) is evicted, and the next run reclaims the main lock
mkdir -p "$LOCK" "$LOCK.reclaim"; echo "$DEAD_PID" > "$LOCK/pid"; echo "$DEAD_PID" > "$LOCK.reclaim/pid"
log() { echo "$*" >> "$TMP/serial.log"; }
# shellcheck source=../lib/ingest-lock.sh
. "$HERE/../lib/ingest-lock.sh"
if acquire_lock; then echo "NG: 奪取ロックの残骸があるのに同じ実行で取得した"; exit 1; fi
grep -q '奪取ロックの残骸' "$TMP/serial.log" || { echo "NG: 奪取ロックの残骸の退避がログに出ていない"; exit 1; }
acquire_lock || { echo "NG: 残骸退避後の実行で奪取できない: $(cat "$TMP/serial.log")"; exit 1; }
[ "$(cat "$LOCK/pid")" = "$$" ] || { echo "NG: 奪取後の pid が自分でない"; exit 1; }

# 下書き・退避ディレクトリを残さない
# No staging or evicted directories are left behind
leftovers="$(find "$TMP" -maxdepth 1 \( -name '*.staging.*' -o -name '*.evicted.*' \))"
[ -z "$leftovers" ] || { echo "NG: 下書き/退避ディレクトリが残った: $leftovers"; exit 1; }
echo OK

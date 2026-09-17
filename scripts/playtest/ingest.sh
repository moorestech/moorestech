#!/usr/bin/env bash
# 受け口の inbox を moorestech_logs へ取り込み、成功したものだけ ack する（ADR 0061）
# 自動修正ランへの投入はしない。人が日次ダイジェストを見て enqueue-autofix.sh で投入する（裁定 2026-09-13）
# Ingests the receiver inbox into moorestech_logs and acks only what succeeded (ADR 0061)
# It never enqueues auto-fix runs; a human picks them from the daily digest via enqueue-autofix.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[ingest] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

# 既定値はスクリプト自身の位置から導出する。supervisor は HOME を封じ込め用に
# 差し替える（~/hermes-agent/data/home）ため、$HOME 基準の既定値は本番で解決しない
# Defaults derive from the script's own location: supervisor swaps HOME for a
# containment dir, so a $HOME-based default would resolve nowhere in production
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# shellcheck disable=SC1090
if [ -f "$ENV_FILE" ]; then
  . "$ENV_FILE"
else
  log "env file が無い（${ENV_FILE}）。PLAYTEST_ADMIN_KEY 等は環境変数頼みになる"
fi

# lib は env.sh 読込の後に source する。先に source すると PLAYTEST_RECEIVER_BASE 等の
# ${VAR:-既定値} 展開が env.sh の export より先に確定してしまい、上書きが効かない
# lib is sourced after env.sh: sourcing it first would resolve its ${VAR:-default}
# expansions before env.sh's exports land, so the override would never take effect
# shellcheck source=lib/receiver-api.sh
. "$HERE/lib/receiver-api.sh"

LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"
PLAYTEST_DIR="$LOGS/harness/playtest"
GIT_CMD="${GIT_CMD:-git}"
GIT_PUSH="${GIT_PUSH:-1}"
MAX_ITEMS="${PLAYTEST_INGEST_MAX_ITEMS:-50}"
MAX_PAGES="${PLAYTEST_INGEST_MAX_PAGES:-20}"
LOCK="${TMPDIR:-/tmp}/moorestech-playtest-ingest.lock"

# R2 側の kind（report|progress）と logs 側のディレクトリ名を明示対応させる
# Maps the receiver kind (report|progress) onto the logs directory name explicitly
dest_subdir() {
  case "$1" in
    report) echo "reports" ;;
    progress) echo "progress" ;;
    *) return 1 ;;
  esac
}

ingest_one() {
  local kind="$1" steam_id="$2" id="$3" ready_at="$4"
  local sub; sub="$(dest_subdir "$kind")" || { log "ERROR: 未知の kind=${kind} id=${id}（ack しない）"; return 1; }
  local dest="$PLAYTEST_DIR/$sub/$steam_id/$id"
  local partial="$PLAYTEST_DIR/$sub/$steam_id/$id.partial"

  # 既に置かれている＝前回 ack だけ失敗した箱。落とし直さず ack だけやり直す
  # An existing dest means only the ack failed last time: never re-download, just re-ack
  if [ ! -d "$dest" ]; then
    rm -rf "$partial" || { log "ERROR: 前回残骸の削除に失敗 $partial（ack しない）"; return 1; }
    mkdir -p "$partial" || { log "ERROR: mkdir 失敗 $partial（ack しない）"; return 1; }
    receiver_get_object "$kind" "$steam_id" "$id" READY "$partial/READY" \
      || { log "ERROR: READY 取得失敗 $kind/$steam_id/$id"; rm -rf "$partial"; return 1; }
    local files py_rc=0
    files="$(python3 -c '
import json,sys
d=json.load(open(sys.argv[1]))
fs=d.get("files") or []
if not fs: sys.exit(3)
for p in fs:
    if p.startswith("/") or ".." in p.split("/"): sys.exit(4)
    print(p)
' "$partial/READY")" || py_rc=$?
    if [ "$py_rc" -ne 0 ]; then
      case "$py_rc" in
        3) log "ERROR: READY 本文に files[] が無い: $kind/$steam_id/${id}（受け口側の修正が要る。ack しない）" ;;
        4) log "ERROR: READY 本文の files[] に不正なパスを含む: $kind/$steam_id/${id}（ack しない）" ;;
        *) log "ERROR: READY 本文の解析に失敗（exit ${py_rc}）: $kind/$steam_id/${id}（ack しない）" ;;
      esac
      rm -rf "$partial"; return 1
    fi
    local rel
    while IFS= read -r rel; do
      [ -n "$rel" ] || continue
      mkdir -p "$partial/$(dirname "$rel")" \
        || { log "ERROR: mkdir 失敗 $partial/$(dirname "$rel")（ack しない）"; rm -rf "$partial"; return 1; }
      receiver_get_object "$kind" "$steam_id" "$id" "$rel" "$partial/$rel" \
        || { log "ERROR: 取得失敗 $id/$rel"; rm -rf "$partial"; return 1; }
    done <<< "$files"
    python3 -c '
import json,sys
kind, steam_id, idv, ready_at, ingested_at, out = sys.argv[1:7]
with open(out, "w") as f:
    json.dump({"kind": kind, "steamId": steam_id, "id": idv, "readyAt": ready_at, "ingestedAt": ingested_at},
              f, separators=(",", ":"))
' "$kind" "$steam_id" "$id" "$ready_at" "$(now_utc)" "$partial/ingest.json" \
      || { log "ERROR: ingest.json 書き込み失敗 $kind/$steam_id/$id（ack しない）"; rm -rf "$partial"; return 1; }
    mkdir -p "$(dirname "$dest")" || { log "ERROR: mkdir 失敗 $dest（ack しない）"; rm -rf "$partial"; return 1; }
    mv "$partial" "$dest" || { log "ERROR: mv 失敗 $partial -> $dest（ack しない）"; rm -rf "$partial"; return 1; }
    log "ingested: $kind/$steam_id/$id"
  else
    log "取り込み済み。ack のみやり直す: $kind/$steam_id/$id"
  fi

  receiver_ack "$kind" "$steam_id" "$id" || { log "ERROR: ack 失敗 ${id}（次回に持ち越し）"; return 1; }
}

commit_logs() {
  # 記録の commit と push は別々に判定する（scripts/bugreport/inbox-poller.sh 前例）。
  # push だけ失敗を潰すと「届いていない」が誰にも見えなくなる
  # Judge commit and push separately (precedent: inbox-poller.sh); swallowing a failed
  # push alone would hide that nothing reached the remote
  [ -d "$LOGS/.git" ] || { log "ERROR: logs repo が無い: ${LOGS}（commit しない）"; return 0; }
  (
    cd "$LOGS"
    $GIT_CMD add -A harness/playtest || exit 1
    $GIT_CMD diff --cached --quiet -- harness/playtest && exit 0
    # 同じ index を触る他ジョブ（bug-report poller 等）が commit を挟んでも、この commit は
    # harness/playtest だけを含める（pathspec 限定。取り込み記録が他ジョブへ混入しない）
    # Even if another job (e.g. the bug-report poller) shares this index and commits in between,
    # this commit stays scoped to harness/playtest via pathspec so it never mixes into their record
    $GIT_CMD commit -qm "playtest: ingest $(now_utc)" -- harness/playtest || exit 1
  ) || log "ERROR: logs repo の commit に失敗（次回に持ち越し）"
  [ "$GIT_PUSH" = 1 ] || return 0
  (
    cd "$LOGS"
    [ "$($GIT_CMD rev-list --count '@{u}..HEAD' 2>/dev/null || echo 0)" != 0 ] || exit 0
    $GIT_CMD push -q || exit 1
  ) || log "ERROR: logs repo の push に失敗（次回に持ち越し）"
}

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

acquire_lock || exit 0
WORK="$(mktemp -d)"
trap 'rm -rf "$LOCK" "$WORK"' EXIT

# logs repo が無ければ受け口に触る前に止める。位置から導出した既定パスは
# 本体clone以外（タスクworktree等）で解決しないため、ここで fail-closed にする
# Stop before touching the receiver if the logs repo is absent: a location-derived
# default resolves to nothing outside the main clone (task worktrees etc.), so fail closed here
[ -d "${LOGS}/.git" ] || { log "ERROR: logs repo が無い（${LOGS}）。取り込み・ack をしない"; exit 1; }

ITEMS="$WORK/items.tsv"; : > "$ITEMS"
cursor=""; page=0
while [ "$page" -lt "$MAX_PAGES" ]; do
  page=$((page + 1))
  receiver_inbox_page "$cursor" "$WORK/page.json" \
    || { log "ERROR: /v1/inbox の取得に失敗（page=${page}）。今回は何もしない"; exit 0; }
  python3 -c '
import json,sys
d=json.load(open(sys.argv[1]))
for it in d.get("items") or []:
    print("\t".join([it.get("kind",""),it.get("steamId",""),it.get("id",""),it.get("readyAt","")]))
' "$WORK/page.json" >> "$ITEMS"
  cursor="$(python3 -c 'import json,sys;print(json.load(open(sys.argv[1])).get("cursor") or "")' "$WORK/page.json")"
  [ -n "$cursor" ] || break
done

# MAX_ITEMS は成功件数で数える。試行件数で数えると、恒久失敗する箱（files[]欠落等）が
# 上限を占有し続け、後続の正常な箱に永久に到達できなくなる
# MAX_ITEMS counts successes, not attempts: counting attempts would let permanently-failing
# boxes (e.g. missing files[]) occupy the cap forever and starve later, healthy boxes
success=0; failed=0
while IFS=$'\t' read -r kind steam_id id ready_at; do
  [ -n "$kind" ] && [ -n "$id" ] || continue
  if [ "$success" -ge "$MAX_ITEMS" ]; then log "成功 $MAX_ITEMS 件に達した。残りは次回"; break; fi
  if ingest_one "$kind" "$steam_id" "$id" "$ready_at"; then
    success=$((success + 1))
  else
    failed=$((failed + 1))
  fi
done < "$ITEMS"

commit_logs
log "done: 取得 $(wc -l < "$ITEMS" | tr -d ' ') 件を走査 / 成功 ${success} / 失敗 ${failed}"

#!/usr/bin/env bash
# 受け口の inbox を moorestech_logs へ取り込み、成功したものだけ ack する（ADR 0061）
# 自動修正ランへの投入はしない。人が日次ダイジェストを見て enqueue-autofix.sh で投入する（裁定 2026-09-13）
# Ingests the receiver inbox into moorestech_logs and acks only what succeeded (ADR 0061)
# It never enqueues auto-fix runs; a human picks them from the daily digest via enqueue-autofix.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/receiver-api.sh
. "$HERE/lib/receiver-api.sh"

ENV_FILE="${PLAYTEST_ENV_FILE:-$HOME/hermes-agent/data/services/playtest/env.sh}"
# shellcheck disable=SC1090
[ -f "$ENV_FILE" ] && . "$ENV_FILE"

LOGS="${MOORESTECH_LOGS:-$HOME/hermes-agent/data/repos/moorestech_logs}"
PLAYTEST_DIR="$LOGS/harness/playtest"
GIT_CMD="${GIT_CMD:-git}"
GIT_PUSH="${GIT_PUSH:-1}"
MAX_ITEMS="${PLAYTEST_INGEST_MAX_ITEMS:-50}"
MAX_PAGES="${PLAYTEST_INGEST_MAX_PAGES:-20}"
LOCK="${TMPDIR:-/tmp}/moorestech-playtest-ingest.lock"

log() { echo "[ingest] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

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
    rm -rf "$partial"; mkdir -p "$partial"
    receiver_get_object "$kind" "$steam_id" "$id" READY "$partial/READY" \
      || { log "ERROR: READY 取得失敗 $kind/$steam_id/$id"; rm -rf "$partial"; return 1; }
    local files
    files="$(python3 -c '
import json,sys
d=json.load(open(sys.argv[1]))
fs=d.get("files") or []
if not fs: sys.exit(3)
for p in fs:
    if p.startswith("/") or ".." in p.split("/"): sys.exit(4)
    print(p)
' "$partial/READY")" || {
      log "ERROR: READY 本文の files[] が無いか不正なパスを含む: $kind/$steam_id/${id}（受け口側の修正が要る。ack しない）"
      rm -rf "$partial"; return 1
    }
    local rel
    while IFS= read -r rel; do
      [ -n "$rel" ] || continue
      mkdir -p "$partial/$(dirname "$rel")"
      receiver_get_object "$kind" "$steam_id" "$id" "$rel" "$partial/$rel" \
        || { log "ERROR: 取得失敗 $id/$rel"; rm -rf "$partial"; return 1; }
    done <<< "$files"
    printf '{"kind":"%s","steamId":"%s","id":"%s","readyAt":"%s","ingestedAt":"%s"}\n' \
      "$kind" "$steam_id" "$id" "$ready_at" "$(now_utc)" > "$partial/ingest.json"
    mkdir -p "$(dirname "$dest")"; mv "$partial" "$dest"
    log "ingested: $kind/$steam_id/$id"
  else
    log "取り込み済み。ack のみやり直す: $kind/$steam_id/$id"
  fi

  receiver_ack "$kind" "$steam_id" "$id" || { log "ERROR: ack 失敗 ${id}（次回に持ち越し）"; return 1; }
}

commit_logs() {
  [ -d "$LOGS/.git" ] || { log "logs repo が無い: $LOGS"; return 0; }
  ( cd "$LOGS"
    $GIT_CMD add -A harness/playtest || exit 1
    if $GIT_CMD diff --cached --quiet -- harness/playtest; then exit 0; fi
    $GIT_CMD commit -qm "playtest: ingest $(now_utc)" || exit 1
    [ "$GIT_PUSH" = 1 ] || exit 0
    $GIT_CMD push -q || exit 1
  ) || log "ERROR: logs repo の commit/push に失敗（次回に持ち越し）"
}

mkdir "$LOCK" 2>/dev/null || { log "別の取り込みが進行中（${LOCK}）"; exit 0; }
WORK="$(mktemp -d)"
trap 'rmdir "$LOCK"; rm -rf "$WORK"' EXIT

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

count=0
while IFS=$'\t' read -r kind steam_id id ready_at; do
  [ -n "$kind" ] && [ -n "$id" ] || continue
  count=$((count + 1))
  if [ "$count" -gt "$MAX_ITEMS" ]; then log "上限 $MAX_ITEMS 件に達した。残りは次回"; break; fi
  ingest_one "$kind" "$steam_id" "$id" "$ready_at" || true
done < "$ITEMS"

commit_logs
log "done: 取得 $(wc -l < "$ITEMS" | tr -d ' ') 件を走査"

#!/usr/bin/env bash
# 片付いてから 7 日たった報告の生データを R2 から消す（settled.py が対象を選ぶ）。既定は一覧を出すだけで消さない
# 使い方: prune.sh [--execute]。stdout には消す対象（既定）・消した物・消せなかった物だけを出し、対象が無い回は無言
# Deletes R2 raw data of reports settled 7+ days ago (settled.py picks them); by default it only lists and deletes nothing
# Usage: prune.sh [--execute]. stdout carries only the targets (default), deletions and failures; a run with no targets is silent
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[r2-prune] $*" >&2; }

REPO="${MOORESTECH_REPO:-$(cd "$HERE/../../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# shellcheck disable=SC1090
if [ -f "$ENV_FILE" ]; then . "$ENV_FILE"; else log "env file が無い（${ENV_FILE}）。PLAYTEST_ADMIN_KEY は環境変数頼みになる"; fi
# shellcheck source=../lib/receiver-api.sh
. "$HERE/../lib/receiver-api.sh"
LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"

execute=0
case "${1:-}" in
  "") ;;
  --execute) execute=1 ;;
  *) log "使い方: prune.sh [--execute]"; exit 1 ;;
esac
[ -d "$LOGS" ] || { log "ERROR: logs repo が無い（${LOGS}）。何も消さない"; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
plan_rc=0
python3 -I "$HERE/settled.py" due "$LOGS" > "$WORK/due.txt" || plan_rc=$?
# 形の不正な記録があっても、読めた分の判定は続ける（不正な行は settled.py が stderr へ出している）
# Keep going with the readable records even when some are malformed (settled.py already reported those on stderr)
[ "$plan_rc" -le 1 ] || exit "$plan_rc"
[ -s "$WORK/due.txt" ] || exit "$plan_rc"

failed=0
while IFS=$'\x1f' read -r kind steam_id id outcome resolved_at; do
  if [ "$execute" -eq 0 ]; then
    echo "削除対象: $kind/$steam_id/${id}（$outcome・片付け $resolved_at）"
    continue
  fi
  python3 -c 'import json,sys;print(json.dumps({"reason":"settled:"+sys.argv[1]+" at "+sys.argv[2]}))' "$outcome" "$resolved_at" \
    > "$WORK/body.json"
  if receiver_purge "$kind" "$steam_id" "$id" "$WORK/body.json" "$WORK/reply.json" \
    && python3 -I "$HERE/settled.py" record "$LOGS" "$kind" "$steam_id" "$id" "$outcome"; then
    echo "R2 から消した: $kind/$steam_id/$id $(cat "$WORK/reply.json")"
  else
    echo "消せなかった: $kind/$steam_id/${id}（理由は stderr の [receiver] 行）"
    failed=$((failed + 1))
  fi
done < "$WORK/due.txt"
[ "$execute" -eq 1 ] || echo "（一覧だけ。消すには --execute）"
[ "$failed" -eq 0 ] && exit "$plan_rc" || exit 1

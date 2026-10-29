#!/usr/bin/env bash
# テスターが書き出した未送信報告の zip を、受け口経由と同じ形で moorestech_logs へ取り込む
# 使い方: import-export-zip.sh <zip>。全箱成功（取り込み済みの飛ばしを含む）で 0、1件でも失敗があれば 1
# Imports a tester-exported unsent-report zip into moorestech_logs in the same shape as the receiver path
# Usage: import-export-zip.sh <zip>. Exits 0 when every box succeeded (skips of already-imported ones included), 1 on any failure
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[import-export-zip] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

# 既定値はスクリプト自身の位置から導出する（ingest.sh と同じ理由）
# Defaults derive from the script's own location (same reason as ingest.sh)
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# shellcheck disable=SC1090
if [ -f "$ENV_FILE" ]; then
  . "$ENV_FILE"
else
  log "env file が無い（${ENV_FILE}）。STEAM_WEB_API_KEY 等は環境変数頼みになる"
fi
# shellcheck source=lib/ingest-lock.sh
. "$HERE/lib/ingest-lock.sh"
# shellcheck source=lib/steam-persona.sh
. "$HERE/lib/steam-persona.sh"
# shellcheck source=lib/logs-commit.sh
. "$HERE/lib/logs-commit.sh"

LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"
PLAYTEST_DIR="$LOGS/harness/playtest"
GIT_CMD="${GIT_CMD:-git}"
GIT_PUSH="${GIT_PUSH:-1}"
LOCK="$LOGS/.git/moorestech-playtest-ingest.lock"
IMPORT_PY="$HERE/lib/import_export_zip.py"

# 1箱を取り込む。失敗は理由を出して 1 を返す（partial は残さない）
# Imports one box; a failure logs its reason and returns 1 (never leaves a partial)
import_one() {
  local kind="$1" id="$2" exported_at="$3"
  local sub; if [ "$kind" = report ]; then sub=reports; else sub=progress; fi
  python3 -I "$HERE/lib/safe_segment.py" segment "$id" \
    || { log "ERROR: 箱の id が安全なパスセグメントでない: $kind/${id}（飛ばす）"; return 1; }
  local box_dir="$UNPACKED/$sub/$id" staged="$WORK/staged-$kind-$id" steam_id
  [ -d "$box_dir" ] || { log "ERROR: export.json にある箱が zip に無い: $sub/${id}（飛ばす）"; return 1; }
  rm -rf "$staged"
  steam_id="$(python3 -I "$IMPORT_PY" stage "$box_dir" "$kind" "$staged")" \
    || { log "ERROR: 箱を整形できない（steamId が無い等）: $sub/${id}（飛ばす）"; rm -rf "$staged"; return 1; }
  python3 -I "$HERE/lib/safe_segment.py" segment "$steam_id" \
    || { log "ERROR: steamId が安全なパスセグメントでない: $sub/${id}（飛ばす）"; rm -rf "$staged"; return 1; }
  # 受け口経路（ingest.sh）と同じ予約名検査を通す。zip は外部入力なので印・run.env の偽装を置く前に弾く
  # Run the receiver path's reserved-name check (ingest.sh): the zip is external input, so reject forged markers/run.env before placing
  local ready_rc=0
  python3 -I "$HERE/lib/safe_segment.py" ready-files "$staged/READY" > /dev/null || ready_rc=$?
  case "$ready_rc" in
    0|5) ;;
    3) log "ERROR: READY 本文に files[] が無い: $sub/$steam_id/${id}（飛ばす）"; rm -rf "$staged"; return 1 ;;
    4) log "ERROR: READY 本文の files[] に不正なパスを含む: $sub/$steam_id/${id}（飛ばす）"; rm -rf "$staged"; return 1 ;;
    6) log "ERROR: READY 本文の files[] が取り込み側の予約名（ingest.json・READY・AUTOFIX_* 等）と衝突: $sub/$steam_id/${id}（飛ばす）"; rm -rf "$staged"; return 1 ;;
    *) log "ERROR: READY 本文の解析に失敗（exit ${ready_rc}）: $sub/$steam_id/${id}（飛ばす）"; rm -rf "$staged"; return 1 ;;
  esac
  local dest="$PLAYTEST_DIR/$sub/$steam_id/$id" partial="$PLAYTEST_DIR/$sub/$steam_id/$id.partial"
  if [ -d "$dest" ]; then
    log "取り込み済み。飛ばす: $sub/$steam_id/$id"; rm -rf "$staged"; return 0
  fi
  rm -rf "$partial"
  mkdir -p "$(dirname "$partial")" && mv "$staged" "$partial" \
    || { log "ERROR: partial を置けない $partial"; rm -rf "$staged" "$partial"; return 1; }
  local persona_file="$STEAM_PERSONA_CACHE_DIR/$kind-$steam_id-$id.persona.json"
  python3 -I "$HERE/../bugreport/lib/restore-commit-transfer.py" "$partial" \
    || { log "ERROR: コミット資料の復元失敗: $sub/$steam_id/$id"; rm -rf "$partial"; return 1; }
  steam_persona_resolve "$steam_id" "$persona_file" \
    || { log "ERROR: 表示名の結果を書けない $sub/$steam_id/$id"; rm -rf "$partial"; return 1; }
  python3 -I "$HERE/lib/ingest_metadata.py" "$kind" "$steam_id" "$id" "$exported_at" "$(now_utc)" "$persona_file" "$partial/ingest.json" \
    || { log "ERROR: ingest.json 書き込み失敗 $sub/$steam_id/$id"; rm -rf "$partial"; return 1; }
  mv "$partial" "$dest" || { log "ERROR: mv 失敗 $partial -> $dest"; rm -rf "$partial"; return 1; }
  log "imported: $sub/$steam_id/$id"
}

[ "$#" -eq 1 ] && [ -f "$1" ] || { log "使い方: import-export-zip.sh <zip>（zip が無い: ${1:-未指定}）"; exit 1; }
ZIP="$1"
# logs repo が無ければ何も触らず止める（ingest.sh と同じ fail-closed）
# Touch nothing when the logs repo is absent (fail closed, like ingest.sh)
[ -d "${LOGS}/.git" ] || { log "ERROR: logs repo が無い（${LOGS}）。取り込まない"; exit 1; }

acquire_lock || { log "ERROR: 別の取り込みが進行中でロックを取れない。後でやり直す"; exit 1; }
WORK="$(mktemp -d)"
trap 'rm -rf "$LOCK" "$WORK"' EXIT
STEAM_PERSONA_CACHE_DIR="$WORK/steam-persona"; UNPACKED="$WORK/unpacked"
mkdir -p "$STEAM_PERSONA_CACHE_DIR" "$UNPACKED" || { log "ERROR: 作業ディレクトリを作れない"; exit 1; }

python3 -I "$IMPORT_PY" unpack "$ZIP" "$UNPACKED" > "$WORK/boxes.txt" || { log "ERROR: zip を展開できない: $ZIP"; exit 1; }

imported=0; failed=0
while IFS=$'\x1f' read -r kind id exported_at; do
  [ -n "${kind}${id}" ] || continue
  if import_one "$kind" "$id" "$exported_at"; then imported=$((imported + 1)); else failed=$((failed + 1)); fi
done < "$WORK/boxes.txt"

commit_logs "playtest: import export zip $(basename "$ZIP")"
log "done: 成功(取り込み済みの飛ばしを含む) ${imported} / 失敗 ${failed}"
[ "$failed" -eq 0 ]

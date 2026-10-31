#!/usr/bin/env bash
# logs repo に既に入っている生データを生データ置き場へ移す（一回きり）。取り込みと同じロックを取ってから migrate.py を呼ぶ
# 使い方: migrate.sh -n（一覧だけ）／migrate.sh（実行。commit まで行い push はしない）。詳細は migrate.py 冒頭
# Moves raw data already in the logs repo into the raw store (one-off); takes the ingest lock, then runs migrate.py
# Usage: migrate.sh -n (list only) / migrate.sh (run; commits but never pushes). Details at the top of migrate.py
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[raw-migrate] $*" >&2; }
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../../.." && pwd)}"
export MOORESTECH_LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"
export MOORESTECH_PLAYTEST_RAW="${MOORESTECH_PLAYTEST_RAW:-$REPO/../moorestech_playtest_raw}"
[ -d "$MOORESTECH_LOGS/.git" ] || { log "ERROR: logs repo が無い（${MOORESTECH_LOGS}）"; exit 1; }
# 取り込みが同じ harness/playtest を commit している最中に git rm を挟まない
# Never git rm while ingest is committing the same harness/playtest
LOCK="$MOORESTECH_LOGS/.git/moorestech-playtest-ingest.lock"
# shellcheck source=../lib/ingest-lock.sh
. "$HERE/../lib/ingest-lock.sh"
acquire_lock || { log "ERROR: 取り込みが進行中でロックを取れない。後でやり直す"; exit 1; }
trap 'rm -rf "$LOCK"' EXIT
python3 -I "$HERE/migrate.py" "$@"

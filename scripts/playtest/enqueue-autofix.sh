#!/usr/bin/env bash
# 取り込み済みのプレイ報告を1件、自動修正ランの inbox へ投入する（人が日次ダイジェストを見て叩く）
# Enqueues one ingested play report into the auto-fix inbox; a human runs this after reading the daily digest
# ADR 0061: テスター報告の自動投入はしない。投入の判断は人が持つ
# ADR 0061: tester reports are never auto-enqueued; the decision to enqueue belongs to a human
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"

FORCE=0
if [ "${1:-}" = "--force" ]; then FORCE=1; shift; fi
STEAM_ID="${1:-}"; ID="${2:-}"
if [ -z "$STEAM_ID" ] || [ -z "$ID" ]; then
  echo "usage: enqueue-autofix.sh [--force] <steamId> <id>" >&2
  exit 1
fi

# 既定値はスクリプト自身の位置から導出する。supervisor は HOME を封じ込め用に
# 差し替える（~/hermes-agent/data/home）ため、$HOME 基準の既定値は本番で解決しない
# Defaults derive from the script's own location: supervisor swaps HOME for a
# containment dir, so a $HOME-based default would resolve nowhere in production
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"
BOX="${LOGS}/harness/playtest/reports/${STEAM_ID}/${ID}"
BUG_INBOX="${LOGS}/harness/bug-report/inbox"
log() { echo "[enqueue] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

[ -d "${BOX}" ] || { log "ERROR: 箱が無い: ${BOX}"; exit 1; }
[ -f "${BOX}/manifest.json" ] || { log "ERROR: manifest.json が無い: ${BOX}"; exit 1; }

# 二重投入は拒否する。plan C の poller は inbox から箱を持ち去るので、inbox の有無では判定できない
# Refuse duplicates: the plan C poller moves boxes out of the inbox, so the inbox itself is not the record
if [ -f "${BOX}/AUTOFIX_QUEUED" ]; then
  log "投入済み: ${ID}（$(cat "${BOX}/AUTOFIX_QUEUED")）。やり直すならマーカーを消してから"
  exit 2
fi

MANIFEST_KIND="$(python3 -c '
import json,sys
try:
    print(json.load(open(sys.argv[1])).get("kind",""))
except Exception:
    print("")
' "${BOX}/manifest.json")"
[ -n "${MANIFEST_KIND}" ] || { log "ERROR: manifest.json の kind を読めない: ${ID}"; exit 1; }
if [ "${MANIFEST_KIND}" != bug ] && [ "${FORCE}" != 1 ]; then
  log "kind=${MANIFEST_KIND} は自動修正ランの対象外。投入するなら --force: ${ID}"
  exit 3
fi
[ "${MANIFEST_KIND}" != bug ] && log "--force で kind=${MANIFEST_KIND} を投入する: ${ID}"

# .partial へ組んでから mv で公開する。poller が途中の箱を掴まないため（plan C と同じ作法）
# Build in .partial and publish with mv so the poller never sees a half box (same idiom as plan C)
PARTIAL="${BUG_INBOX}/${ID}.partial"
mkdir -p "${BUG_INBOX}"; rm -rf "${PARTIAL}"
cp -R "${BOX}" "${PARTIAL}"
rm -f "${PARTIAL}/AUTOFIX_QUEUED"
# --force の印は inbox 側へ持たせる。poller の種別ガードはこの印がある箱だけ通す（plan C 改訂メモ2）
# The --force marker travels with the inbox copy; the poller's kind guard lets only marked boxes through
[ "${FORCE}" = 1 ] && printf 'forced kind=%s at %s\n' "${MANIFEST_KIND}" "$(now_utc)" > "${PARTIAL}/AUTOFIX_FORCED"
[ -f "${PARTIAL}/READY" ] || now_utc > "${PARTIAL}/READY"
rm -rf "${BUG_INBOX}/${ID}"; mv "${PARTIAL}" "${BUG_INBOX}/${ID}"
printf 'queued at %s\n' "$(now_utc)" > "${BOX}/AUTOFIX_QUEUED"
log "投入した: ${ID}（poller が最大60秒で拾う）"

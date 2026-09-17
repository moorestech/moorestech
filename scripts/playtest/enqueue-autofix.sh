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

# manifest は壊れうる外部入力。読めない理由をログして空を返す（無音で握り潰さない。ship-outbox.sh前例）
# manifest is fallible external input: log why it could not be read and return empty (precedent: ship-outbox.sh)
KIND_ERR="$(mktemp)"
MANIFEST_KIND="$(python3 -c '
import json,sys
kind = json.load(open(sys.argv[1])).get("kind", "")
if not isinstance(kind, str):
    sys.stderr.write(f"kind が文字列でない: {kind!r}")
    kind = ""
print(kind)
' "${BOX}/manifest.json" 2>"$KIND_ERR")" || MANIFEST_KIND=""
if [ -s "$KIND_ERR" ]; then
  log "manifest.json の kind を読めない（$(tr '\n' ' ' < "$KIND_ERR")）: ${ID}"
fi
rm -f "$KIND_ERR"
[ -n "${MANIFEST_KIND}" ] || { log "ERROR: manifest.json の kind を読めない: ${ID}"; exit 1; }
if [ "${MANIFEST_KIND}" != bug ] && [ "${FORCE}" != 1 ]; then
  log "kind=${MANIFEST_KIND} は自動修正ランの対象外。投入するなら --force: ${ID}"
  exit 3
fi
[ "${MANIFEST_KIND}" != bug ] && log "--force で kind=${MANIFEST_KIND} を投入する: ${ID}"

# .partial へ組んでから mv で公開する。poller が途中の箱を掴まないため（plan C と同じ作法）。
# 既に公開済み/組立中の箱があれば無言で消さず据え置く（前例 ship-outbox.sh:96-103。並行実行や
# 中断後の再実行で poller 未回収の箱を壊さないため）
# Build in .partial and publish with mv so the poller never sees a half box (same idiom as plan C).
# A pre-existing published/partial box is left alone rather than silently deleted (precedent:
# ship-outbox.sh:96-103), so a concurrent run or a resume-after-interrupt never corrupts an unclaimed box
PARTIAL="${BUG_INBOX}/${ID}.partial"
mkdir -p "${BUG_INBOX}"
if [ -e "${PARTIAL}" ] || [ -e "${BUG_INBOX}/${ID}" ]; then
  log "ERROR: inbox に既存の箱がある（前回の投入が未完了か同時実行の疑い）。壊さないため投入しない: ${BUG_INBOX}/${ID}"
  exit 4
fi
cp -R "${BOX}" "${PARTIAL}"
rm -f "${PARTIAL}/AUTOFIX_QUEUED"
# --force の印は inbox 側へ持たせる。poller の種別ガードはこの印がある箱だけ通す（plan C 改訂メモ2）
# The --force marker travels with the inbox copy; the poller's kind guard lets only marked boxes through
[ "${FORCE}" = 1 ] && printf 'forced kind=%s at %s\n' "${MANIFEST_KIND}" "$(now_utc)" > "${PARTIAL}/AUTOFIX_FORCED"
[ -f "${PARTIAL}/READY" ] || now_utc > "${PARTIAL}/READY"
mv "${PARTIAL}" "${BUG_INBOX}/${ID}"
printf 'queued at %s\n' "$(now_utc)" > "${BOX}/AUTOFIX_QUEUED"
log "投入した: ${ID}（poller が最大60秒で拾う）"

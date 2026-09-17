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
log() { echo "[enqueue] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

# 引数はダイジェストから貼られるテスター由来の値。rm/mv/cp のパスへ連結する前に単一の安全セグメントか検証する
# The arguments are tester-supplied values pasted from the digest; verify each is a single safe segment before path joins
python3 "$HERE/lib/safe_segment.py" segment "$STEAM_ID" && python3 "$HERE/lib/safe_segment.py" segment "$ID" \
  || { log "ERROR: steamId/id が安全なパスセグメントでない: ${STEAM_ID} ${ID}"; exit 1; }
BOX="${LOGS}/harness/playtest/reports/${STEAM_ID}/${ID}"
BUG_INBOX="${LOGS}/harness/bug-report/inbox"
BUG_RUNS="${LOGS}/harness/bug-report/runs"

[ -d "${BOX}" ] || { log "ERROR: 箱が無い: ${BOX}"; exit 1; }
[ -f "${BOX}/manifest.json" ] || { log "ERROR: manifest.json が無い: ${BOX}"; exit 1; }
# READY は取り込み時に受け口から写したもの。無い箱は取り込みが完結していないので捏造せず拒否する
# READY is copied from the receiver at ingest; a box without it was never fully ingested, so refuse rather than fabricate one
[ -f "${BOX}/READY" ] || { log "ERROR: READY が無い（取り込みが完結していない箱）: ${BOX}"; exit 1; }

# 二重投入は拒否する。plan C の poller は inbox から箱を持ち去るので、inbox の有無では判定できない
# Refuse duplicates: the plan C poller moves boxes out of the inbox, so the inbox itself is not the record
if [ -f "${BOX}/AUTOFIX_QUEUED" ]; then
  log "投入済み: ${ID}（$(cat "${BOX}/AUTOFIX_QUEUED")）。やり直すならマーカーを消してから"
  exit 2
fi
# poller は runs/<id> が既にある箱を .duplicate へ隔離してランを起こさない。投入しても走らないので先に止める
# The poller quarantines a box whose runs/<id> already exists and never runs it, so refuse up front
if [ -e "${BUG_RUNS}/${ID}" ]; then
  log "ERROR: 同名のラン記録が既にある（poller が隔離してランを起こさない）。再実行するなら旧ランを改名・退避してから: ${BUG_RUNS}/${ID}"
  exit 5
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
mv "${PARTIAL}" "${BUG_INBOX}/${ID}"
printf 'queued at %s\n' "$(now_utc)" > "${BOX}/AUTOFIX_QUEUED"
log "投入した: ${ID}（poller が最大60秒で拾う）"

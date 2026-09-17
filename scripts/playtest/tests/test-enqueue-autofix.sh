#!/usr/bin/env bash
# 手動投入コマンドの4経路（投入・二重投入拒否・種別拒否・--force）を検証する
# Verifies the four paths of the manual enqueue command: enqueue, duplicate, kind guard, --force
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
LOGS="$TMP/logs"
BUG="$LOGS/harness/playtest/reports/7656001/20260913_100000_bug1"
FB="$LOGS/harness/playtest/reports/7656002/20260913_110000_fb1"
mkdir -p "$BUG" "$FB" "$LOGS/harness/bug-report/inbox"
echo '{"kind":"bug","description":"ベルトが止まる"}' > "$BUG/manifest.json"
echo 'READY-summary' > "$BUG/READY"
echo '{"kind":"feedback","description":"序盤が長い"}' > "$FB/manifest.json"
echo 'READY-summary' > "$FB/READY"

run() { MOORESTECH_LOGS="$LOGS" bash "$HERE/../enqueue-autofix.sh" "$@"; }
I="$LOGS/harness/bug-report/inbox"

run 7656001 20260913_100000_bug1
[ -f "$I/20260913_100000_bug1/READY" ] || { echo "NG: inbox に入っていない"; exit 1; }
[ -f "$I/20260913_100000_bug1/manifest.json" ] || { echo "NG: 中身が入っていない"; exit 1; }
[ ! -e "$I/20260913_100000_bug1/AUTOFIX_QUEUED" ] || { echo "NG: マーカーを inbox 側へ持ち込んだ"; exit 1; }
grep -q '^queued at ' "$BUG/AUTOFIX_QUEUED" || { echo "NG: マーカーが無い"; exit 1; }

set +e; run 7656001 20260913_100000_bug1; code=$?; set -e
[ "$code" = 2 ] || { echo "NG: 二重投入が拒否されない（exit=$code）"; exit 1; }

set +e; run 7656002 20260913_110000_fb1; code=$?; set -e
[ "$code" = 3 ] || { echo "NG: 感想が拒否されない（exit=$code）"; exit 1; }
[ ! -e "$I/20260913_110000_fb1" ] || { echo "NG: 感想が投入された"; exit 1; }

run --force 7656002 20260913_110000_fb1
[ -f "$I/20260913_110000_fb1/READY" ] || { echo "NG: --force で投入されない"; exit 1; }
grep -q '^forced kind=feedback' "$I/20260913_110000_fb1/AUTOFIX_FORCED" || { echo "NG: AUTOFIX_FORCED が無い"; exit 1; }
[ ! -e "$I/20260913_100000_bug1/AUTOFIX_FORCED" ] || { echo "NG: --force 無しで FORCED が付いた"; exit 1; }

set +e; run 7656001 存在しないID; code=$?; set -e
[ "$code" = 1 ] || { echo "NG: 箱が無いのに失敗しない（exit=$code）"; exit 1; }
echo OK

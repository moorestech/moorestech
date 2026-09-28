#!/usr/bin/env bash
# 手動投入コマンドの経路（投入・二重投入拒否・種別拒否・--force・不正引数・READY 欠落・既存ラン）を検証する
# Verifies the manual enqueue paths: enqueue, duplicate, kind guard, --force, unsafe args, missing READY and an existing run
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

# パスへ連結される引数は単一の安全セグメントでなければ拒否する（.. で箱の外を指せない）
# Arguments joined into paths must be single safe segments (no .. escaping the box tree)
for bad in ".." "a/b" ""; do
  set +e; run 7656001 "$bad" 2>/dev/null; code=$?; set -e
  [ "$code" = 1 ] || { echo "NG: 不正な id '$bad' が拒否されない（exit=$code）"; exit 1; }
done
set +e; run .. 20260913_100000_bug1 2>/dev/null; code=$?; set -e
[ "$code" = 1 ] || { echo "NG: 不正な steamId が拒否されない（exit=$code）"; exit 1; }

# 元箱に READY が無ければ捏造せず拒否する
# A source box without READY is refused rather than given a fabricated one
NOREADY="$LOGS/harness/playtest/reports/7656003/20260913_120000_noready"
mkdir -p "$NOREADY"; echo '{"kind":"bug"}' > "$NOREADY/manifest.json"
set +e; run 7656003 20260913_120000_noready 2>/dev/null; code=$?; set -e
[ "$code" = 1 ] && [ ! -e "$I/20260913_120000_noready" ] || { echo "NG: READY 無しの箱が投入された（exit=$code）"; exit 1; }

# 同名のラン記録が既にあれば poller が走らせないので投入しない
# An existing run record of the same name is refused, since the poller would never run it
RUNBOX="$LOGS/harness/playtest/reports/7656004/20260913_130000_rerun"
mkdir -p "$RUNBOX" "$LOGS/harness/bug-report/runs/20260913_130000_rerun"
echo '{"kind":"bug"}' > "$RUNBOX/manifest.json"; echo 'READY-summary' > "$RUNBOX/READY"
set +e; run 7656004 20260913_130000_rerun 2>"$TMP/rerun.log"; code=$?; set -e
[ "$code" = 5 ] && [ ! -e "$I/20260913_130000_rerun" ] || { echo "NG: 既存ランのある id が投入された（exit=$code）"; exit 1; }
grep -q '同名のラン記録が既にある' "$TMP/rerun.log" || { echo "NG: 既存ランの拒否理由が出ていない"; exit 1; }

# 遠隔実行の印は通常投入を拒否し、明示的な強制だけを通す
# Remote-exec marks refuse ordinary enqueue and allow only explicit force
REX="$LOGS/harness/playtest/reports/7656005/rex"
mkdir -p "$REX"
echo '{"kind":"bug","remoteExec":{"ledgerFiles":[]}}' > "$REX/manifest.json"
echo ready > "$REX/READY"
set +e; run 7656005 rex 2>"$TMP/rex.log"; code=$?; set -e
[ "$code" = 6 ] && [ ! -e "$I/rex" ] || { echo "NG: 遠隔実行の箱が拒否されない（exit=$code）"; exit 1; }
grep -q '遠隔実行が有効だったセッション' "$TMP/rex.log" || { echo "NG: 遠隔実行の拒否理由が無い"; exit 1; }
run --force 7656005 rex 2>"$TMP/rex-force.log"
[ -f "$I/rex/AUTOFIX_FORCED" ] || { echo "NG: 強制投入印が無い"; exit 1; }
grep -q 'remoteExec=1' "$I/rex/AUTOFIX_FORCED" || { echo "NG: 遠隔実行の強制理由が無い"; exit 1; }
grep -q -- '--force で遠隔実行ありの箱を投入する' "$TMP/rex-force.log"
mkdir -p "$LOGS/harness/playtest/reports/7656005/normal"
echo '{"kind":"bug","remoteExec":null}' > "$LOGS/harness/playtest/reports/7656005/normal/manifest.json"
echo ready > "$LOGS/harness/playtest/reports/7656005/normal/READY"
run 7656005 normal
mkdir -p "$LOGS/harness/playtest/reports/7656005/empty-mark"
echo '{"kind":"bug","remoteExec":{}}' > "$LOGS/harness/playtest/reports/7656005/empty-mark/manifest.json"
echo ready > "$LOGS/harness/playtest/reports/7656005/empty-mark/READY"
set +e; run 7656005 empty-mark 2>"$TMP/empty.log"; code=$?; set -e
[ "$code" = 6 ] || { echo "NG: 空の印を通常扱いした（exit=$code）"; exit 1; }

# 型が壊れた印は強制指定でも黙って通常扱いにしない
# Malformed marks must never silently become ordinary reports, even with force
BAD="$LOGS/harness/playtest/reports/7656005/bad"
mkdir -p "$BAD"
echo ready > "$BAD/READY"
echo '{"kind":"bug","remoteExec":{"ledgerFiles":"bad"}}' > "$BAD/manifest.json"
set +e; run --force 7656005 bad 2>"$TMP/bad.log"; code=$?; set -e
[ "$code" = 1 ] && [ ! -e "$I/bad" ] || { echo "NG: 壊れた印を投入した（exit=$code）"; exit 1; }
grep -q 'remoteExec を読めない' "$TMP/bad.log" || { echo "NG: 壊れた印の理由が無い"; exit 1; }
echo OK

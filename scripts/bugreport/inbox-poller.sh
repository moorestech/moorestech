#!/usr/bin/env bash
# inbox から1件取り出し、隔離 worktree を用意して自動修正ランを起動する。単一飛行（サーバーポート固定のため）
# Takes one box from the inbox, prepares an isolated worktree and launches the auto-fix run; single flight (fixed server port)
set -euo pipefail
LOGS="${MOORESTECH_LOGS:-$HOME/hermes-agent/data/repos/moorestech_logs}"
BASE="$LOGS/harness/bug-report"; INBOX="$BASE/inbox"; RUNS="$BASE/runs"
REPO="${MOORESTECH_REPO:-$HOME/hermes-agent/data/repos/moorestech}"
CLAUDE_CMD="${CLAUDE_CMD:-claude}"
PREPARE_CMD="${PREPARE_CMD:-$(cd "$(dirname "$0")" && pwd)/prepare-run.sh}"
GIT_PUSH="${GIT_PUSH:-1}"
LOCK="${TMPDIR:-/tmp}/moorestech-bugreport-poller.lock"
log() { echo "[poller] $*" >&2; }

mkdir "$LOCK" 2>/dev/null || { log "別のランが進行中（${LOCK}）"; exit 0; }
trap 'rmdir "$LOCK"' EXIT

# READY 付きの箱だけを対象にし、運搬中の <id>.partial は掴まない（ship-outbox の原子性と対）
# Only READY boxes qualify; in-flight <id>.partial boxes are never picked up (pairs with ship-outbox's atomicity)
shopt -s nullglob
box=""; id=""
for marker in "$INBOX"/*/READY; do
  candidate="$(dirname "$marker")"; name="$(basename "$candidate")"
  case "$name" in
    *.partial) log "運搬中のためスキップ: $name"; continue ;;
  esac
  box="$candidate"; id="$name"; break
done
[ -n "$box" ] || exit 0

# 同じ箱を二度処理しない。runs に同名があれば mv が入れ子になるため、理由を出して中断する
# Never process the same box twice; a same-named run would make mv nest it, so stop with a reason
if [ -e "$RUNS/$id" ]; then
  log "runs に同名のランが既にある。二重処理を避けて中断: $RUNS/$id"; exit 0
fi
mkdir -p "$RUNS"; mv "$box" "$RUNS/$id"; run="$RUNS/$id"
log "run start: $id"

MOORESTECH_LOGS="$LOGS" "$PREPARE_CMD" "$id" || log "prepare 失敗（続行してエージェントに判断させる）"
. "$run/run.env" 2>/dev/null || WORKTREE="$REPO"

# 非対話で起動し、終了まで待つ。上限は設けない（裁定）
# Launch non-interactively and wait; no time budget (ruling)
( cd "$WORKTREE" && BUG_REPORT_RUNDIR_BASE="$RUNS" $CLAUDE_CMD -p "【無人起動】/bug-report-auto-fix $id" --permission-mode bypassPermissions --output-format json > "$run/claude.out.json" 2> "$run/claude.err.log" ) || log "claude 異常終了（exit $?）"

if [ ! -f "$run/fix-result.json" ]; then
  log "fix-result.json が無いため failure で補完する: $id"
  printf '{"status": "failure", "summary": "claude exited without fix-result.json", "remaining": "runs/%s/claude.err.log を確認"}\n' "$id" > "$run/fix-result.json"
fi
log "run end: $id status=$(python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('status'))" "$run/fix-result.json")"

( cd "$LOGS" && git add "harness/bug-report/runs/$id" && git commit -qm "bug-report run $id" && { [ "$GIT_PUSH" = "1" ] && git push -q || true; } ) || log "logs commit/push 失敗"

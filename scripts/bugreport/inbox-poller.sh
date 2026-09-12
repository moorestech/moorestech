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
[ -d "$INBOX" ] || { log "inbox が無いので何もしない: $INBOX"; exit 0; }
box=""; id=""
for marker in "$INBOX"/*/READY; do
  candidate="$(dirname "$marker")"; name="$(basename "$candidate")"
  case "$name" in
    *.partial) log "運搬中のためスキップ: $name"; continue ;;
  esac
  box="$candidate"; id="$name"; break
done
[ -n "$box" ] || { log "READY の箱が無いので何もしない: $INBOX"; exit 0; }

# 同じ箱を二度処理しない。runs に同名があれば mv が入れ子になるため、理由を出して中断する
# Never process the same box twice; a same-named run would make mv nest it, so stop with a reason
if [ -e "$RUNS/$id" ]; then
  log "runs に同名のランが既にある。二重処理を避けて中断: $RUNS/$id"; exit 0
fi
mkdir -p "$RUNS"
mv "$box" "$RUNS/$id" || { log "箱を runs へ移せなかった。次回に再試行: $box"; exit 1; }
run="$RUNS/$id"
log "run start: $id"

MOORESTECH_LOGS="$LOGS" "$PREPARE_CMD" "$id" || log "prepare 失敗（続行してエージェントに判断させる）"
# bash の `.` は特殊組み込みで、ファイルが無いと `||` があってもスクリプトごと落ちる。必ず存在を確かめてから読む
# bash's `.` is a special builtin and aborts the script when the file is absent even with `||`; check first
WORKTREE=""
if [ -f "$run/run.env" ]; then
  . "$run/run.env" || log "run.env の読み込みに失敗した: $run/run.env"
else
  log "run.env が無い（prepare が最後まで進まなかった）: $run/run.env"
fi

# 隔離 worktree が無いままメインのワーキングツリーで走らせない（他セッションと共有されているため）
# Never fall back to the main working tree when the isolated worktree is missing; it is shared with other sessions
if [ -z "${WORKTREE:-}" ] || [ ! -d "$WORKTREE" ]; then
  log "隔離 worktree が無いため自動修正ランを起こさない（WORKTREE='${WORKTREE:-}'）: $id"
  printf '{"status": "failure", "summary": "prepare が隔離 worktree を用意できなかった", "remaining": "runs/%s/ の prepare ログを確認"}\n' "$id" > "$run/fix-result.json"
else
  # 非対話で起動し、終了まで待つ。上限は設けない（裁定）
  # Launch non-interactively and wait; no time budget (ruling)
  ( cd "$WORKTREE" && BUG_REPORT_RUNDIR_BASE="$RUNS" $CLAUDE_CMD -p "【無人起動】/bug-report-auto-fix $id" --permission-mode bypassPermissions --output-format json > "$run/claude.out.json" 2> "$run/claude.err.log" ) || log "claude 異常終了（exit $?）"

  if [ ! -f "$run/fix-result.json" ]; then
    log "fix-result.json が無いため failure で補完する: $id"
    printf '{"status": "failure", "summary": "claude exited without fix-result.json", "remaining": "runs/%s/claude.err.log を確認"}\n' "$id" > "$run/fix-result.json"
  fi
fi

# fix-result.json はエージェントが書く外部入力。壊れていても run の締めを止めない
# fix-result.json is agent-written external input; a broken one must not stop the run from closing out
status="$(python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('status'))" "$run/fix-result.json" 2>/dev/null)" \
  || { status="unreadable"; log "fix-result.json を読めない（壊れた JSON）: $run/fix-result.json"; }
log "run end: $id status=$status"

# 記録の commit と push は別々に判定する。push の失敗を 0 に潰すと「届いていない」が誰にも見えなくなる
# Judge the commit and the push separately; swallowing a failed push hides the fact that nothing reached the remote
( cd "$LOGS" && git add "harness/bug-report/runs/$id" && git commit -qm "bug-report run $id" ) \
  || log "logs commit 失敗（このランの記録は private remote へ残っていない）: $RUNS/$id"
if [ "$GIT_PUSH" = "1" ]; then
  ( cd "$LOGS" && git push -q ) \
    || log "logs push 失敗（このランの記録は手元にしか無い。手動 push が要る）: $RUNS/$id"
fi

#!/usr/bin/env bash
# inbox から1件取り出し、隔離 worktree を用意して自動修正ランを起動する。単一飛行（サーバーポート固定のため）
# Takes one box from the inbox, prepares an isolated worktree and launches the auto-fix run; single flight (fixed server port)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
# 既定値はスクリプト自身の位置から導出する。supervisor は HOME を封じ込め用に差し替えるため $HOME 基準は本番で解決しない
# Defaults derive from the script's own location; supervisor swaps HOME for containment, so $HOME-based defaults break in production
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"
BASE="$LOGS/harness/bug-report"; INBOX="$BASE/inbox"; RUNS="$BASE/runs"
# 隔離先は dot 始まりにする。READY 付きのまま置いても候補 glob に掴まれない
# The quarantine directory starts with a dot so a box kept there with its READY marker never matches the candidate glob
DUPLICATE="$INBOX/.duplicate"
WORKTREES="${MOORESTECH_WORKTREES:-$REPO/../moorestech-worktrees}"
CLAUDE_CMD="${CLAUDE_CMD:-claude}"
PREPARE_CMD="${PREPARE_CMD:-$HERE/prepare-run.sh}"
CANON_SETUP_CMD="${CANON_SETUP_CMD:-python3 $REPO/.agents/skills/pr-independent-review/scripts/canon_setup.py}"
CANON_SKILL_REL=".agents/skills/bug-report-auto-fix/SKILL.md"
GIT_PUSH="${GIT_PUSH:-1}"
LOCK="${TMPDIR:-/tmp}/moorestech-bugreport-poller.lock"
log() { echo "[poller] $*" >&2; }
# fix-result.json の finishedAt（日次ダイジェストの日付判定に使う）と同じ ISO8601 UTC 形式
# Same ISO8601 UTC form as fix-result.json finishedAt, which the daily digest dates runs by
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

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
  # 同じ箱を二度処理しない。runs に同名があると mv が入れ子になるので隔離し、後続の報告は止めずに次の候補へ進む
  # Never process the same box twice; a same-named run would make mv nest it, so quarantine it and move on to the next candidate
  if [ -e "$RUNS/$name" ]; then
    mkdir -p "$DUPLICATE"
    if [ -e "$DUPLICATE/$name" ]; then
      log "runs にも隔離先にも同名がある。触らず次の候補へ: $candidate"
    elif mv "$candidate" "$DUPLICATE/$name"; then
      log "runs に同名のランが既にあるため隔離した（人が中身を見て捨てるか改名する）: $DUPLICATE/$name"
    else
      log "重複箱を隔離できなかった。今回は飛ばして次の候補へ: $candidate"
    fi
    continue
  fi
  box="$candidate"; id="$name"; break
done
[ -n "$box" ] || { log "READY の箱が無いので何もしない: $INBOX"; exit 0; }

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

# 実行制御の正本（スキル本文・補助スクリプト・hook）は SHA 固定の canon から読む。修正対象の worktree は報告時のコミットにあり、
# そこを cwd にするとスキルも関所も報告時の版（無いことすらある）で走ってしまう
# The run-control source (skill body, helper scripts, hooks) comes from the SHA-pinned canon; the fix-target worktree sits at the report commit,
# so using it as cwd would run the skill and the gate from that old revision, where they may not exist at all
CANON=""
resolve_canon() {
  local out
  # canon_setup の SKILL.md 同一性ガードは $ORIGIN からスキル本文を読む運用のためのもので、poller は canon だけを読むため適用しない
  # canon_setup's SKILL.md identity guard exists for flows that read the skill body from $ORIGIN; the poller reads only the canon, so it does not apply
  out="$($CANON_SETUP_CMD --origin "$REPO" --parent "$WORKTREES" --allow-skew 2> "$run/canon.err.log")" \
    || { log "canon worktree を用意できなかった（canon_setup 失敗。$run/canon.err.log を確認）: $CANON_SETUP_CMD"; return 1; }
  CANON="$(printf '%s' "$out" | python3 -c "import json,sys;print(json.load(sys.stdin)['canon'])")" \
    || { log "canon_setup の出力から canon を読めなかった: $CANON_SETUP_CMD"; return 1; }
  [ -f "$CANON/$CANON_SKILL_REL" ] \
    || { log "canon に $CANON_SKILL_REL が無い（このスキルがまだ master に入っていない）: $CANON"; return 1; }
  return 0
}

# 隔離 worktree が無いままメインのワーキングツリーで走らせない（他セッションと共有されているため）
# Never fall back to the main working tree when the isolated worktree is missing; it is shared with other sessions
if [ -z "${WORKTREE:-}" ] || [ ! -d "$WORKTREE" ]; then
  log "隔離 worktree が無いため自動修正ランを起こさない（WORKTREE='${WORKTREE:-}'）: $id"
  printf '{"status": "failure", "finishedAt": "%s", "summary": "prepare が隔離 worktree を用意できなかった", "remaining": "runs/%s/ の prepare ログを確認"}\n' "$(now_utc)" "$id" > "$run/fix-result.json"
elif ! resolve_canon; then
  log "実行制御の正本（canon）が無いため自動修正ランを起こさない: $id"
  printf '{"status": "failure", "finishedAt": "%s", "summary": "SHA固定の canon worktree を用意できなかった", "remaining": "runs/%s/canon.err.log と poller ログを確認"}\n' "$(now_utc)" "$id" > "$run/fix-result.json"
else
  # 非対話で起動し、終了まで待つ。上限は設けない（裁定）。cwd は canon（読み取り専用）、コードを直す先は --add-dir の worktree
  # Launch non-interactively and wait; no time budget (ruling). cwd is the read-only canon; code is fixed in the --add-dir worktree
  ( cd "$CANON" && BUG_REPORT_RUNDIR_BASE="$RUNS" $CLAUDE_CMD -p "【無人起動】/bug-report-auto-fix $id" --add-dir "$WORKTREE" --permission-mode bypassPermissions --output-format json > "$run/claude.out.json" 2> "$run/claude.err.log" ) || log "claude 異常終了（exit $?）"

  if [ ! -f "$run/fix-result.json" ]; then
    log "fix-result.json が無いため failure で補完する: $id"
    printf '{"status": "failure", "finishedAt": "%s", "summary": "claude exited without fix-result.json", "remaining": "runs/%s/claude.err.log を確認"}\n' "$(now_utc)" "$id" > "$run/fix-result.json"
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

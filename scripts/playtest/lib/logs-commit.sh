#!/usr/bin/env bash
# logs repo の harness/playtest をコミット・push する。呼び出し側が log・LOGS・GIT_CMD・GIT_PUSH を定義してから source する
# （now_utc はメッセージを作る呼び出し側のもの）
# Commits and pushes harness/playtest in the logs repo; the caller defines log, LOGS, GIT_CMD and GIT_PUSH before sourcing
# (now_utc, used to build the message, also belongs to the caller)

commit_logs() {
  local message="$1"
  # 記録の commit と push は別々に判定する（scripts/bugreport/inbox-poller.sh 前例）。
  # push だけ失敗を潰すと「届いていない」が誰にも見えなくなる
  # Judge commit and push separately (precedent: inbox-poller.sh); swallowing a failed
  # push alone would hide that nothing reached the remote
  [ -d "$LOGS/.git" ] || { log "ERROR: logs repo が無い: ${LOGS}（commit しない）"; return 0; }
  (
    cd "$LOGS"
    $GIT_CMD add -A harness/playtest || exit 1
    $GIT_CMD diff --cached --quiet -- harness/playtest && exit 0
    # 同じ index を触る他ジョブ（bug-report poller 等）が commit を挟んでも、この commit は
    # harness/playtest だけを含める（pathspec 限定。取り込み記録が他ジョブへ混入しない）
    # Even if another job (e.g. the bug-report poller) shares this index and commits in between,
    # this commit stays scoped to harness/playtest via pathspec so it never mixes into their record
    $GIT_CMD commit -qm "$message" -- harness/playtest || exit 1
  ) || log "ERROR: logs repo の commit に失敗（次回に持ち越し）"
  [ "$GIT_PUSH" = 1 ] || return 0
  (
    cd "$LOGS"
    local ahead
    # 数えられない（上流未設定等）を 0 と読み替えると push 漏れが無音になるので理由を出す
    # Reading a failed count (e.g. no upstream) as 0 would silently skip the push, so log why
    ahead="$($GIT_CMD rev-list --count '@{u}..HEAD' 2>&1)" \
      || { log "ERROR: upstream との差を数えられず push しない: ${ahead}"; exit 0; }
    [ "$ahead" != 0 ] || exit 0
    $GIT_CMD push -q || exit 1
  ) || log "ERROR: logs repo の push に失敗（次回に持ち越し）"
}

#!/bin/bash
# user-simulator review の関所。brainstorming / writing-plans のfrontmatter hooksから呼ばれる
# （スキル発動セッション限定で有効）。track: spec/plan執筆とreview実行痕跡の追跡 / stop: 終了関所
# Gate for user-simulator review, activated only in sessions where the wired skills fired.
set -u
MODE="${1:-}"
INPUT="$(cat)"
SID=$(printf '%s' "$INPUT" | python3 -c 'import sys,json;print(json.load(sys.stdin).get("session_id",""))' 2>/dev/null)
[ -z "$SID" ] && exit 0
DIR="${TMPDIR:-/tmp}/claude-user-simulator-gate"
mkdir -p "$DIR"
STATE="$DIR/$SID"

case "$MODE" in
  track)
    FILE=$(printf '%s' "$INPUT" | python3 -c 'import sys,json;print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))' 2>/dev/null)
    # spec/planの執筆を検知したら関所を武装する / Arm the gate when a spec or plan is written
    case "$FILE" in
      */docs/superpowers/specs/*.md|*/docs/superpowers/plans/*.md) touch "$STATE.doc" ;;
    esac
    # misses.mdへの記録=review実行(またはスキップ判断の記録)の痕跡として関所を解除する
    # Any write to misses.md counts as evidence that review (or an explicit skip) was recorded
    case "$FILE" in
      */user-simulator/modes/improve/misses.md) touch "$STATE.reviewed" ;;
    esac
    exit 0 ;;
  stop)
    [ -f "$STATE.doc" ] || exit 0
    [ -f "$STATE.reviewed" ] && exit 0
    # ブロック上限は自前カウンタで管理（ハーネス側上限は実測で機能しない） / Own block counter
    COUNT=0
    [ -f "$STATE.blocks" ] && COUNT=$(cat "$STATE.blocks")
    if [ "$COUNT" -ge 2 ]; then exit 0; fi
    echo $((COUNT + 1)) > "$STATE.blocks"
    echo "このセッションでspec/planが書かれましたが user-simulator review の実行記録がありません。.claude/skills/user-simulator/modes/review/protocol.md に従いreviewを実行し、採点を modes/improve/misses.md に追記してください。ユーザーが明示的にスキップを指示した場合は、その旨をmisses.mdに1行記録すれば通過できます。" >&2
    exit 2 ;;
  *)
    exit 0 ;;
esac

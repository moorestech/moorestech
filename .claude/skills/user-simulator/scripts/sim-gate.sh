#!/bin/bash
# user-simulator review の関所。brainstorming / writing-plans のfrontmatter hooksから呼ばれる
# （スキル発動セッション限定で有効）。track: spec/plan執筆とreview実行痕跡の追跡 / stop: 終了関所 / preask: AskUserQuestion直前のpreanswer関所
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
    # 解除はdoc単位で判定する。.reviewedがセッション一発解除だと、先行docのreview後に
    # 書かれた後続doc（brainstorming spec→writing-plans plan等）の関所が素通りする（実測bug）。
    # Release is per-document: a session-global .reviewed would let a later doc slip the gate.
    [ "$STATE.reviewed" -nt "$STATE.doc" ] && exit 0
    # ブロック上限は自前カウンタで管理（ハーネス側上限は実測で機能しない） / Own block counter
    COUNT=0
    [ -f "$STATE.blocks" ] && COUNT=$(cat "$STATE.blocks")
    if [ "$COUNT" -ge 2 ]; then exit 0; fi
    echo $((COUNT + 1)) > "$STATE.blocks"
    echo "このセッションでspec/planが書かれましたが user-simulator review の実行記録がありません。.claude/skills/user-simulator/modes/review/protocol.md に従いreviewを実行し、採点を modes/improve/misses.md に追記してください。ユーザーが明示的にスキップを指示した場合は、その旨をmisses.mdに1行記録すれば通過できます。" >&2
    exit 2 ;;
  preask)
    # AskUserQuestion直前の関所（PreToolUse・matcherでAskUserQuestionに限定）。preanswer未実行なら質問をブロック。
    # 解除条件=「最後に通した質問(.askgate)より新しいuser-simulator実行痕跡(.reviewed=misses.md書込)」。質問1件につきpreanswer1回を要求する。
    # Gate before AskUserQuestion: block until a preanswer (misses.md write) fresher than the last allowed question exists.
    if [ -e "$STATE.reviewed" ] && { [ ! -e "$STATE.askgate" ] || [ "$STATE.reviewed" -nt "$STATE.askgate" ]; }; then
      # 対応するpreanswer痕跡あり=この質問分の予測が済んでいる。1件分を消費して通す / Consume one preanswer, let the question through
      touch "$STATE.askgate"; rm -f "$STATE.askblocks"; exit 0
    fi
    # ブロック上限は質問ごとにリセット（通過時rm）。無限ブロック防止 / Per-question block cap, reset on pass
    COUNT=0
    [ -f "$STATE.askblocks" ] && COUNT=$(cat "$STATE.askblocks")
    if [ "$COUNT" -ge 2 ]; then touch "$STATE.askgate"; rm -f "$STATE.askblocks"; exit 0; fi
    echo $((COUNT + 1)) > "$STATE.askblocks"
    echo "AskUserQuestionを出す前に user-simulator preanswerモードを通してください。.claude/skills/user-simulator/modes/preanswer/protocol.md に従い判事の予測を取り、確信高は前提宣言へ降格・確信中は予測注記付きで質問し、採点を modes/improve/misses.md に追記してください。追記後に再度AskUserQuestionを出せば通過できます。" >&2
    exit 2 ;;
  *)
    exit 0 ;;
esac

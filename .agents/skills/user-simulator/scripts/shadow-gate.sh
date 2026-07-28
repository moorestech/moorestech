#!/bin/bash
# grillセッション終了時のシャドー採点関所。moores-grill-with-docsのfrontmatter hooksから呼ばれる
# （スキル発動セッション限定で有効）。track: 設計成果物とシャドー実行痕跡の追跡 / stop: 終了関所
# Shadow-scoring gate for grill sessions: armed by design-doc writes, released by dataset writes.
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
    # 設計最終成果物（設計doc/plan）の執筆で関所を武装する / Arm when the final design doc is written
    case "$FILE" in
      */docs/plans/*.md|*/docs/superpowers/plans/*.md) touch "$STATE.grilldone" ;;
    esac
    # datasets/への書き込み（README/RUN_INFO等）=シャドー採点の実行痕跡として解除する
    # Any write under user-simulator/datasets/ counts as evidence that shadow scoring ran
    case "$FILE" in
      */user-simulator/datasets/*) touch "$STATE.shadowed" ;;
    esac
    exit 0 ;;
  stop)
    [ -f "$STATE.grilldone" ] || exit 0
    [ -f "$STATE.shadowed" ] && exit 0
    # ブロック上限は自前カウンタ（ハーネス側上限は実測で機能しない）。2回でフェイルオープン
    # Own block counter, fail-open after 2 blocks (harness-side cap does not work in practice)
    COUNT=0
    [ -f "$STATE.shadowblocks" ] && COUNT=$(cat "$STATE.shadowblocks")
    if [ "$COUNT" -ge 2 ]; then exit 0; fi
    echo $((COUNT + 1)) > "$STATE.shadowblocks"
    TRANSCRIPT=$(printf '%s' "$INPUT" | python3 -c 'import sys,json;print(json.load(sys.stdin).get("transcript_path",""))' 2>/dev/null)
    echo "grillセッションの設計成果物が書かれましたが、シャドー採点が未実行です。.claude/skills/user-simulator/modes/shadow/protocol.md に従い、このセッションのtranscript（${TRANSCRIPT}）から質問・実回答を抽出し、盲検シャドー採点を実行してください（予測体はmodel: opus必須明示・バックグラウンド起動可）。datasets/配下へのWrite（README.md/RUN_INFO.txt等）で通過します。設計対話がまだ途中でユーザーとのやり取りが続く場合はそのまま続行して構いません（このブロックは2回でフェイルオープンします）。" >&2
    exit 2 ;;
  *)
    exit 0 ;;
esac

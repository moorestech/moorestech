#!/bin/bash
# この worktree を作った後に Unity のセーブ置き場（Saves/）へ作られた・書き換えられた
# ワールドを列挙する。作業の終わりに、このうち自分の作業が作ったものを消すための候補表示。
# Saves/ は worktree の外にあり、worktree を畳んでも消えないため。
# 判定の起点は worktree の .git ファイルの更新時刻（worktree 作成時に書かれる）。
# 他セッションが同じ期間に触ったワールドも並ぶので、消すかどうかは名前と中身で決める。
#
# Lists worlds created or modified under Unity's Saves/ after this worktree was created,
# as cleanup candidates at the end of the work. Saves/ lives outside the worktree and survives its removal.
# The reference time is the mtime of the worktree's .git file (written at worktree creation).
# Worlds touched by other sessions in the same period also appear, so decide by name and content.
#
# 使い方: list-work-saves.sh（作業中の worktree を cwd にして実行する）
set -euo pipefail

case "$(uname -s)" in
    Darwin) saves_dir="$HOME/Library/Application Support/moorestech/Saves" ;;
    Linux) saves_dir="$HOME/.moorestech/Saves" ;;
    *) saves_dir="$APPDATA/.moorestech/Saves" ;;
esac

worktree_root=$(git rev-parse --show-toplevel)
reference_file="$worktree_root/.git"
if [[ ! -e "$reference_file" ]]; then
    echo "NG: 起点にする ${reference_file} がありません" >&2
    exit 1
fi
if [[ ! -d "$saves_dir" ]]; then
    echo "セーブ置き場がありません（${saves_dir}）。消す候補は無し"
    exit 0
fi

echo "== ${saves_dir} のうち ${worktree_root} の作成後に作られた・書き換えられたもの =="
# 上書き保存ではディレクトリ自体の時刻が変わらないので、中のファイルまで見て判定する
# Overwriting files does not touch the directory mtime, so look inside each world
for entry in "$saves_dir"/*; do
    [[ -e "$entry" ]] || continue
    if [[ -n "$(find "$entry" -newer "$reference_file" -print -quit)" ]]; then
        echo "$entry"
    fi
done

#!/usr/bin/env bash
set -euo pipefail

# メインブランチの Unity の moorestech_server/Library フォルダを
# 現在の git worktree（このスクリプトを実行したリポジトリ環境）の
# moorestech_server/Library にコピーするスクリプト

# Git 管理下か確認
if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Error: このディレクトリは git リポジトリではありません。" >&2
  exit 1
fi

echo "現在のディレクトリ: $(pwd)"

# git worktree list からメインワークツリーのパスを取得
# 一番最初の worktree エントリがメインワークツリー
main_repo_path=$(git worktree list --porcelain | awk '/^worktree /{print $2; exit}')

if [ -z "${main_repo_path:-}" ]; then
  echo "Error: メインワークツリーのパスを特定できませんでした。" >&2
  exit 1
fi

# 現在の worktree（このスクリプトを実行している側）のルート
current_worktree_root=$(git rev-parse --show-toplevel)

# メインワークツリー自身で実行していないかチェック
if [ "$current_worktree_root" = "$main_repo_path" ]; then
  echo "Error: 現在の worktree がメインワークツリーです。コピー元とコピー先が同一になります。" >&2
  exit 1
fi

echo "メインワークツリー:      $main_repo_path"
echo "現在の git worktree ルート: $current_worktree_root"

src="$main_repo_path/moorestech_server/Library"
dst="$current_worktree_root/moorestech_server/Library"

if [ ! -d "$src" ]; then
  echo "Error: コピー元のディレクトリが存在しません: $src" >&2
  exit 1
fi

echo "コピー元: $src"
echo "コピー先: $dst"

# コピー先のディレクトリ作成
mkdir -p "$dst"

# rsync があればミラーリング、なければ cp -a でコピー
if command -v rsync >/dev/null 2>&1; then
  # メインワークツリーの Library の内容を現在の worktree に同期
  rsync -a --delete "$src/" "$dst/"
else
  # rsync が無い環境向けのフォールバック
  cp -a "$src/." "$dst/"
fi

echo "コピー完了しました。"

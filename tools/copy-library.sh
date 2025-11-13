#!/usr/bin/env bash
set -euo pipefail

# ここにコピーしたいディレクトリ（リポジトリルートからの相対パス）を列挙
COPY_DIRS=(
  "moorestech_server/Library"
  "moorestech_client/Library"
  # 例:
  # "some/other/path"
)

# メインブランチの Unity の moorestech_server/Library などを
# 現在の git worktree（このスクリプトを実行したリポジトリ環境）の
# 対応するディレクトリにコピーするスクリプト

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

for rel_path in "${COPY_DIRS[@]}"; do
  src="$main_repo_path/$rel_path"
  dst="$current_worktree_root/$rel_path"

  if [ ! -d "$src" ]; then
    echo "Error: コピー元のディレクトリが存在しません: $src" >&2
    exit 1
  fi

  echo "コピー元: $src"
  echo "コピー先: $dst"

  mkdir -p "$dst"

  if command -v rsync >/dev/null 2>&1; then
    rsync -a --delete "$src/" "$dst/"
  else
    cp -a "$src/." "$dst/"
  fi

  echo "コピー完了: $rel_path"
done

echo "すべてのコピーが完了しました。"

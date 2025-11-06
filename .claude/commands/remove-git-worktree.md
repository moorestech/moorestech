---
name:remove-git-worktree
---

以下のgit worktreeクリーンアップワークフローを実行してください：

1. `git worktree list`を使用して現在のすべてのgit worktreeを一覧表示する
2. メインリポジトリ（プロジェクトルートにあるもの）を除くすべてのworktreeを特定する
3. メイン以外の各worktreeに対して：
   - worktreeのパスと関連付けられたブランチ名を抽出する
   - `git worktree remove <path>`を使用してworktreeを削除する
   - `git branch -D <branch-name>`を使用して関連するブランチを削除する
4. 削除されたすべてのworktreeとブランチのサマリーを表示する

重要な安全上の考慮事項：
- メインworktree（/Users/sato-katsumi/moorestechにあるもの）は絶対に削除しない
- 削除前に、削除されるworktreeとブランチをユーザーに表示し、確認を求める
- worktreeが削除できない場合（コミットされていない変更がある等）、明示的なユーザー確認後にのみ`git worktree remove --force`を使用する
- ブランチが存在しない、または既に削除されている等のエッジケースに対応する

期待される出力形式：
```
削除対象のworktreeが見つかりました：
1. [パス] -> ブランチ: [ブランチ名]
2. [パス] -> ブランチ: [ブランチ名]

削除を実行しますか？ (yes/no)
```

ユーザーの確認後、削除を実行し、結果を報告してください。